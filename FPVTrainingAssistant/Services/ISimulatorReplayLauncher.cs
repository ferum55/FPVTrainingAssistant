namespace FPVTrainingAssistant.Services;

public interface ISimulatorReplayLauncher
{
    string? SimulatorExecutablePath { get; }
    SimulatorReplayLaunchResult Launch(string? sourceFilePath);
}

public sealed record SimulatorReplayLaunchResult(bool IsSuccess, string Message);
