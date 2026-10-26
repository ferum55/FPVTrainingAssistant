using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class PersistenceWorkflowTests
{
    [Theory]
    [InlineData(SessionSaveState.Saved)]
    [InlineData(SessionSaveState.Duplicate)]
    [InlineData(SessionSaveState.Failed)]
    public async Task ImportAttemptsSaveAndRetainsAnalysisForEveryOutcome(SessionSaveState outcome)
    {
        var repository = new FakeSessions { Outcome = outcome };
        var vm = Create(repository, new EmptyHistory());
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("replay_obstacle_course_v1_1.json"));
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(outcome, vm.LastSaveResult!.State);
        Assert.NotNull(vm.ResultView); Assert.Equal(82.67, vm.ResultView.AssistantScore);
        Assert.True(vm.HasTrajectory); Assert.Contains(outcome.ToString(), vm.StorageMessage);
    }

    [Fact]
    public async Task UnexpectedSaveExceptionStillShowsCurrentResult()
    {
        var vm = Create(new FakeSessions { Throws = true }, new EmptyHistory());
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("valid_target_mission.json"));
        Assert.Equal(SessionSaveState.Failed, vm.LastSaveResult!.State);
        Assert.Contains("could not be saved", vm.StorageMessage); Assert.DoesNotContain("Private", vm.StorageMessage);
        Assert.Equal(90, vm.AnalysisResult!.FinalScore); Assert.True(vm.HasResult);
    }

    [Fact]
    public async Task InitializationFailureDisablesHistoryButKeepsImportAndAnalysisAvailable()
    {
        var sessions = new FakeSessions();
        var vm = Create(sessions, new EmptyHistory(), new FailedInitializer());
        await vm.InitializePersistenceAsync();
        Assert.False(vm.PersistenceAvailable); Assert.False(vm.NavigateHistoryCommand.CanExecute(null));
        Assert.Contains("storage unavailable", vm.StorageMessage);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("valid_obstacle_course.json"));
        Assert.Equal(0, sessions.SaveCalls); Assert.True(vm.HasResult);
        Assert.Equal(82.67, vm.AnalysisResult!.FinalScore); Assert.Contains("could not be saved", vm.StorageMessage);
    }

    [Fact]
    public async Task HistoryReopensStoredResultWithoutAnalyzerAndSupportsBothReplayViews()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var saved = PersistenceTestDatabase.Snapshot(); await db.Sessions.SaveAsync(saved);
        db.Execute("UPDATE evaluation_results SET score=37,explanation='Persisted historical explanation' WHERE criterion_id='CheckpointAccuracy'");
        var windows = new RecordingMock();
        var vm = new MainViewModel(new NoDialog(), new SessionImportService(), new ExerciseProfileProvider(), new ForbiddenAnalyzer(),
            new MockSimulatorReplayLauncher(windows), db.Sessions, db.History);
        Assert.True(vm.NavigateHistoryCommand.CanExecute(null));
        await vm.NavigateHistoryCommand.ExecuteAsync(); Assert.True(vm.IsHistoryScreen);
        Assert.Single(vm.HistoryView!.Sessions);
        await vm.HistoryView.OpenResultCommand.ExecuteAsync();
        Assert.Same(vm.ResultView, vm.CurrentViewModel); Assert.Equal("Historical session", vm.ResultView!.Origin);
        Assert.Equal(saved.Analysis.FinalScore, vm.ResultView.AssistantScore);
        Assert.Contains(vm.AnalysisResult!.CriterionResults, r => r.Score == 37 && r.Explanation == "Persisted historical explanation");
        Assert.Null(vm.SourceFilePath); Assert.True(vm.HasTrajectory);
        vm.NavigateTrajectoryCommand.Execute(null); Assert.Same(vm.TrajectoryView, vm.CurrentViewModel);
        vm.Open3DReplayCommand.Execute(null); Assert.Equal(1, windows.Calls);
        Assert.True(vm.ReplayCapability!.CanPreviewReplay); Assert.False(vm.ReplayCapability.CanLaunchExternalSimulator);
    }

    [Fact]
    public async Task DuplicateImportWarnsAndHistoryRefreshesAfterSuccessfulSave()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var vm = Create(db.Sessions, db.History);
        await vm.NavigateHistoryCommand.ExecuteAsync(); Assert.Empty(vm.HistoryView!.Sessions);
        var file = ReplayTestFiles.FixturePath("valid_obstacle_course.json");
        await vm.ImportFileAsync(file); Assert.Equal(SessionSaveState.Saved, vm.LastSaveResult!.State);
        await vm.NavigateHistoryCommand.ExecuteAsync(); Assert.Single(vm.HistoryView.Sessions);
        await vm.ImportFileAsync(file); Assert.Equal(SessionSaveState.Duplicate, vm.LastSaveResult!.State);
        Assert.Contains("not saved again", vm.StorageMessage); Assert.True(vm.HasResult);
        await vm.NavigateHistoryCommand.ExecuteAsync(); Assert.Single(vm.HistoryView.Sessions);
    }

    [Fact]
    public async Task HistoryReadFailureIsFriendlyAndLeavesViewUsable()
    {
        var history = new HistoryViewModel(new FailingHistory(), _ => Task.CompletedTask);
        await history.RefreshAsync();
        Assert.Equal("History could not be loaded.", history.Message); Assert.False(history.IsBusy);
        Assert.True(history.RefreshCommand.CanExecute(null));
    }

    private static MainViewModel Create(ISessionRepository sessions, IHistoryRepository history, IDatabaseInitializer? initializer = null) =>
        new(new NoDialog(), new SessionImportService(), new ExerciseProfileProvider(), new SessionAnalyzer(), null, sessions, history, initializer);
    private sealed class NoDialog : IFileDialogService { public string? ChooseSessionFile() => null; }
    private sealed class FakeSessions : ISessionRepository
    {
        public SessionSaveState Outcome { get; init; }
        public bool Throws { get; init; }
        public int SaveCalls { get; private set; }
        public Task<SessionSaveResult> SaveAsync(StoredSession session)
        { SaveCalls++; return Throws ? throw new InvalidOperationException("Private failure") : Task.FromResult(new SessionSaveResult(Outcome, Outcome.ToString())); }
        public Task<StoredSession?> LoadAsync(string id) => Task.FromResult<StoredSession?>(null);
    }
    private sealed class EmptyHistory : IHistoryRepository
    {
        public Task<IReadOnlyList<OperatorHistory>> ListOperatorsAsync() => Task.FromResult<IReadOnlyList<OperatorHistory>>([]);
        public Task<IReadOnlyList<SessionHistoryEntry>> ListSessionsAsync(string id, string? exercise) => Task.FromResult<IReadOnlyList<SessionHistoryEntry>>([]);
        public Task<bool> UpdateOperatorAsync(string id, string? display, string? notes) => Task.FromResult(false);
    }
    private sealed class FailedInitializer : IDatabaseInitializer
    { public Task<DatabaseInitializationResult> InitializeAsync() => Task.FromResult(new DatabaseInitializationResult(false, "History storage unavailable.")); }
    private sealed class ForbiddenAnalyzer : ISessionAnalyzer
    { public SessionAnalysisResult Analyze(TrainingSession s, ExerciseProfile p) => throw new InvalidOperationException("History must not analyze."); }
    private sealed class RecordingMock : IReplayMockWindowService { public int Calls { get; private set; } public void Show(ReplayLaunchRequest request) => Calls++; }
    private sealed class FailingHistory : IHistoryRepository
    {
        public Task<IReadOnlyList<OperatorHistory>> ListOperatorsAsync() => throw new InvalidOperationException("Private SQL failure");
        public Task<IReadOnlyList<SessionHistoryEntry>> ListSessionsAsync(string id, string? exercise) => throw new InvalidOperationException();
        public Task<bool> UpdateOperatorAsync(string id, string? display, string? notes) => throw new InvalidOperationException();
    }
}
