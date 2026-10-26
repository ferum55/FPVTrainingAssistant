using System.Diagnostics;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class SimulatorReplayTests : IDisposable
{
    private readonly ReplayTestFiles _files = new();
    public void Dispose() => _files.Dispose();

    [Fact]
    public void AnalyticalSessionRetainsDroneReplayButLacks3DEnvironment()
    {
        var session = ReplayTestFiles.Session(ReplayTestFiles.Document("valid_obstacle_course.json"));
        var result = new ReplayCapabilityService().Evaluate(session, _files.Create(), _files.Create(suffix: ".exe"));
        Assert.True(result.CanReplayDrone);
        Assert.False(result.HasEnvironment);
        Assert.False(result.HasDynamicEntityTracks);
        Assert.False(result.CanLaunchSimulatorReplay);
        Assert.Equal([ReplayCapabilityService.MissingEnvironment], result.Limitations);
    }

    [Fact]
    public void ReplayContextWithRecordedEntityIsCapableWhenLocalFilesExist()
    {
        var session = ReplayTestFiles.Session(ReplayTestFiles.Document());
        var result = new ReplayCapabilityService().Evaluate(session, _files.Create(), _files.Create(suffix: ".exe"));
        Assert.True(result.CanReplayDrone);
        Assert.True(result.HasEnvironment);
        Assert.True(result.HasDynamicEntityTracks);
        Assert.True(result.CanLaunchSimulatorReplay);
        Assert.Empty(result.Limitations);
    }

    [Fact]
    public void StaticCoursesDoNotRequireDynamicEntities()
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"] = new JsonArray();
        var result = new ReplayCapabilityService().Evaluate(ReplayTestFiles.Session(document), _files.Create(), _files.Create());
        Assert.False(result.HasDynamicEntityTracks);
        Assert.True(result.CanLaunchSimulatorReplay);
    }

    [Fact]
    public void MalformedTracksAreNotReportedAsAvailable()
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"]![0]!["samples"] = new JsonArray();
        var result = new ReplayCapabilityService().Evaluate(ReplayTestFiles.Session(document), null, null);
        Assert.False(result.HasDynamicEntityTracks);
    }

    [Fact]
    public void MissingEnvironmentAndTelemetryPreventLaunch()
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["environmentId"] = "";
        document["telemetry"] = new JsonArray();
        var result = new ReplayCapabilityService().Evaluate(ReplayTestFiles.Session(document), _files.Create(), _files.Create());
        Assert.False(result.HasEnvironment);
        Assert.False(result.CanReplayDrone);
        Assert.False(result.CanLaunchSimulatorReplay);
        Assert.Contains(ReplayCapabilityService.MissingEnvironment, result.Limitations);
    }

    [Fact]
    public void NullSessionHasClearLimitations()
    {
        var result = new ReplayCapabilityService().Evaluate(null, null, null);
        Assert.False(result.CanReplayDrone);
        Assert.False(result.HasEnvironment);
        Assert.False(result.CanLaunchSimulatorReplay);
        Assert.Contains(ReplayCapabilityService.MissingSourceFile, result.Limitations);
        Assert.Contains(ReplayCapabilityService.MissingConfiguration, result.Limitations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingConfigurationIsNonfatalAndNeverStartsProcess(string? executable)
    {
        var starter = new RecordingStarter();
        var result = new SimulatorReplayLauncher(starter, () => executable).Launch(_files.Create());
        Assert.False(result.IsSuccess);
        Assert.Equal(ReplayCapabilityService.MissingConfiguration, result.Message);
        Assert.Null(starter.StartInfo);
    }

    [Fact]
    public void MissingExecutableIsRejectedBeforeProcessLaunch()
    {
        var path = _files.Create(suffix: ".exe");
        File.Delete(path);
        var starter = new RecordingStarter();
        var result = new SimulatorReplayLauncher(starter, () => path).Launch(_files.Create());
        Assert.False(result.IsSuccess);
        Assert.Equal(ReplayCapabilityService.MissingExecutable, result.Message);
        Assert.Null(starter.StartInfo);
    }

    [Fact]
    public void DeletedSourceIsRejectedBeforeProcessLaunch()
    {
        var executable = _files.Create(suffix: ".exe");
        var source = _files.Create();
        File.Delete(source);
        var starter = new RecordingStarter();
        var result = new SimulatorReplayLauncher(starter, () => executable).Launch(source);
        Assert.False(result.IsSuccess);
        Assert.Equal(ReplayCapabilityService.MissingSourceFile, result.Message);
        Assert.Null(starter.StartInfo);
    }

    [Fact]
    public void ArgumentListPreservesPathsWithSpacesAndSourceContents()
    {
        var executable = _files.Create(suffix: ".exe");
        var source = _files.Create("{\"synthetic\":true}");
        var before = File.ReadAllBytes(source);
        var starter = new RecordingStarter();
        var result = new SimulatorReplayLauncher(starter, () => executable).Launch(source);
        Assert.True(result.IsSuccess);
        var info = Assert.IsType<ProcessStartInfo>(starter.StartInfo);
        Assert.Contains(' ', source);
        Assert.Contains(' ', executable);
        Assert.Equal(Path.GetFullPath(executable), info.FileName);
        Assert.Equal(Path.GetDirectoryName(executable), info.WorkingDirectory);
        Assert.False(info.UseShellExecute);
        Assert.Empty(info.Verb);
        Assert.Empty(info.Arguments);
        Assert.Equal(["-ReplayMode", "-ReplayFile=" + Path.GetFullPath(source)], info.ArgumentList);
        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void RelativeSourceIsNormalizedWithoutChangingWorkingDirectory()
    {
        var executable = _files.Create(suffix: ".exe");
        var source = _files.Create();
        var starter = new RecordingStarter();
        Assert.True(new SimulatorReplayLauncher(starter, () => executable).Launch(Path.GetRelativePath(Environment.CurrentDirectory, source)).IsSuccess);
        Assert.Equal("-ReplayFile=" + source, starter.StartInfo!.ArgumentList[1]);
    }

    [Fact]
    public void ConfigurationProviderIsReadAgainForEachLaunch()
    {
        string? executable = null;
        var starter = new RecordingStarter();
        var launcher = new SimulatorReplayLauncher(starter, () => executable);
        var source = _files.Create();
        Assert.False(launcher.Launch(source).IsSuccess);
        executable = _files.Create(suffix: ".exe");
        Assert.Equal(executable, launcher.SimulatorExecutablePath);
        Assert.True(launcher.Launch(source).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessFailureBecomesFriendlyResult(bool throws)
    {
        var executable = _files.Create(suffix: ".exe");
        var result = new SimulatorReplayLauncher(new FailingStarter(throws), () => executable).Launch(_files.Create());
        Assert.False(result.IsSuccess);
        Assert.Contains("could not be started", result.Message);
        Assert.DoesNotContain("Private error", result.Message);
    }

    private sealed class RecordingStarter : IReplayProcessStarter
    {
        public ProcessStartInfo? StartInfo { get; private set; }
        public bool Start(ProcessStartInfo startInfo) { StartInfo = startInfo; return true; }
    }

    private sealed class FailingStarter(bool throws) : IReplayProcessStarter
    {
        public bool Start(ProcessStartInfo startInfo) => throws ? throw new InvalidOperationException("Private error") : false;
    }
}
