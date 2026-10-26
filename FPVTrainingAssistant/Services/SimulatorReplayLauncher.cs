using System.Diagnostics;
using System.IO;

namespace FPVTrainingAssistant.Services;

public sealed class SimulatorReplayLauncher : ISimulatorReplayLauncher
{
    public const string EnvironmentVariableName = "FPVTRAINING_SIMULATOR_EXE";
    private readonly IReplayProcessStarter _processStarter;
    private readonly Func<string?> _executablePathProvider;

    public SimulatorReplayLauncher(IReplayProcessStarter? processStarter = null, Func<string?>? executablePathProvider = null)
    {
        _processStarter = processStarter ?? new ReplayProcessStarter();
        _executablePathProvider = executablePathProvider ?? (() => Environment.GetEnvironmentVariable(EnvironmentVariableName));
    }

    public string? SimulatorExecutablePath => _executablePathProvider();

    public SimulatorReplayLaunchResult Launch(string? sourceFilePath)
    {
        try
        {
            var executable = SimulatorExecutablePath;
            if (string.IsNullOrWhiteSpace(executable)) return new(false, ReplayCapabilityService.MissingConfiguration);
            if (!File.Exists(executable)) return new(false, ReplayCapabilityService.MissingExecutable);
            if (!File.Exists(sourceFilePath)) return new(false, ReplayCapabilityService.MissingSourceFile);
            var executablePath = Path.GetFullPath(executable);
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-ReplayMode");
            startInfo.ArgumentList.Add("-ReplayFile=" + Path.GetFullPath(sourceFilePath!));
            return _processStarter.Start(startInfo)
                ? new(true, "Simulator started with the source session file. 3D replay requires simulator-side replay support.")
                : new(false, "The simulator could not be started. Check the executable configuration and try again.");
        }
        catch (Exception)
        {
            return new(false, "The simulator could not be started. Check the executable configuration and file access, then try again.");
        }
    }
}
