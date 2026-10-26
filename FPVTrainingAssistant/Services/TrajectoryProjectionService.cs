using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class TrajectoryProjectionService
{
    public TrajectoryProjection Create(IEnumerable<Point2D> trajectory, IEnumerable<Point2D> referencePoints,
        double width, double height, double padding = 30)
    {
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(padding) || padding < 0) throw new ArgumentOutOfRangeException(nameof(padding));
        var points = trajectory.Concat(referencePoints).ToArray();
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) throw new ArgumentException("Projection points must be finite.");
        var minX = points.Length > 0 ? points.Min(p => p.X) : 0;
        var maxX = points.Length > 0 ? points.Max(p => p.X) : 0;
        var minY = points.Length > 0 ? points.Min(p => p.Y) : 0;
        var maxY = points.Length > 0 ? points.Max(p => p.Y) : 0;
        return new(minX, maxX, minY, maxY, width, height, Math.Min(padding, Math.Min(width, height) / 4));
    }
}

public sealed class TrajectoryProjection
{
    private readonly double _centerX, _centerY, _normalizer, _scale, _offsetX, _offsetY;
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }

    internal TrajectoryProjection(double minX, double maxX, double minY, double maxY, double width, double height, double padding)
    {
        MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY;
        _centerX = minX / 2 + maxX / 2;
        _centerY = minY / 2 + maxY / 2;
        // Half-ranges remain finite even when max-min would overflow.
        var range = Math.Max(maxX / 2 - minX / 2, maxY / 2 - minY / 2);
        if (range == 0) range = Math.Max(maxX - minX, maxY - minY); // Subnormal ranges.
        _normalizer = range > 0 ? range : 1;
        var left = (minX - _centerX) / _normalizer;
        var right = (maxX - _centerX) / _normalizer;
        var bottom = (minY - _centerY) / _normalizer;
        var top = (maxY - _centerY) / _normalizer;
        var scaleX = right > left ? (width - 2 * padding) / (right - left) : double.PositiveInfinity;
        var scaleY = top > bottom ? (height - 2 * padding) / (top - bottom) : double.PositiveInfinity;
        _scale = range > 0 ? Math.Min(scaleX, scaleY) : 1;
        _offsetX = width / 2 - (left / 2 + right / 2) * _scale;
        _offsetY = height / 2 + (bottom / 2 + top / 2) * _scale;
    }

    public Point2D Project(Point2D point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentException("Projection points must be finite.");
        return new(_offsetX + ((point.X - _centerX) / _normalizer) * _scale,
            _offsetY - ((point.Y - _centerY) / _normalizer) * _scale);
    }

    public double LengthInPixels(double meters) => (meters / _normalizer) * _scale;
}
