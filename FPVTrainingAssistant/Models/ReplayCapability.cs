namespace FPVTrainingAssistant.Models;

public sealed record ReplayCapability(
    bool CanReplayDrone,
    bool HasEnvironment,
    bool HasDynamicEntityTracks,
    bool CanLaunchSimulatorReplay,
    IReadOnlyList<string> Limitations)
{
    public bool CanPreviewReplay { get; init; }
    public bool CanLaunchExternalSimulator => CanLaunchSimulatorReplay;
    public IReadOnlyList<string> PreviewLimitations { get; init; } = [];
}
