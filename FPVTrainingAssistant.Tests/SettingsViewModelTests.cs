using System.Text.Json;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task EditorLoadsIndependentWorkingCopyAndResetRestoresStoredProfile()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var vm=new SettingsViewModel(repo,new ApplicationSettingsService(db.Connections));
        await vm.RefreshAsync();Assert.Equal(2,vm.Profiles.Count);Assert.NotNull(vm.Draft);
        var source=vm.Draft!.Source;var original=JsonSerializer.Serialize(source);
        vm.Draft.Name="Unsaved name";vm.Draft.Criteria[0].WeightText="17";vm.Draft.Points[0].XText="9";
        Assert.True(vm.Draft.HasUnsavedChanges);Assert.True(vm.SaveVersionCommand.CanExecute(null));
        Assert.Equal(original,JsonSerializer.Serialize((await repo.LoadAsync(source.ProfileId,1))!));
        vm.ResetCommand.Execute(null);
        Assert.False(vm.Draft!.HasUnsavedChanges);Assert.Equal(source.Name,vm.Draft.Name);
        Assert.Equal(source.Criteria[0].Weight.ToString(System.Globalization.CultureInfo.InvariantCulture),vm.Draft.Criteria[0].WeightText);
        Assert.Equal(2,(await repo.GetProfilesAsync()).Count);
    }

    [Fact]
    public async Task ProfileSwitchRequiresExplicitDiscardAndCanKeepEditing()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var vm=new SettingsViewModel(new ExerciseProfileRepository(db.Connections),new ApplicationSettingsService(db.Connections));await vm.RefreshAsync();
        var selected=vm.SelectedProfile!;var other=vm.Profiles.Single(p=>p.Profile.ProfileId!=selected.Profile.ProfileId);
        vm.Draft!.Name="Keep this work";vm.SelectedProfile=other;
        Assert.Same(selected,vm.SelectedProfile);Assert.True(vm.HasPendingSelection);Assert.Equal("Keep this work",vm.Draft.Name);
        vm.KeepEditingCommand.Execute(null);Assert.False(vm.HasPendingSelection);Assert.True(vm.Draft.HasUnsavedChanges);
        vm.SelectedProfile=other;vm.DiscardAndSwitchCommand.Execute(null);
        Assert.Same(other,vm.SelectedProfile);Assert.False(vm.HasPendingSelection);Assert.False(vm.Draft!.HasUnsavedChanges);
    }

    [Fact]
    public async Task SaveCreatesVersionRefreshesListAndActivationIsIndependent()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var vm=new SettingsViewModel(repo,new ApplicationSettingsService(db.Connections));await vm.RefreshAsync();
        var id=vm.Draft!.ProfileId;vm.Draft.Criteria[0].WeightText="55";
        await vm.SaveVersionCommand.ExecuteAsync();
        Assert.Equal(2,vm.SelectedProfile!.Profile.Version);Assert.Contains("version 2 saved",vm.Message);
        Assert.Equal(3,vm.Profiles.Count);Assert.False(vm.Draft!.HasUnsavedChanges);
        Assert.Equal(1,(await repo.GetActiveVersionAsync(id))!.Version);
        await vm.ActivateCommand.ExecuteAsync();
        Assert.Equal(2,(await repo.GetActiveVersionAsync(id))!.Version);Assert.Equal("Active profile updated.",vm.Message);
        Assert.Equal(40,(await repo.LoadAsync(id,1))!.Criteria[0].Weight);
    }

    [Fact]
    public async Task ValidationBlocksDatabaseSaveAndRefreshDoesNotLoseUnsavedEdits()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var vm=new SettingsViewModel(repo,new ApplicationSettingsService(db.Connections));await vm.RefreshAsync();
        vm.Draft!.Criteria[0].WeightText="0";
        await vm.SaveVersionCommand.ExecuteAsync();
        Assert.True(vm.HasValidationIssues);Assert.Equal(2,(await repo.GetProfilesAsync()).Count);
        Assert.True(vm.Draft.HasUnsavedChanges);
        await vm.RefreshAsync();Assert.Equal("0",vm.Draft.Criteria[0].WeightText);Assert.True(vm.Draft.HasUnsavedChanges);
    }

    [Fact]
    public async Task ApplicationPreferencesHaveExplicitSaveAndSurviveRefreshAndRecreation()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var preferences=new ApplicationSettingsService(db.Connections);
        var vm=new SettingsViewModel(repo,preferences);await vm.RefreshAsync();
        Assert.Equal(1,vm.DefaultReplaySpeed);Assert.False(vm.HasUnsavedApplicationSettings);
        vm.DefaultReplaySpeed=2;vm.ShowMinimapByDefault=false;vm.ShowEventLogByDefault=false;
        Assert.True(vm.HasUnsavedApplicationSettings);
        await vm.RefreshAsync();Assert.Equal(2,vm.DefaultReplaySpeed);Assert.False(vm.ShowMinimapByDefault);
        await vm.SaveApplicationCommand.ExecuteAsync();Assert.Equal("Application settings saved.",vm.Message);Assert.False(vm.HasUnsavedApplicationSettings);
        var recreated=new SettingsViewModel(repo,new ApplicationSettingsService(db.Connections));await recreated.RefreshAsync();
        Assert.Equal(2,recreated.DefaultReplaySpeed);Assert.False(recreated.ShowMinimapByDefault);Assert.False(recreated.ShowEventLogByDefault);
    }

    [Fact]
    public async Task ConcurrentEditorConflictKeepsTheWorkingCopyForReview()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);var vm=new SettingsViewModel(repo,new ApplicationSettingsService(db.Connections));await vm.RefreshAsync();
        var source=vm.Draft!.Source;vm.Draft.Name="This editor's name";
        await repo.CreateNewVersionAsync(source,1,false);
        await vm.SaveVersionCommand.ExecuteAsync();
        Assert.Contains("Another profile version",vm.Message);Assert.Equal("This editor's name",vm.Draft.Name);Assert.True(vm.Draft.HasUnsavedChanges);
        await vm.RefreshAsync();await vm.SaveVersionCommand.ExecuteAsync();Assert.Equal(3,vm.SelectedProfile!.Profile.Version);
    }

    [Fact]
    public async Task RepositoryRejectsInvalidConfigurationEvenWithoutTheEditor()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var profile=JsonSerializer.SerializeToNode(PersistenceTestDatabase.Snapshot().Profile)!;
        profile["Criteria"]![0]!["Weight"]=0;
        var invalid=profile.Deserialize<ExerciseProfile>()!;
        var result=await new ExerciseProfileRepository(db.Connections).CreateNewVersionAsync(invalid,1,true);
        Assert.Equal(ProfileSaveState.Invalid,result.State);Assert.NotEmpty(result.Issues!);
        Assert.Equal(2L,db.Scalar("SELECT COUNT(*) FROM exercise_profiles"));
    }
}
