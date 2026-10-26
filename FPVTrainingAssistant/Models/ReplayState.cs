namespace FPVTrainingAssistant.Models;

public sealed record ReplayState(
    double TimeSec, double X, double Y, double Z, double SpeedMps, double AltitudeM,
    double PitchDeg, double RollDeg, double YawDeg,
    double? BatteryPercent, double? ControlLinkQualityPercent, double? VideoLinkQualityPercent)
{
    public double? VerticalSpeedMps { get; init; }
    public double? CameraTiltDeg { get; init; }
    public double? CameraFovDeg { get; init; }
    public double? BatteryVoltageV { get; init; }
    public double? CellVoltageV { get; init; }
    public double? CurrentA { get; init; }
    public double? ConsumedMah { get; init; }
    public double? ControlRssiDbm { get; init; }
    public bool? Failsafe { get; init; }
    public string? FlightMode { get; init; }
    public bool? Armed { get; init; }
}
