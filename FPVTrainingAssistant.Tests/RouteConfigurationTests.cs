using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class RouteConfigurationTests
{
    [Fact]
    public async Task AddRemoveReorderNormalizeSequencesAndPersistEditedRoute()
    {
        using var db=new PersistenceTestDatabase();await db.Initialize();
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        draft.AddPointCommand.Execute(null);Assert.Equal(5,draft.Points.Count);
        var added=draft.SelectedPoint!;added.XText="25";added.YText="9";added.ZText="3";added.PointType="Checkpoint";
        added.GreenRadiusText="2";added.YellowRadiusText="4";
        draft.MoveUpCommand.Execute(null);Assert.Same(added,draft.Points[3]);
        draft.MoveDownCommand.Execute(null);Assert.Same(added,draft.Points[4]);
        draft.SelectedPoint=draft.Points[1];draft.RemovePointCommand.Execute(null);
        Assert.Equal(new[]{0,1,2,3},draft.Points.Select(p=>p.SequenceNo));
        Assert.True(draft.HasUnsavedChanges);
        var candidate=draft.Build(out var issues);Assert.Empty(issues);
        var repo=new ExerciseProfileRepository(db.Connections);
        await repo.CreateNewVersionAsync(candidate!,1,false);
        var loaded=(await repo.LoadAsync(candidate!.ProfileId,2))!;
        var last=loaded.ReferencePoints.Last();
        Assert.Equal(25,last.X);Assert.Equal(9,last.Y);Assert.Equal(3,last.Z);
        Assert.Equal(2,last.GreenRadiusM);Assert.Equal(4,last.YellowRadiusM);Assert.Equal("Checkpoint",last.PointType);
        Assert.Equal(4,(await repo.LoadAsync(candidate.ProfileId,1))!.ReferencePoints.Count);
    }

    [Theory]
    [InlineData("-1","3")]
    [InlineData("4","3")]
    [InlineData("1","NaN")]
    [InlineData("Infinity","3")]
    public void InvalidRadiiPreventSaving(string green,string yellow)
    {
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        draft.Points[0].GreenRadiusText=green;draft.Points[0].YellowRadiusText=yellow;
        Assert.Null(draft.Build(out var issues));Assert.NotEmpty(issues);
    }

    [Fact]
    public void OptionalMultiPointRouteMustRemainUsableForTheExistingRouteMetrics()
    {
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.RouteAdherence).IsEnabled=false;
        foreach(var point in draft.Points) {point.XText="0";point.YText="0";}
        Assert.Null(draft.Build(out var issues));Assert.Contains(issues,i=>i.Contains("non-zero"));
    }

    [Fact]
    public void RouteAdherenceRequiresTwoUsablePointsAndFiniteCoordinates()
    {
        using var draft=new ProfileEditorViewModel(PersistenceTestDatabase.Snapshot().Profile);
        foreach(var point in draft.Points) {point.XText="0";point.YText="0";}
        Assert.Null(draft.Build(out _));
        while(draft.Points.Count>1) {draft.SelectedPoint=draft.Points.Last();draft.RemovePointCommand.Execute(null);}
        Assert.Null(draft.Build(out _));
        draft.Criteria.Single(c=>c.RuleType==RuleTypes.RouteAdherence).IsEnabled=false;
        Assert.NotNull(draft.Build(out _));
        draft.Points[0].XText="invalid";Assert.Null(draft.Build(out _));
    }
}
