using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

// Finite visual context, independent of analytical route classification.
public sealed class RouteZoneVisualizationService
{
    private readonly int _subdivisions;
    private readonly double _minimumRedMarginM, _redMarginFactor;

    public RouteZoneVisualizationService(int subdivisions = 12, double minimumRedMarginM = 2, double redMarginFactor = 0.5)
    {
        if (subdivisions < 1) throw new ArgumentOutOfRangeException(nameof(subdivisions));
        if (!double.IsFinite(minimumRedMarginM) || minimumRedMarginM < 0) throw new ArgumentOutOfRangeException(nameof(minimumRedMarginM));
        if (!double.IsFinite(redMarginFactor) || redMarginFactor < 0) throw new ArgumentOutOfRangeException(nameof(redMarginFactor));
        _subdivisions = subdivisions; _minimumRedMarginM = minimumRedMarginM; _redMarginFactor = redMarginFactor;
    }

    public double RedMarginM(double yellowRadiusM) => Math.Max(_minimumRedMarginM, _redMarginFactor * yellowRadiusM);

    public static IReadOnlyList<Point2D> BoundsOf(IReadOnlyList<RouteZoneSegment> segments)
    {
        var bounds = new List<Point2D>();
        foreach (var segment in segments)
        {
            foreach (var point in new[] { segment.Start, segment.End })
            {
                bounds.Add(new(FiniteSum(point.X, -segment.RedRadiusM), FiniteSum(point.Y, -segment.RedRadiusM)));
                bounds.Add(new(FiniteSum(point.X, segment.RedRadiusM), FiniteSum(point.Y, segment.RedRadiusM)));
            }
        }
        return bounds.AsReadOnly();
    }

    public IReadOnlyList<RouteZoneSegment> Create(IReadOnlyList<ReferencePoint> referencePoints)
    {
        var route = referencePoints.OrderBy(p => p.SequenceNo).ToArray();
        var segments = new List<RouteZoneSegment>();
        for (var i = 1; i < route.Length; i++)
        {
            var a = route[i - 1]; var b = route[i];
            if (a.X == b.X && a.Y == b.Y) continue;
            for (var part = 0; part < _subdivisions; part++)
            {
                var start = (double)part / _subdivisions;
                var end = (double)(part + 1) / _subdivisions;
                var middle = (start + end) / 2;
                var green = Mix(a.GreenRadiusM, b.GreenRadiusM, middle);
                var yellow = Mix(a.YellowRadiusM, b.YellowRadiusM, middle);
                segments.Add(new(new(Mix(a.X, b.X, start), Mix(a.Y, b.Y, start)),
                    new(Mix(a.X, b.X, end), Mix(a.Y, b.Y, end)), green, yellow, yellow + RedMarginM(yellow)));
            }
        }
        return segments.AsReadOnly();
    }

    private static double Mix(double a, double b, double t) => (1 - t) * a + t * b;
    private static double FiniteSum(double a, double b) => double.IsFinite(a + b) ? a + b : Math.CopySign(double.MaxValue, b);
}
