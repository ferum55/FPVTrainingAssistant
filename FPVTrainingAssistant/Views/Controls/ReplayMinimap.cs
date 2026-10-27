using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Views.Controls;

public sealed class ReplayMinimap : FrameworkElement
{
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(nameof(Scene), typeof(ReplayMinimapViewModel), typeof(ReplayMinimap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((ReplayMinimap)d)._staticDrawing = null));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(ReplayMinimapFrame), typeof(ReplayMinimap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public ReplayMinimapViewModel? Scene { get => (ReplayMinimapViewModel?)GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public ReplayMinimapFrame? Frame { get => (ReplayMinimapFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    private DrawingGroup? _staticDrawing;

    public ReplayMinimap()
    {
        ClipToBounds = true;
        SizeChanged += (_, _) => { _staticDrawing = null; InvalidateVisual(); };
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (Scene is not { } scene || ActualWidth <= 0 || ActualHeight <= 0) return;
        var projection = scene.Projection(ActualWidth, ActualHeight);
        _staticDrawing ??= BuildDrawing(scene, projection);
        drawing.DrawDrawing(_staticDrawing);
        if (Frame is not { } frame) return;
        var projected = scene.Project(frame, ActualWidth, ActualHeight);
        if (projected.Drone is { } drone)
        {
            var point = ToPoint(drone);
            drawing.DrawEllipse(Brush("DroneMarkerBrush"), null, point, 5, 5);
            var yaw = (frame.Drone?.YawDeg ?? 0) * Math.PI / 180;
            drawing.DrawLine(new Pen(Brush("DroneMarkerBrush"), 2), point, new(point.X + 12 * Math.Cos(yaw), point.Y - 12 * Math.Sin(yaw)));
        }
        foreach (var entity in projected.Entities)
        {
            var point = ToPoint(entity.Position);
            drawing.DrawRectangle(Brush("WarningBrush"), null, new Rect(point.X - 4, point.Y - 4, 8, 8));
            var label = entity.EntityId.Length > 22 ? entity.EntityId[..19] + "…" : entity.EntityId;
            Text(drawing, label, new(Math.Clamp(point.X + 6, 0, Math.Max(0, ActualWidth - 130)), Math.Clamp(point.Y + 6, 0, Math.Max(0, ActualHeight - 16))));
        }
    }

    private DrawingGroup BuildDrawing(ReplayMinimapViewModel scene, TrajectoryProjection projection)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open())
        {
            drawing.DrawRectangle(Brush("InsetBrush"), null, new Rect(RenderSize));
            drawing.DrawGeometry(null, new Pen(Brush("RecordedTrajectoryBrush"), 1.5), Path(scene.Trajectory, projection));
            if (scene.HasRoute) drawing.DrawGeometry(null, new Pen(Brush("ReferenceRouteBrush"), 1) { DashStyle = DashStyles.Dash }, Path(scene.Route, projection));
            Text(drawing, "+X →   +Y ↑", new(8, 6));
        }
        group.Freeze();
        return group;
    }

    private static StreamGeometry Path(IEnumerable<Point2D> points, TrajectoryProjection projection)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                var projected = ToPoint(projection.Project(point));
                if (first) { context.BeginFigure(projected, false, false); first = false; }
                else context.LineTo(projected, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }
    private Brush Brush(string key) => (Brush)FindResource(key);
    private void Text(DrawingContext drawing, string text, Point point) => drawing.DrawText(new FormattedText(text, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brush("MutedBrush"), VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
    private static Point ToPoint(Point2D point) => new(point.X, point.Y);
}
