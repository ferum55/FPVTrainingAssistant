using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class RouteAnalyzer
{
    public RouteAnalysisResult Analyze(IReadOnlyList<TelemetrySample> telemetry, IReadOnlyList<ReferencePoint> referencePoints)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var points = ValidateRoute(referencePoints);
        if (telemetry.Count < 2)
        {
            throw new ArgumentException("Route analysis requires at least two telemetry samples.", nameof(telemetry));
        }

        double green = 0, yellow = 0, red = 0, total = 0, average = 0, maximum = 0;
        for (var i = 0; i < telemetry.Count; i++)
        {
            var sample = telemetry[i];
            if (sample is null || !double.IsFinite(sample.TimeSec) || sample.TimeSec < 0 ||
                !double.IsFinite(sample.X) || !double.IsFinite(sample.Y))
            {
                throw new ArgumentException("Route telemetry must contain finite X/Y positions and nonnegative times.", nameof(telemetry));
            }
            if (i == 0) continue;
            var previous = telemetry[i - 1];
            var deltaTime = sample.TimeSec - previous.TimeSec;
            if (!double.IsFinite(deltaTime) || deltaTime <= 0)
            {
                throw new ArgumentException("Route telemetry times must be strictly increasing with positive evaluated duration.", nameof(telemetry));
            }

            // Dividing before adding also avoids overflow for large finite positions.
            var midX = previous.X / 2 + sample.X / 2;
            var midY = previous.Y / 2 + sample.Y / 2;
            var nearest = Nearest(midX, midY, points);
            if (nearest.Distance <= nearest.Green) green += deltaTime;
            else if (nearest.Distance <= nearest.Yellow) yellow += deltaTime;
            else red += deltaTime;
            total += deltaTime;
            if (!double.IsFinite(total)) throw new ArgumentException("Evaluated route duration is not finite.", nameof(telemetry));
            // Incremental time-weighted mean avoids distance * duration overflow.
            average += (nearest.Distance - average) * (deltaTime / total);
            maximum = Math.Max(maximum, nearest.Distance);
        }

        return new(green / total * 100, yellow / total * 100, red / total * 100, average, maximum, total);
    }

    internal static ReferencePoint[] ValidateRoute(IReadOnlyList<ReferencePoint>? referencePoints)
    {
        if (referencePoints is null || referencePoints.Count < 2)
        {
            throw new AnalysisConfigurationException("A reference route requires at least two points.");
        }
        if (referencePoints.Any(p => p is null || !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z) ||
            !double.IsFinite(p.GreenRadiusM) || !double.IsFinite(p.YellowRadiusM) || p.GreenRadiusM < 0 || p.YellowRadiusM < p.GreenRadiusM))
        {
            throw new AnalysisConfigurationException("Route points must be finite with 0 <= GreenRadiusM <= YellowRadiusM.");
        }
        if (referencePoints.Select(p => p.SequenceNo).Distinct().Count() != referencePoints.Count)
        {
            throw new AnalysisConfigurationException("Reference point SequenceNo values must be unique.");
        }

        var ordered = referencePoints.OrderBy(p => p.SequenceNo).ToArray();
        var hasSegment = false;
        for (var i = 1; i < ordered.Length; i++)
        {
            var dx = ordered[i].X - ordered[i - 1].X;
            var dy = ordered[i].Y - ordered[i - 1].Y;
            if (!double.IsFinite(dx) || !double.IsFinite(dy))
            {
                throw new AnalysisConfigurationException("Route segment coordinates exceed supported numeric precision.");
            }
            hasSegment |= dx != 0 || dy != 0;
        }
        if (!hasSegment) throw new AnalysisConfigurationException("The reference route has no non-zero X/Y segment.");
        return ordered;
    }

    private static (double Distance, double Green, double Yellow) Nearest(double x, double y, ReferencePoint[] points)
    {
        var best = (Distance: double.PositiveInfinity, Green: 0d, Yellow: 0d);
        for (var i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (scale == 0) continue;
            var sx = dx / scale;
            var sy = dy / scale;
            var projection = (((x - a.X) / scale) * sx + ((y - a.Y) / scale) * sy) / (sx * sx + sy * sy);
            if (double.IsNaN(projection)) throw new ArgumentException("Route projection exceeds supported numeric precision.");
            var t = Math.Clamp(projection, 0, 1);
            var nearestX = (1 - t) * a.X + t * b.X;
            var nearestY = (1 - t) * a.Y + t * b.Y;
            var distance = double.Hypot(x - nearestX, y - nearestY);
            if (!double.IsFinite(distance)) throw new ArgumentException("Route deviation exceeds supported numeric precision.");
            // Exact ties keep the first segment in SequenceNo order.
            if (distance < best.Distance)
            {
                best = (distance, (1 - t) * a.GreenRadiusM + t * b.GreenRadiusM,
                    (1 - t) * a.YellowRadiusM + t * b.YellowRadiusM);
            }
        }
        return best;
    }
}
