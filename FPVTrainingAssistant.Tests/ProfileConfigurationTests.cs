using System.Text.Json;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class ProfileConfigurationTests
{
    [Fact]
    public async Task NewVersionRetainsOriginalCriteriaRouteAndHistoricalResults()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var old=PersistenceTestDatabase.Snapshot();await db.Sessions.SaveAsync(old);
        var profiles=new ExerciseProfileRepository(db.Connections);
        using var draft=new ProfileEditorViewModel(old.Profile);
        draft.Name="Configured obstacle";
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.CollisionAvoidance).Parameters[0].ValueText="40";
        draft.Criteria[0].WeightText="50";draft.Points[0].GreenRadiusText="2";
        var candidate=draft.Build(out var issues);Assert.Empty(issues);
        var saved=await profiles.CreateNewVersionAsync(candidate!,1,false);
        Assert.Equal(ProfileSaveState.Saved,saved.State);Assert.Equal(2,saved.Profile!.Version);
        Assert.False(saved.Profile.IsActive);
        var version1=(await profiles.LoadAsync(old.Profile.ProfileId,1))!;
        Assert.Equal(JsonSerializer.Serialize(old.Profile),JsonSerializer.Serialize(version1));
        Assert.Equal(2,(await profiles.GetProfilesAsync()).Count(p=>p.ProfileId==old.Profile.ProfileId));
        Assert.Equal(ProfileSaveState.Saved,(await profiles.SetActiveVersionAsync(old.Profile.ProfileId,2)).State);
        Assert.False((await profiles.LoadAsync(old.Profile.ProfileId,1))!.IsActive);
        Assert.Equal(2,(await profiles.GetActiveVersionAsync(old.Profile.ProfileId))!.Version);
        Assert.Single(await profiles.GetProfilesAsync(),p=>p.ProfileId==old.Profile.ProfileId && p.IsActive);
        var historical=(await db.Sessions.LoadAsync(old.Session.SessionId))!;
        Assert.Equal(1,historical.Session.ProfileVersion);
        Assert.Equal(JsonSerializer.Serialize(old.Analysis),JsonSerializer.Serialize(historical.Analysis));
        Assert.Equal(JsonSerializer.Serialize(old.Profile.Criteria),JsonSerializer.Serialize(historical.Profile.Criteria));
        Assert.Equal(JsonSerializer.Serialize(old.Profile.ReferencePoints),JsonSerializer.Serialize(historical.Profile.ReferencePoints));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("not a number")]
    public void InvalidEnabledWeightCannotProduceSavableWorkingCopy(string value)
    {
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        draft.Criteria[0].WeightText=value;
        Assert.Null(draft.Build(out var issues));Assert.NotEmpty(issues);
    }

    [Fact]
    public void NoEnabledCriteriaIsRejectedAndDisabledZeroWeightIsAllowed()
    {
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        foreach(var c in draft.Criteria) c.IsEnabled=false;
        Assert.Null(draft.Build(out var issues));Assert.Contains(issues,i=>i.Contains("no enabled criteria"));
        draft.Criteria[0].IsEnabled=true;draft.Criteria[1].WeightText="0";
        Assert.NotNull(draft.Build(out issues));Assert.Empty(issues);
    }

    [Fact]
    public async Task CriterionWeightsEnabledStateAndParametersPersistWithoutRewritingWeights()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        draft.Criteria[0].WeightText="4";
        draft.Criteria[1].WeightText="3";draft.Criteria[2].WeightText="2";draft.Criteria[3].WeightText="1";
        Assert.Equal(new[]{"40.0%","30.0%","20.0%","10.0%"},draft.Criteria.Select(c=>c.Contribution));
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.CollisionAvoidance).Parameters[0].ValueText="35";
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.RouteAdherence).Parameters[0].ValueText="0.25";
        var time=draft.Criteria.Single(c=>c.RuleType==RuleTypes.CompletionTime);
        time.Parameters[0].ValueText="20";time.Parameters[1].ValueText="90";time.IsEnabled=false;
        var candidate=draft.Build(out var issues);Assert.Empty(issues);
        Assert.Equal(ProfileSaveState.Saved,(await repo.CreateNewVersionAsync(candidate!,1,true)).State);
        var loaded=(await repo.LoadAsync(candidate!.ProfileId,2))!;
        Assert.Equal(new[]{4d,3,2,1},loaded.Criteria.Select(c=>c.Weight));
        Assert.False(loaded.Criteria[3].IsEnabled);
        Assert.Equal(35,loaded.Criteria[2].Parameters!["PenaltyPerCollision"]);
        Assert.Equal(0.25,loaded.Criteria[1].Parameters!["YellowZoneFactor"]);
        Assert.Equal(20,loaded.Criteria[3].Parameters!["TargetTimeSec"]);
        Assert.Equal(90,loaded.Criteria[3].Parameters!["MaximumTimeSec"]);
    }

    [Theory]
    [InlineData(RuleTypes.CollisionAvoidance,"PenaltyPerCollision","-1")]
    [InlineData(RuleTypes.RouteAdherence,"YellowZoneFactor","1.1")]
    [InlineData(RuleTypes.CompletionTime,"MaximumTimeSec","1")]
    [InlineData(RuleTypes.CompletionTime,"TargetTimeSec","0")]
    public void RuleParameterValidationUsesTheAnalysisRules(string rule,string key,string value)
    {
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        draft.Criteria.Single(c=>c.RuleType==rule).Parameters.Single(p=>p.Key==key).ValueText=value;
        Assert.Null(draft.Build(out var issues));Assert.NotEmpty(issues);
    }

    [Fact]
    public async Task TargetDamageCategorySettingsPersistAndRejectOutOfRangeValues()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot("valid_target_mission.json").Profile);
        var damage=draft.Criteria.Single(c=>c.RuleType==RuleTypes.TargetDamage);
        Assert.Equal(6,damage.Parameters.Count);
        foreach(var parameter in damage.Parameters) parameter.ValueText="60";
        var candidate=draft.Build(out var issues);Assert.Empty(issues);
        var repo=new ExerciseProfileRepository(db.Connections);
        Assert.Equal(ProfileSaveState.Saved,(await repo.CreateNewVersionAsync(candidate!,1,true)).State);
        var loaded=(await repo.LoadAsync(candidate!.ProfileId,2))!;
        Assert.All(loaded.Criteria.Single(c=>c.RuleType==RuleTypes.TargetDamage).Parameters!,p=>Assert.Equal(60,p.Value));
        damage.Parameters[0].ValueText="101";Assert.Null(draft.Build(out _));
        damage.Parameters[0].ValueText="-1";Assert.Null(draft.Build(out _));
    }

    [Fact]
    public async Task ConcurrentVersionSavesHaveOneWinnerAndNoOverwrites()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        var repo=new ExerciseProfileRepository(db.Connections);
        var profile=PersistenceTestDatabase.Snapshot().Profile;
        var results=await Task.WhenAll(repo.CreateNewVersionAsync(profile,1,true),repo.CreateNewVersionAsync(profile,1,true));
        Assert.Single(results,r=>r.State==ProfileSaveState.Saved);Assert.Single(results,r=>r.State==ProfileSaveState.Conflict);
        Assert.Equal(2,(await repo.GetProfilesAsync()).Count(p=>p.ProfileId==profile.ProfileId));
        Assert.Single(await repo.GetProfilesAsync(),p=>p.ProfileId==profile.ProfileId && p.IsActive);
    }

    [Fact]
    public async Task FailureDuringProfileInsertRollsBackActivationAndChildren()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        db.Execute("CREATE TRIGGER profile_failure BEFORE INSERT ON evaluation_criteria WHEN NEW.profile_version=2 BEGIN SELECT RAISE(ABORT,'controlled test failure'); END;");
        var repo=new ExerciseProfileRepository(db.Connections);var p=PersistenceTestDatabase.Snapshot().Profile;
        Assert.Equal(ProfileSaveState.Failed,(await repo.CreateNewVersionAsync(p,1,true)).State);
        Assert.Null(await repo.LoadAsync(p.ProfileId,2));Assert.True((await repo.LoadAsync(p.ProfileId,1))!.IsActive);
        Assert.Equal(4L,db.Scalar("SELECT COUNT(*) FROM reference_points"));
    }
}
