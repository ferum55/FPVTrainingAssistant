using System.Text.Json.Nodes;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class MockReplayLauncherTests
{
    [Theory]
    [InlineData("replay_obstacle_course_v1_1.json")]
    [InlineData("replay_target_mission_v1_1.json")]
    public void MockAcceptsActualLoadedSessionWithoutExecutableOrSourceFile(string file)
    {
        var request = MockReplayTestData.Request(file) with { SourceFilePath = null };
        var host = new RecordingWindowService();
        ISimulatorReplayLauncher launcher = new MockSimulatorReplayLauncher(host);
        Assert.True(launcher.IsMock);
        Assert.Null(launcher.SimulatorExecutablePath);
        var result = launcher.Launch(request);
        Assert.True(result.IsSuccess);
        Assert.Same(request, Assert.Single(host.Requests));
        Assert.Contains("prototype", result.Message);
        Assert.DoesNotContain("Simulator started", result.Message);
        var capability = new ReplayCapabilityService().Evaluate(request.Session, null, null);
        Assert.True(capability.CanPreviewReplay);
        Assert.Empty(capability.PreviewLimitations);
        Assert.False(capability.CanLaunchExternalSimulator);
    }

    [Fact]
    public void Contract10DoesNotOpenAWindowAndExplainsRequiredContext()
    {
        var host = new RecordingWindowService();
        var result = new MockSimulatorReplayLauncher(host).Launch(MockReplayTestData.Request("valid_obstacle_course.json"));
        Assert.False(result.IsSuccess);
        Assert.Contains("Contract 1.1 replay context", result.Message);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public void MissingEnvironmentPreventsPreview()
    {
        var request = MockReplayTestData.Request(edit: document => document["replay"]!["environmentId"] = "");
        var host = new RecordingWindowService();
        var result = new MockSimulatorReplayLauncher(host).Launch(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ReplayCapabilityService.MissingEnvironment, result.Message);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public void InsufficientDroneSamplesPreventsPreview()
    {
        var request = MockReplayTestData.Request("replay_target_mission_v1_1.json", document =>
            document["telemetry"] = new JsonArray(document["telemetry"]![0]!.DeepClone()));
        var host = new RecordingWindowService();
        var result = new MockSimulatorReplayLauncher(host).Launch(request);
        Assert.False(result.IsSuccess);
        Assert.Contains("two telemetry samples", result.Message);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public void MissingLoadedSessionHasFriendlyResult()
    {
        Assert.False(new MockSimulatorReplayLauncher().Launch((string?)null).IsSuccess);
        var capability = new ReplayCapabilityService().Evaluate(null, null, null);
        Assert.False(capability.CanPreviewReplay);
        Assert.NotEmpty(capability.PreviewLimitations);
    }

    [Fact]
    public void WindowCreationFailureIsRecoverable()
    {
        var result = new MockSimulatorReplayLauncher(new ThrowingWindowService()).Launch(MockReplayTestData.Request());
        Assert.False(result.IsSuccess);
        Assert.Contains("prototype could not be opened", result.Message);
        Assert.DoesNotContain("Private", result.Message);
    }

    [Fact]
    public async Task MainWorkflowUsesMockWithoutExecutableAndPreservesLocalTrajectoryState()
    {
        var host = new RecordingWindowService();
        var vm = new MainViewModel(new NoDialog(), new SessionImportService(), new ExerciseProfileProvider(), new SessionAnalyzer(), new MockSimulatorReplayLauncher(host));
        Assert.False(vm.Open3DReplayCommand.CanExecute(null));
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("replay_obstacle_course_v1_1.json"));
        vm.TrajectoryView!.CurrentTimeSec = 3.25;
        vm.Open3DReplayCommand.Execute(null);
        Assert.Same(vm.ImportedSession, Assert.Single(host.Requests).Session);
        Assert.Equal(vm.SourceFilePath, host.Requests[0].SourceFilePath);
        Assert.Equal(3.25, vm.TrajectoryView.CurrentTimeSec);
        Assert.True(vm.ReplayCapability!.CanPreviewReplay);
        Assert.Contains("prototype initialized", vm.ReplayLaunchMessage);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("valid_obstacle_course.json"));
        vm.Open3DReplayCommand.Execute(null);
        Assert.Contains("Contract 1.1 replay context", vm.ReplayLaunchMessage);
        Assert.Single(host.Requests);
        Assert.True(vm.HasTrajectory);
        Assert.Equal(82.67, vm.AnalysisResult!.FinalScore);
    }

    private sealed class RecordingWindowService : IReplayMockWindowService
    {
        public List<ReplayLaunchRequest> Requests { get; } = [];
        public void Show(ReplayLaunchRequest request) => Requests.Add(request);
    }
    private sealed class ThrowingWindowService : IReplayMockWindowService
    {
        public void Show(ReplayLaunchRequest request) => throw new InvalidOperationException("Private detail");
    }
    private sealed class NoDialog : IFileDialogService { public string? ChooseSessionFile() => null; }
}
