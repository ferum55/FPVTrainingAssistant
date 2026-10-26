namespace FPVTrainingAssistant.Models;

/// <summary>Recorded transform in the same units, frame and elapsed time as drone telemetry.</summary>
public sealed class ReplayEntitySample
{
    public required double TimeSec { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double PitchDeg { get; init; }
    public required double RollDeg { get; init; }
    public required double YawDeg { get; init; }
    public bool? IsActive { get; init; }
    public string? State { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
