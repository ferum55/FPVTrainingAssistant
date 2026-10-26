namespace FPVTrainingAssistant.Models;

/// <summary>
/// A sample at TimeSec seconds from session start. Positions/altitude use meters,
/// speed uses meters per second, and attitude uses degrees. No scoring is applied here.
/// </summary>
public sealed class TelemetrySample
{
    public required double TimeSec { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required double SpeedMps { get; init; }
    public required double AltitudeM { get; init; }
    public required double PitchDeg { get; init; }
    public required double RollDeg { get; init; }
    public required double YawDeg { get; init; }
    public double? BatteryPercent { get; init; }
    public double? ControlLinkQualityPercent { get; init; }
    public double? VideoLinkQualityPercent { get; init; }
}
