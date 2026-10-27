using System.Text.Json.Nodes;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class ConfiguredProfileWorkflowTests
{
    [Fact]
    public async Task ExplicitVersionsResolveExactlyEvenWhenAnOlderVersionIsInactive()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var original=PersistenceTestDatabase.Snapshot();
        using var draft=new ProfileEditorViewModel(original.Profile);
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.CollisionAvoidance).Parameters[0].ValueText="40";
        var edited=draft.Build(out _)!;
        await repo.CreateNewVersionAsync(edited,1,true);
        var provider=new ExerciseProfileProvider(repo);
        var v1=await provider.ResolveAsync(original.Session,true);
        Assert.NotNull(v1);Assert.Equal(1,v1.Version);Assert.False(v1.IsActive);
        var analyzer=new SessionAnalyzer();
        Assert.Throws<AnalysisConfigurationException>(()=>analyzer.Analyze(original.Session,v1));
        Assert.Equal(82.67,analyzer.AnalyzeRequestedVersion(original.Session,v1).FinalScore);
        var document=ReplayTestFiles.Document();document["profileVersion"]=2;document["sessionId"]="configured-session-two";
        var session=ReplayTestFiles.Session(document);
        var v2=await provider.ResolveAsync(session,true);
        Assert.Equal(2,v2!.Version);Assert.Equal(78.67,analyzer.AnalyzeRequestedVersion(session,v2).FinalScore);
        document["profileVersion"]=3;Assert.Null(await provider.ResolveAsync(ReplayTestFiles.Session(document),true));
    }

    [Fact]
    public async Task MainSettingsNavigationWorksWithoutActiveSessionAndFutureImportsUseDatabaseVersions()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();using var files=new ReplayTestFiles();
        var profiles=new ExerciseProfileRepository(db.Connections);var settings=new ApplicationSettingsService(db.Connections);await settings.LoadAsync();
        var vm=new MainViewModel(new NoDialog(),new SessionImportService(),new ExerciseProfileProvider(profiles),new SessionAnalyzer(),null,
            db.Sessions,db.History,null,profiles,settings);
        Assert.True(vm.NavigateSettingsCommand.CanExecute(null));await vm.NavigateSettingsCommand.ExecuteAsync();Assert.True(vm.IsSettingsScreen);
        vm.SettingsView!.Draft!.Criteria.Single(c=>c.RuleType==RuleTypes.CollisionAvoidance).Parameters[0].ValueText="40";
        vm.SettingsView.ActivateAfterSave=true;await vm.SettingsView.SaveVersionCommand.ExecuteAsync();
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("replay_obstacle_course_v1_1.json"));
        Assert.Equal(1,vm.Profile!.Version);Assert.Equal(82.67,vm.AnalysisResult!.FinalScore);
        Assert.Equal(SessionSaveState.Saved,vm.LastSaveResult!.State);
        await settings.SaveAsync(new(2,false,false));
        var document=ReplayTestFiles.Document();document["profileVersion"]=2;document["sessionId"]="settings-workflow-v2";
        await vm.ImportFileAsync(files.Create(document.ToJsonString()));
        Assert.Equal(2,vm.Profile!.Version);Assert.Equal(78.67,vm.AnalysisResult!.FinalScore);
        Assert.Equal(2,vm.TrajectoryView!.PlaybackSpeed);
        var saved=(await db.Sessions.LoadAsync("settings-workflow-v2"))!;Assert.Equal(2,saved.Profile.Version);
        await vm.OpenHistoricalAsync("synthetic-replay-obstacle-course-001");
        Assert.Equal(1,vm.Profile.Version);Assert.Equal(82.67,vm.AnalysisResult.FinalScore);
    }

    [Fact]
    public async Task VersionOneTargetBaselineAlsoRemainsUnchangedAfterTargetVersionTwoIsActivated()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var original=PersistenceTestDatabase.Snapshot("replay_target_mission_v1_1.json");
        using var draft=new ProfileEditorViewModel(original.Profile);
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.TargetDamage).Parameters.Single(p=>p.Key=="DamageScore_Default").ValueText="60";
        await repo.CreateNewVersionAsync(draft.Build(out _)!,1,true);
        var oldProfile=await new ExerciseProfileProvider(repo).ResolveAsync(original.Session,true);
        Assert.Equal(90,new SessionAnalyzer().AnalyzeRequestedVersion(original.Session,oldProfile!).FinalScore);
        var doc=ReplayTestFiles.Document("replay_target_mission_v1_1.json");doc["profileVersion"]=2;
        var session=ReplayTestFiles.Session(doc);var profile=await repo.LoadAsync(session.ProfileId,2);
        Assert.Equal(92,new SessionAnalyzer().AnalyzeRequestedVersion(session,profile!).FinalScore);
    }
    private sealed class NoDialog : IFileDialogService {public string? ChooseSessionFile()=>null;}
}
