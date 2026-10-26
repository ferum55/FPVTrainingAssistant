namespace FPVTrainingAssistant.Services;

public interface ISimulatorReplayLauncher
{
    string? SimulatorExecutablePath { get; }
    bool IsMock => false;
    SimulatorReplayLaunchResult Launch(string? sourceFilePath);
    SimulatorReplayLaunchResult Launch(ReplayLaunchRequest request) => Launch(request.SourceFilePath);
}

public sealed record SimulatorReplayLaunchResult(bool IsSuccess, string Message);
