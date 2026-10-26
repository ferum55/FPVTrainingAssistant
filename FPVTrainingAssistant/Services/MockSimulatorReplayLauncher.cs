namespace FPVTrainingAssistant.Services;

public sealed class MockSimulatorReplayLauncher(IReplayMockWindowService? windows = null) : ISimulatorReplayLauncher
{
    public bool IsMock => true;
    public string? SimulatorExecutablePath => null;

    public SimulatorReplayLaunchResult Launch(string? sourceFilePath) =>
        new(false, "Load a Contract 1.1 session with replay context to open the replay prototype.");

    public SimulatorReplayLaunchResult Launch(ReplayLaunchRequest request)
    {
        var capability = new ReplayCapabilityService().Evaluate(request.Session, request.SourceFilePath, null);
        if (!capability.CanPreviewReplay) return new(false, capability.PreviewLimitations[0]);
        try
        {
            if (windows is null) return new(false, "The replay prototype window is unavailable.");
            windows.Show(request);
            return new(true, "Replay prototype initialized successfully. Mock integration preview; no external simulator was started.");
        }
        catch (Exception)
        {
            return new(false, "The replay prototype could not be opened. Try opening it again.");
        }
    }
}
