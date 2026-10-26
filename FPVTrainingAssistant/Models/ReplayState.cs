namespace FPVTrainingAssistant.Models;

public sealed record ReplayState(
    double TimeSec, double X, double Y, double Z, double SpeedMps, double AltitudeM,
    double PitchDeg, double RollDeg, double YawDeg,
    double? BatteryPercent, double? ControlLinkQualityPercent, double? VideoLinkQualityPercent);
