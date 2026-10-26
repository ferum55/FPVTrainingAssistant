namespace FPVTrainingAssistant.Models;

public sealed class ReferencePoint
{
    // Set both radii together so their invariant does not depend on property assignment order.
    public ReferencePoint(double greenRadiusM, double yellowRadiusM)
    {
        if (!double.IsFinite(greenRadiusM) || greenRadiusM < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(greenRadiusM), "Green radius must be finite and nonnegative.");
        }

        if (!double.IsFinite(yellowRadiusM) || yellowRadiusM < greenRadiusM)
        {
            throw new ArgumentOutOfRangeException(nameof(yellowRadiusM), "Yellow radius must be finite and at least the green radius.");
        }

        GreenRadiusM = greenRadiusM;
        YellowRadiusM = yellowRadiusM;
    }

    public required int SequenceNo { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Z { get; init; }
    public required string PointType { get; init; }
    public double GreenRadiusM { get; }
    public double YellowRadiusM { get; }
}
