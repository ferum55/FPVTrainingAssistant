namespace FPVTrainingAssistant.Models;

public sealed record ReplayCapability(
    bool CanReplayDrone,
    bool HasEnvironment,
    bool HasDynamicEntityTracks,
    bool CanLaunchSimulatorReplay,
    IReadOnlyList<string> Limitations);
