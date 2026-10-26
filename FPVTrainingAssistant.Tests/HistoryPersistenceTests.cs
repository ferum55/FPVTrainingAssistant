using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class HistoryPersistenceTests
{
    [Fact]
    public async Task OperatorDetailsUpdateOnlyMetadataAndSessionCountsComeFromDatabase()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var s = PersistenceTestDatabase.Snapshot();
        await db.Sessions.SaveAsync(s);
        await db.Sessions.SaveAsync(PersistenceTestDatabase.Snapshot("valid_target_mission.json"));
        Assert.True(await db.History.UpdateOperatorAsync(s.Session.OperatorId, "Operator 'A'", "Notes; SELECT * FROM operators"));
        var op = Assert.Single(await db.History.ListOperatorsAsync());
        Assert.Equal(s.Session.OperatorId, op.Profile.OperatorId);
        Assert.Equal("Operator 'A'", op.Profile.DisplayName);
        Assert.Equal("Notes; SELECT * FROM operators", op.Profile.Notes);
        Assert.Equal(2, op.SessionCount);
        Assert.Equal((82.67 + 90) / 2, op.AverageScore);
        Assert.Equal(s.Session.StartedAtUtc, op.LastSessionUtc);
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM operators"));
        Assert.False(await db.History.UpdateOperatorAsync("missing", "Name", null));
    }

    [Fact]
    public async Task HistoryScopesSortsFiltersAndComputesProgressFromSavedScores()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        foreach (var i in new[] { 1, 2, 3 })
        {
            var snapshot = PersistenceTestDatabase.Snapshot($"history_progress_0{i}.json");
            Assert.Equal(new[] { 56d, 82.67, 100 }[i - 1], snapshot.Analysis.FinalScore);
            Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(snapshot)).State);
        }
        await db.Sessions.SaveAsync(PersistenceTestDatabase.Snapshot("valid_target_mission.json"));
        await db.Sessions.SaveAsync(PersistenceTestDatabase.Snapshot("valid_target_mission.json", d => { d["operatorId"] = "other-operator"; d["sessionId"] = "other-target"; }));
        Assert.Equal(2, (await db.History.ListOperatorsAsync()).Count);
        var sessions = await db.History.ListSessionsAsync("synthetic-operator-001", null);
        Assert.Equal(4, sessions.Count);
        Assert.Equal(sessions.OrderByDescending(s => s.StartedAtUtc).Select(s => s.SessionId), sessions.Select(s => s.SessionId));
        Assert.Single(await db.History.ListSessionsAsync("other-operator", null));
        var vm = new HistoryViewModel(db.History, _ => Task.CompletedTask);
        await vm.RefreshAsync();
        vm.SelectedOperator = vm.Operators.Single(o => o.Profile.OperatorId == "synthetic-operator-001"); await vm.SelectionLoad;
        Assert.Equal("4", vm.SessionCountText); Assert.Equal("100.00", vm.BestScoreText); Assert.Equal("90.00", vm.LatestScoreText);
        vm.SelectedExercise = "ObstacleCourse"; await vm.SelectionLoad;
        Assert.Equal(3, vm.Sessions.Count); Assert.Equal("79.56", vm.AverageScoreText);
        Assert.Equal(new[] { 56d, 82.67, 100 }, vm.ProgressPoints.Select(p => p.Score));
        Assert.Equal(vm.ProgressPoints.OrderBy(p => p.StartedAtUtc), vm.ProgressPoints);
        vm.SelectedExercise = "TargetMission"; await vm.SelectionLoad;
        Assert.Single(vm.ProgressPoints); Assert.Equal("90.00", vm.AverageScoreText);
    }

    [Fact]
    public async Task NewestListDoesNotMixOperatorsWithSQLLikeIds()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        const string operatorId = "pilot'; DROP TABLE operators; --";
        await db.Sessions.SaveAsync(PersistenceTestDatabase.Snapshot(edit: d => d["operatorId"] = operatorId));
        Assert.Single(await db.History.ListSessionsAsync(operatorId, null));
        Assert.Empty(await db.History.ListSessionsAsync("other", null));
        Assert.Single(await db.History.ListOperatorsAsync());
    }

    [Fact]
    public async Task EmptyHistoryZeroSessionsAndNoMatchingFilterHaveClearStates()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var vm = new HistoryViewModel(db.History, _ => Task.CompletedTask);
        await vm.RefreshAsync();
        Assert.Equal("No training history is available yet.", vm.EmptyMessage);
        Assert.Empty(vm.ProgressPoints); Assert.Equal("N/A", vm.AverageScoreText);
        db.Execute("INSERT INTO operators VALUES('empty',NULL,NULL,'2026-10-26T00:00:00+00:00','2026-10-26T00:00:00+00:00')");
        await vm.RefreshAsync();
        Assert.Equal("No sessions are available for this operator.", vm.EmptyMessage);
        vm.SelectedExercise = "TargetMission"; await vm.SelectionLoad;
        Assert.Equal("No sessions match the selected exercise filter.", vm.EmptyMessage);
        Assert.False(vm.OpenResultCommand.CanExecute(null));
    }

    [Fact]
    public async Task HistoryRefreshAndOperatorEditorSeeChangesWithoutRestart()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var vm = new HistoryViewModel(db.History, _ => Task.CompletedTask);
        await vm.RefreshAsync(); Assert.Empty(vm.Operators);
        await db.Sessions.SaveAsync(PersistenceTestDatabase.Snapshot());
        await vm.RefreshAsync(); Assert.Single(vm.Sessions);
        vm.DisplayName = "Synthetic pilot"; vm.Notes = "Practice notes";
        await vm.SaveOperatorCommand.ExecuteAsync();
        Assert.Equal("Synthetic pilot", vm.SelectedOperator!.Profile.DisplayName);
        Assert.Equal("Practice notes", vm.SelectedOperator.Profile.Notes);
        Assert.Contains("saved", vm.Message);
        var reopened = new HistoryViewModel(new HistoryRepository(new DatabaseConnectionFactory(db)), _ => Task.CompletedTask);
        await reopened.RefreshAsync();
        Assert.Equal("Synthetic pilot", reopened.DisplayName); Assert.Equal("Practice notes", reopened.Notes);
    }

    [Fact]
    public async Task SameSessionConcurrentSavesHaveOneAtomicWinner()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var snapshot = PersistenceTestDatabase.Snapshot();
        var outcomes = await Task.WhenAll(db.Sessions.SaveAsync(snapshot), new SessionRepository(db.Connections).SaveAsync(snapshot));
        Assert.Single(outcomes, r => r.State == SessionSaveState.Saved);
        Assert.Single(outcomes, r => r.State == SessionSaveState.Duplicate);
        Assert.Equal(21L, db.Scalar("SELECT COUNT(*) FROM telemetry_samples"));
    }
}
