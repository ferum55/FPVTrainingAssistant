namespace FPVTrainingAssistant.Services;

// Shared by drone and entity replay; preserves the Stage 5 interpolation convention.
internal static class ReplayInterpolation
{
    public static double Lerp(double a, double b, double f) => (1 - f) * a + f * b;
    public static double? Optional(double? a, double? b, double f) => a.HasValue && b.HasValue ? Lerp(a.Value, b.Value, f) : null;
    public static double? OptionalAngle(double? a, double? b, double f) => a.HasValue && b.HasValue ? Angle(a.Value, b.Value, f) : null;
    public static double Angle(double a, double b, double f) => Normalize(Normalize(a) + Normalize(Normalize(b) - Normalize(a)) * f);
    public static double Normalize(double angle)
    {
        var wrapped = angle % 360;
        return wrapped < -180 ? wrapped + 360 : wrapped >= 180 ? wrapped - 360 : wrapped;
    }
}
