using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class SimulatorReplayViewModelTests : IDisposable
{
    private readonly ReplayTestFiles _files = new();
    public void Dispose() => _files.Dispose();

    [Fact]
    public void NoLoadedSessionDisablesCommand()
    {
        var launcher = new FakeLauncher(null);
        var vm = Create(launcher);
        Assert.False(vm.Open3DReplayCommand.CanExecute(null));
        vm.Open3DReplayCommand.Execute(null);
        Assert.Null(vm.ReplayCapability);
        Assert.False(vm.HasReplayLaunchMessage);
        Assert.Equal(0, launcher.Calls);
    }

    [Fact]
    public async Task Version10ExplainsMissingContextAndRetainsLocalTrajectory()
    {
        var launcher = new FakeLauncher(null);
        var vm = Create(launcher);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("valid_obstacle_course.json"));
        Assert.True(vm.Open3DReplayCommand.CanExecute(null));
        vm.ResultView!.Open3DReplayCommand.Execute(null);
        Assert.Equal(ReplayCapabilityService.MissingEnvironment, vm.ReplayLaunchMessage);
        Assert.True(vm.HasReplayLaunchMessage);
        Assert.True(vm.ReplayCapability!.CanReplayDrone);
        Assert.False(vm.ReplayCapability.CanLaunchSimulatorReplay);
        Assert.Equal(0, launcher.Calls);
        vm.ResultView.ViewTrajectoryCommand.Execute(null);
        Assert.Same(vm.TrajectoryView, vm.CurrentViewModel);
        Assert.True(vm.TrajectoryView!.CanReplay);
    }

    [Fact]
    public async Task MissingSimulatorConfigurationDoesNotDisruptAnalysisOrReplay()
    {
        var launcher = new FakeLauncher(null);
        var vm = Create(launcher);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("replay_target_mission_v1_1.json"));
        Assert.True(vm.Open3DReplayCommand.CanExecute(null));
        vm.Open3DReplayCommand.Execute(null);
        Assert.Equal(ReplayCapabilityService.MissingConfiguration, vm.ReplayLaunchMessage);
        Assert.False(vm.ImportView.HasFailure);
        Assert.Equal(90, vm.AnalysisResult!.FinalScore);
        Assert.True(vm.HasTrajectory);
        Assert.Equal(0, launcher.Calls);
    }

    [Fact]
    public async Task ReplayCapableSessionPassesOriginalAbsoluteSourceToLauncher()
    {
        var launcher = new FakeLauncher(_files.Create(suffix: ".exe"));
        var vm = Create(launcher);
        var path = ReplayTestFiles.FixturePath("replay_obstacle_course_v1_1.json");
        await vm.ImportFileAsync(path);
        Assert.True(vm.ReplayCapability!.CanLaunchSimulatorReplay);
        Assert.Same(vm.Open3DReplayCommand, vm.ResultView!.Open3DReplayCommand);
        vm.Open3DReplayCommand.Execute(null);
        Assert.Equal(1, launcher.Calls);
        Assert.Equal(Path.GetFullPath(path), launcher.Source);
        Assert.Equal(Path.GetFullPath(path), vm.SourceFilePath);
        Assert.Equal("Synthetic launch result", vm.ReplayLaunchMessage);
        Assert.Same(vm.ResultView, vm.CurrentViewModel);
    }

    [Fact]
    public async Task FileDeletedAfterImportIsRecheckedAtClick()
    {
        var launcher = new FakeLauncher(_files.Create(suffix: ".exe"));
        var vm = Create(launcher);
        var source = _files.Create(ReplayTestFiles.Document().ToJsonString());
        await vm.ImportFileAsync(source);
        Assert.True(vm.ReplayCapability!.CanLaunchSimulatorReplay);
        File.Delete(source);
        vm.Open3DReplayCommand.Execute(null);
        Assert.Equal(ReplayCapabilityService.MissingSourceFile, vm.ReplayLaunchMessage);
        Assert.False(vm.ReplayCapability.CanLaunchSimulatorReplay);
        Assert.True(vm.HasTrajectory);
        Assert.Equal(0, launcher.Calls);
    }

    [Fact]
    public async Task ImportingAnotherSessionRefreshesPathCapabilityAndMessages()
    {
        var launcher = new FakeLauncher(_files.Create(suffix: ".exe"));
        var vm = Create(launcher);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("replay_obstacle_course_v1_1.json"));
        vm.Open3DReplayCommand.Execute(null);
        Assert.True(vm.ReplayCapability!.HasDynamicEntityTracks);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("valid_target_mission.json"));
        Assert.False(vm.HasReplayLaunchMessage);
        Assert.False(vm.ReplayCapability!.HasEnvironment);
        Assert.False(vm.ReplayCapability.HasDynamicEntityTracks);
        Assert.Equal(ReplayTestFiles.FixturePath("valid_target_mission.json"), vm.SourceFilePath);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("invalid_missing_session_id.json"));
        Assert.Null(vm.SourceFilePath);
        Assert.Null(vm.ReplayCapability);
        Assert.False(vm.Open3DReplayCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnexpectedLauncherFailureIsNonfatal()
    {
        var launcher = new FakeLauncher(_files.Create(suffix: ".exe")) { ThrowOnLaunch = true };
        var vm = Create(launcher);
        await vm.ImportFileAsync(ReplayTestFiles.FixturePath("replay_obstacle_course_v1_1.json"));
        vm.Open3DReplayCommand.Execute(null);
        Assert.Contains("could not be started", vm.ReplayLaunchMessage);
        Assert.DoesNotContain("Private error", vm.ReplayLaunchMessage);
        Assert.True(vm.HasResult);
        Assert.True(vm.HasTrajectory);
    }

    private static MainViewModel Create(FakeLauncher launcher) =>
        new(new FakeDialog(), new SessionImportService(), new ExerciseProfileProvider(), new SessionAnalyzer(), launcher);
    private sealed class FakeDialog : IFileDialogService { public string? ChooseSessionFile() => null; }
    private sealed class FakeLauncher(string? executable) : ISimulatorReplayLauncher
    {
        public string? SimulatorExecutablePath => executable;
        public int Calls { get; private set; }
        public string? Source { get; private set; }
        public bool ThrowOnLaunch { get; init; }
        public SimulatorReplayLaunchResult Launch(string? sourceFilePath)
        {
            Calls++;
            Source = sourceFilePath;
            return ThrowOnLaunch ? throw new InvalidOperationException("Private error") : new(true, "Synthetic launch result");
        }
    }
}
