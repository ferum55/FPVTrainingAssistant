using System.Text.Json.Nodes;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class MainViewModelTests
{
    [Theory]
    [InlineData("valid_obstacle_course.json", 82.67)]
    [InlineData("valid_target_mission.json", 90)]
    public async Task ValidWorkflowResolvesProfileAnalyzesAndNavigates(string file, double expected)
    {
        var analyzer = new CountingAnalyzer();
        var vm = Create(new FakeDialog(Fixture(file)), analyzer: analyzer);
        await vm.ImportCommand.ExecuteAsync();
        Assert.Equal(1, analyzer.CallCount);
        Assert.NotNull(vm.ImportedSession);
        Assert.NotNull(vm.Profile);
        Assert.Equal(vm.ImportedSession.ProfileId, vm.Profile.ProfileId);
        Assert.Equal(vm.ImportedSession.ProfileVersion, vm.Profile.Version);
        Assert.Equal(expected, vm.AnalysisResult!.FinalScore);
        Assert.Same(vm.ResultView, vm.CurrentViewModel);
        Assert.Equal("Session result", vm.ScreenTitle);
        Assert.Equal(4, vm.ResultView!.Criteria.Count);
        Assert.False(vm.IsBusy);
        Assert.True(vm.NavigateResultCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("invalid_missing_session_id.json", "RequiredField")]
    [InlineData("invalid_bad_telemetry_time.json", "TelemetryTimeOrder")]
    [InlineData("invalid_out_of_range_percentage.json", "PercentageOutOfRange")]
    public async Task InvalidImportExposesIssuesAndNeverInvokesAnalysis(string file, string code)
    {
        var analyzer = new CountingAnalyzer();
        var vm = Create(analyzer: analyzer);
        await vm.ImportFileAsync(Fixture(file));
        Assert.Equal(0, analyzer.CallCount);
        Assert.True(vm.ImportView.HasFailure);
        Assert.Contains(vm.ImportView.Issues, issue => issue.Code == code && issue.Severity == ValidationSeverity.Error);
        Assert.Same(vm.ImportView, vm.CurrentViewModel);
        Assert.Null(vm.AnalysisResult);
        Assert.Null(vm.ResultView);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task IncompatibleProfileShowsFailureWithoutMutatingImportedIds()
    {
        var analyzer = new CountingAnalyzer();
        var vm = Create(profiles: new FakeProfiles(_ => null), analyzer: analyzer);
        await vm.ImportFileAsync(Fixture("valid_obstacle_course.json"));
        Assert.True(vm.ImportView.HasFailure);
        Assert.Contains("No compatible exercise profile", vm.ImportView.FailureMessage);
        Assert.Equal("synthetic-obstacle-course", vm.ImportedSession!.ProfileId);
        Assert.Equal("synthetic-obstacle-001", vm.ImportedSession.SessionId);
        Assert.Equal(0, analyzer.CallCount);
        Assert.False(vm.NavigateResultCommand.CanExecute(null));
    }

    [Fact]
    public async Task AnalysisConfigurationErrorIsRecoverable()
    {
        var vm = Create(profiles: new FakeProfiles(_ => AnalysisTestData.Profile(ExerciseTypes.ObstacleCourse, [], id: "synthetic-obstacle-course")));
        await vm.ImportFileAsync(Fixture("valid_obstacle_course.json"));
        Assert.True(vm.ImportView.HasFailure);
        Assert.Equal("Analysis configuration error", vm.ImportView.FailureHeading);
        Assert.False(vm.IsBusy);
        Assert.True(vm.ImportCommand.CanExecute(null));
        Assert.Null(vm.AnalysisResult);
    }

    [Fact]
    public async Task WarningsRemainAvailableOnSuccessfulResult()
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(Fixture("valid_target_mission.json")))!;
        document["events"] = new JsonArray();
        await WithDocument(document.ToJsonString(), async path =>
        {
            var vm = Create();
            await vm.ImportFileAsync(path);
            Assert.NotNull(vm.ResultView);
            Assert.True(vm.ResultView.HasWarnings);
            Assert.Contains(vm.ValidationWarnings, issue => issue.Code == "NoTargetEvents");
            Assert.Contains(vm.ResultView.Warnings, issue => issue.Code == "NoTargetEvents");
            Assert.False(vm.ImportView.HasFailure);
        });
    }

    [Fact]
    public async Task SimulatorAndAssistantScoresAreMappedIndependently()
    {
        var vm = Create();
        await vm.ImportFileAsync(Fixture("valid_obstacle_course.json"));
        Assert.Equal("82.67", vm.ResultView!.AssistantScoreText);
        Assert.Equal("750.00", vm.ResultView.SimulatorScoreText);
        Assert.Null(vm.ImportedSession!.AssistantFinalScore);
    }

    [Fact]
    public void NoSessionPreventsResultNavigation()
    {
        var vm = Create();
        Assert.False(vm.NavigateResultCommand.CanExecute(null));
        vm.NavigateResultCommand.Execute(null);
        Assert.Same(vm.ImportView, vm.CurrentViewModel);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task NavigationKeepsCurrentResultWithoutCreatingHistory()
    {
        var vm = Create();
        await vm.ImportFileAsync(Fixture("valid_target_mission.json"));
        var result = vm.ResultView;
        vm.NavigateImportCommand.Execute(null);
        Assert.Same(vm.ImportView, vm.CurrentViewModel);
        vm.NavigateResultCommand.Execute(null);
        Assert.Same(result, vm.CurrentViewModel);
    }

    [Fact]
    public async Task CancelledPickerKeepsExistingResult()
    {
        var vm = Create(new FakeDialog(null));
        await vm.ImportFileAsync(Fixture("valid_target_mission.json"));
        var result = vm.ResultView;
        await vm.ImportCommand.ExecuteAsync();
        Assert.Same(result, vm.CurrentViewModel);
        Assert.Same(result, vm.ResultView);
    }

    [Fact]
    public async Task FailedNewImportClearsOldResultAndCanRecover()
    {
        var vm = Create();
        await vm.ImportFileAsync(Fixture("valid_target_mission.json"));
        await vm.ImportFileAsync(Fixture("invalid_missing_session_id.json"));
        Assert.Null(vm.ResultView);
        Assert.False(vm.NavigateResultCommand.CanExecute(null));
        await vm.ImportFileAsync(Fixture("valid_obstacle_course.json"));
        Assert.NotNull(vm.ResultView);
        Assert.False(vm.ImportView.HasFailure);
    }

    [Fact]
    public async Task BusyWorkflowPreventsOverlappingImportsAndNavigation()
    {
        var gate = new TaskCompletionSource<SessionImportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var importer = new FakeImporter(_ => { calls++; return gate.Task; });
        var vm = Create(new FakeDialog("session.json"), importer);
        var running = vm.ImportCommand.ExecuteAsync();
        Assert.True(vm.IsBusy);
        Assert.True(vm.ImportView.IsBusy);
        Assert.False(vm.ImportCommand.CanExecute(null));
        Assert.False(vm.NavigateImportCommand.CanExecute(null));
        await vm.ImportCommand.ExecuteAsync();
        await vm.ImportFileAsync("another.json");
        Assert.Equal(1, calls);
        gate.SetResult(new(null, new ValidationResult([new("SyntheticError", "$", "Synthetic failure.")])));
        await running;
        Assert.False(vm.IsBusy);
        Assert.False(vm.ImportCommand.IsRunning);
    }

    [Fact]
    public async Task MalformedJsonShowsRecoverableValidationState()
    {
        await WithDocument("{", async path =>
        {
            var vm = Create();
            await vm.ImportFileAsync(path);
            Assert.True(vm.ImportView.HasFailure);
            Assert.Contains(vm.ImportView.Issues, i => i.Code == "InvalidJson");
            Assert.True(vm.ImportCommand.CanExecute(null));
        });
    }

    [Fact]
    public async Task TechnicalFailureUsesFriendlyTextWithoutStackTrace()
    {
        var vm = Create(importer: new FakeImporter(_ => Task.FromResult(new SessionImportResult(null, new ValidationResult([]), "Private implementation detail\n at HiddenMethod()"))));
        await vm.ImportFileAsync("synthetic.json");
        Assert.True(vm.ImportView.HasTechnicalError);
        Assert.DoesNotContain("HiddenMethod", vm.ImportView.TechnicalError!);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task UnexpectedServiceFailureDoesNotEscapeWorkflow()
    {
        var vm = Create(importer: new FakeImporter(_ => throw new InvalidOperationException("Private error")));
        await vm.ImportFileAsync("synthetic.json");
        Assert.True(vm.ImportView.HasFailure);
        Assert.False(vm.IsBusy);
        Assert.DoesNotContain("Private error", vm.ImportView.FailureMessage);
    }

    [Fact]
    public async Task PickerFailureDoesNotEscapeICommandBoundary()
    {
        var vm = Create(new ThrowingDialog());
        await vm.ImportCommand.ExecuteAsync();
        Assert.True(vm.ImportView.HasFailure);
        Assert.NotNull(vm.ImportCommand.LastError);
        Assert.False(vm.ImportCommand.IsRunning);
    }

    private static MainViewModel Create(IFileDialogService? dialog = null, ISessionImportService? importer = null,
        IExerciseProfileProvider? profiles = null, ISessionAnalyzer? analyzer = null) =>
        new(dialog ?? new FakeDialog(null), importer ?? new SessionImportService(), profiles ?? new ExerciseProfileProvider(), analyzer ?? new SessionAnalyzer());

    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "TestSessions", file);
    private static async Task WithDocument(string text, Func<string, Task> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "FPVTrainingAssistant-vm-" + Guid.NewGuid() + ".json");
        try { await File.WriteAllTextAsync(path, text); await action(path); }
        finally { File.Delete(path); }
    }

    private sealed class FakeDialog(string? path) : IFileDialogService { public string? ChooseSessionFile() => path; }
    private sealed class ThrowingDialog : IFileDialogService { public string? ChooseSessionFile() => throw new InvalidOperationException(); }
    private sealed class FakeImporter(Func<string, Task<SessionImportResult>> import) : ISessionImportService
    {
        public Task<SessionImportResult> ImportAsync(string path, CancellationToken cancellationToken = default) => import(path);
    }
    private sealed class FakeProfiles(Func<TrainingSession, ExerciseProfile?> resolve) : IExerciseProfileProvider
    {
        public ExerciseProfile? Resolve(TrainingSession session) => resolve(session);
    }
    private sealed class CountingAnalyzer : ISessionAnalyzer
    {
        public int CallCount { get; private set; }
        public SessionAnalysisResult Analyze(TrainingSession session, ExerciseProfile profile)
        {
            CallCount++;
            return new SessionAnalyzer().Analyze(session, profile);
        }
    }
}
