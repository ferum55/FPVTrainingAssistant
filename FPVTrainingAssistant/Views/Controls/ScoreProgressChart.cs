using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Views.Controls;

public sealed class ScoreProgressChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(IReadOnlyList<ScoreProgressPoint>), typeof(ScoreProgressChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<ScoreProgressPoint>? Points { get => (IReadOnlyList<ScoreProgressPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (ActualWidth < 80 || ActualHeight < 60) return;
        var left = 38d; var right = ActualWidth - 24; var top = 16d; var bottom = ActualHeight - 28;
        foreach (var score in new[] { 0, 50, 100 })
        {
            var y = bottom - (bottom - top) * score / 100;
            drawing.DrawLine(new Pen(Brush("BorderBrush"), 1), new(left, y), new(right, y));
            Text(drawing, score.ToString(CultureInfo.InvariantCulture), new(0, y - 7), Brush("MutedBrush"));
        }
        var points = Points ?? [];
        if (points.Count == 0) { Text(drawing, "No scores available.", new(left, top), Brush("MutedBrush")); return; }
        Point? previous = null;
        for (var i = 0; i < points.Count; i++)
        {
            var x = points.Count == 1 ? (left + right) / 2 : left + (right - left) * i / (points.Count - 1);
            var p = new Point(x, bottom - (bottom - top) * points[i].Score / 100);
            if (previous is { } a) drawing.DrawLine(new Pen(Brush("AccentBrush"), 2), a, p);
            drawing.DrawEllipse(Brush("AccentBrush"), null, p, 4, 4);
            Text(drawing, points[i].Score.ToString("F1", CultureInfo.InvariantCulture), new(Math.Min(p.X + 5, right - 22), Math.Max(0, p.Y - 19)), Brush("TextBrush"));
            previous = p;
        }
        Text(drawing, points[0].StartedAtUtc.ToLocalTime().ToString("dd MMM", CultureInfo.InvariantCulture), new(left, bottom + 9), Brush("MutedBrush"));
        if (points.Count > 1) Text(drawing, points[^1].StartedAtUtc.ToLocalTime().ToString("dd MMM", CultureInfo.InvariantCulture), new(right - 40, bottom + 9), Brush("MutedBrush"));
    }
    private Brush Brush(string key) => (Brush)FindResource(key);
    private void Text(DrawingContext d, string text, Point p, Brush brush) => d.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), p);
}
