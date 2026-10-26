using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class TelemetryReplayService
{
    private readonly TelemetrySample[] _samples;
    public int Count => _samples.Length;
    public double StartTimeSec => Count == 0 ? 0 : _samples[0].TimeSec;
    public double EndTimeSec => Count == 0 ? 0 : _samples[^1].TimeSec;

    public TelemetryReplayService(IReadOnlyList<TelemetrySample> telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        _samples = telemetry.ToArray();
        for (var i = 0; i < Count; i++)
        {
            var s = _samples[i];
            if (s is null || !double.IsFinite(s.TimeSec) || s.TimeSec < 0 ||
                (i > 0 && s.TimeSec <= _samples[i - 1].TimeSec) ||
                new[] { s.X, s.Y, s.Z, s.SpeedMps, s.AltitudeM, s.PitchDeg, s.RollDeg, s.YawDeg }.Any(v => !double.IsFinite(v)))
            {
                throw new ArgumentException("Replay requires finite telemetry ordered by strictly increasing nonnegative time.", nameof(telemetry));
            }
        }
    }

    public ReplayState? GetStateAtTime(double timeSec)
    {
        if (double.IsNaN(timeSec)) throw new ArgumentOutOfRangeException(nameof(timeSec));
        if (Count == 0) return null;
        var time = Math.Clamp(timeSec, StartTimeSec, EndTimeSec);
        var next = LowerBound(time);
        if (next == 0 || _samples[next].TimeSec == time) return State(_samples[next]);
        var a = _samples[next - 1];
        var b = _samples[next];
        var f = (time - a.TimeSec) / (b.TimeSec - a.TimeSec);
        return new(time, Lerp(a.X, b.X, f), Lerp(a.Y, b.Y, f), Lerp(a.Z, b.Z, f),
            Lerp(a.SpeedMps, b.SpeedMps, f), Lerp(a.AltitudeM, b.AltitudeM, f),
            Angle(a.PitchDeg, b.PitchDeg, f), Angle(a.RollDeg, b.RollDeg, f), Angle(a.YawDeg, b.YawDeg, f),
            Optional(a.BatteryPercent, b.BatteryPercent, f), Optional(a.ControlLinkQualityPercent, b.ControlLinkQualityPercent, f),
            Optional(a.VideoLinkQualityPercent, b.VideoLinkQualityPercent, f));
    }

    public double NextSampleTime(double timeSec)
    {
        if (Count == 0) return 0;
        var index = LowerBound(Math.Clamp(timeSec, StartTimeSec, EndTimeSec));
        if (_samples[index].TimeSec <= timeSec) index = Math.Min(index + 1, Count - 1);
        return _samples[index].TimeSec;
    }

    public double PreviousSampleTime(double timeSec)
    {
        if (Count == 0) return 0;
        var index = LowerBound(Math.Clamp(timeSec, StartTimeSec, EndTimeSec));
        return _samples[Math.Max(0, index - 1)].TimeSec;
    }

    private int LowerBound(double time)
    {
        var low = 0;
        var high = Count - 1;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_samples[middle].TimeSec < time) low = middle + 1; else high = middle;
        }
        return low;
    }

    private static ReplayState State(TelemetrySample s) => new(s.TimeSec, s.X, s.Y, s.Z, s.SpeedMps, s.AltitudeM,
        Normalize(s.PitchDeg), Normalize(s.RollDeg), Normalize(s.YawDeg), s.BatteryPercent, s.ControlLinkQualityPercent, s.VideoLinkQualityPercent);
    private static double Lerp(double a, double b, double f) => (1 - f) * a + f * b;
    private static double? Optional(double? a, double? b, double f) => a.HasValue && b.HasValue ? Lerp(a.Value, b.Value, f) : null;
    private static double Angle(double a, double b, double f) => Normalize(Normalize(a) + Normalize(Normalize(b) - Normalize(a)) * f);
    private static double Normalize(double angle)
    {
        var wrapped = angle % 360;
        return wrapped < -180 ? wrapped + 360 : wrapped >= 180 ? wrapped - 360 : wrapped;
    }
}
