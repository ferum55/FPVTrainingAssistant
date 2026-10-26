using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Views.Controls;

// WPF rendering adapter. Static geometry is rebuilt only for a new scene/viewport.
public sealed class TrajectoryMap : FrameworkElement
{
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(nameof(Scene), typeof(TrajectoryViewModel), typeof(TrajectoryMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((TrajectoryMap)d)._staticDrawing = null));
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(ReplayState), typeof(TrajectoryMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public TrajectoryViewModel? Scene { get => (TrajectoryViewModel?)GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public ReplayState? State { get => (ReplayState?)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    private DrawingGroup? _staticDrawing;
    private TrajectoryProjection? _projection;

    public TrajectoryMap()
    {
        ClipToBounds = true;
        SizeChanged += (_, _) => { _staticDrawing = null; InvalidateVisual(); };
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        drawing.DrawRectangle(Brush("InsetBrush"), null, new Rect(RenderSize));
        if (Scene is not { HasTelemetry: true } scene)
        {
            Text(drawing, "No telemetry is available for replay.", new Point(24, 24), Brush("MutedBrush"));
            return;
        }
        _staticDrawing ??= BuildDrawing(scene);
        drawing.DrawDrawing(_staticDrawing);
        if (State is { } state && _projection is { } projection)
        {
            var position = ToPoint(projection.Project(new(state.X, state.Y)));
            drawing.DrawEllipse(Brush("InsetBrush"), new Pen(Brush("DroneMarkerBrush"), 1.5), position, 12, 12);
            drawing.PushTransform(new RotateTransform(-state.YawDeg, position.X, position.Y));
            var arrow = new StreamGeometry();
            using (var context = arrow.Open())
            {
                context.BeginFigure(new(position.X + 10, position.Y), true, true);
                context.LineTo(new(position.X - 7, position.Y - 6), true, false);
                context.LineTo(new(position.X - 4, position.Y), true, false);
                context.LineTo(new(position.X - 7, position.Y + 6), true, false);
            }
            drawing.DrawGeometry(Brush("DroneMarkerBrush"), null, arrow);
            drawing.Pop();
        }
    }

    private DrawingGroup BuildDrawing(TrajectoryViewModel scene)
    {
        _projection = new TrajectoryProjectionService().Create(scene.TrajectoryPoints, scene.RouteVisualBounds, ActualWidth, ActualHeight, 36);
        var projection = _projection;
        var group = new DrawingGroup();
        using (var drawing = group.Open())
        {
            foreach (var layer in new[] { "Red", "Yellow", "Green" })
            {
                foreach (var segment in scene.ZoneSegments)
                {
                    var radius = layer == "Green" ? segment.GreenRadiusM : layer == "Yellow" ? segment.YellowRadiusM : segment.RedRadiusM;
                    var width = projection.LengthInPixels(radius) * 2;
                    if (width <= 0) continue;
                    var pen = new Pen(Brush("Route" + layer + "Brush"), Math.Min(width, Math.Max(ActualWidth, ActualHeight) * 4))
                    { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                    drawing.DrawLine(pen, ToPoint(projection.Project(segment.Start)), ToPoint(projection.Project(segment.End)));
                }
            }
            if (scene.HasRoute)
            {
                var pen = new Pen(Brush("ReferenceRouteBrush"), 2) { DashStyle = DashStyles.Dash };
                drawing.DrawGeometry(null, pen, Polyline(scene.ReferencePoints.Select(p => new Point2D(p.X, p.Y)), projection));
            }
            drawing.DrawGeometry(null, new Pen(Brush("RecordedTrajectoryBrush"), 2.5), Polyline(scene.TrajectoryPoints, projection));
            if (scene.HasRoute)
            {
                foreach (var reference in scene.ReferencePoints)
                {
                    var point = ToPoint(projection.Project(new(reference.X, reference.Y)));
                    drawing.DrawEllipse(Brush("ReferenceRouteBrush"), new Pen(Brush("InsetBrush"), 1), point, 4, 4);
                    Text(drawing, "R" + reference.SequenceNo.ToString(CultureInfo.InvariantCulture), new(point.X + 7, point.Y + 5), Brush("ReferenceRouteBrush"));
                }
            }
            var axisPen = new Pen(Brush("MutedBrush"), 1);
            drawing.DrawLine(axisPen, new(20, ActualHeight - 24), new(50, ActualHeight - 24));
            drawing.DrawLine(axisPen, new(20, ActualHeight - 24), new(20, ActualHeight - 54));
            Text(drawing, "+X", new(53, ActualHeight - 32), Brush("MutedBrush"));
            Text(drawing, "+Y", new(12, ActualHeight - 73), Brush("MutedBrush"));
            Text(drawing, FormattableString.Invariant($"X {projection.MinX:G4}..{projection.MaxX:G4} m   ·   Y {projection.MinY:G4}..{projection.MaxY:G4} m"), new(18, 12), Brush("MutedBrush"));
        }
        group.Freeze();
        return group;
    }

    private static StreamGeometry Polyline(IEnumerable<Point2D> points, TrajectoryProjection projection)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                var view = ToPoint(projection.Project(point));
                if (first) { context.BeginFigure(view, false, false); first = false; }
                else context.LineTo(view, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private Brush Brush(string key) => (Brush)FindResource(key);
    private void Text(DrawingContext drawing, string text, Point position, Brush brush) => drawing.DrawText(
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), position);
    private static Point ToPoint(Point2D p) => new(p.X, p.Y);
}
