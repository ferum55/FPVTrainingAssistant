using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class TrajectoryProjectionTests
{
    [Fact]
    public void ProjectionPreservesAspectRatioAndCentersUnusedSpace()
    {
        var p = Project([new(0, 0), new(10, 5)], width: 200, height: 200, padding: 20);
        var a = p.Project(new(0, 0)); var b = p.Project(new(10, 5));
        Assert.Equal(2, (b.X - a.X) / (a.Y - b.Y), 10);
        Assert.Equal(20, a.X, 10); Assert.Equal(180, b.X, 10);
        Assert.Equal(140, a.Y, 10); Assert.Equal(60, b.Y, 10);
    }

    [Fact]
    public void NegativeCoordinatesAndPositiveYUpAreSupported()
    {
        var p = Project([new(-10, -10), new(-5, -5)]);
        Assert.True(p.Project(new(-5, -5)).Y < p.Project(new(-10, -10)).Y);
        Assert.True(p.Project(new(-5, -5)).X > p.Project(new(-10, -10)).X);
    }

    [Fact]
    public void VerticalTrajectoryCentersX()
    {
        var p = Project([new(7, -5), new(7, 5)]);
        Assert.Equal(100, p.Project(new(7, 0)).X, 10);
        Assert.Equal(20, p.Project(new(7, 5)).Y, 10);
    }

    [Fact]
    public void HorizontalTrajectoryCentersY()
    {
        var p = Project([new(-5, 12), new(5, 12)]);
        Assert.Equal(100, p.Project(new(0, 12)).Y, 10);
    }

    [Fact]
    public void SinglePointAndEmptySceneAreSafe()
    {
        Assert.Equal(new Point2D(100, 100), Project([new(-23, 42)]).Project(new(-23, 42)));
        Assert.Equal(new Point2D(100, 100), Project([]).Project(new(0, 0)));
    }

    [Fact]
    public void RouteAndTrajectoryContributeToTheSameBounds()
    {
        var p = new TrajectoryProjectionService().Create([new(0, 0)], [new(-10, -20), new(30, 40)], 200, 200, 20);
        Assert.Equal(-10, p.MinX); Assert.Equal(30, p.MaxX); Assert.Equal(-20, p.MinY); Assert.Equal(40, p.MaxY);
        foreach (var point in new[] { new Point2D(-10, -20), new Point2D(30, 40), new Point2D(0, 0) })
        {
            var view = p.Project(point);
            Assert.InRange(view.X, 20, 180); Assert.InRange(view.Y, 20, 180);
        }
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(1e-300)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    public void VeryLargeAndVerySmallRangesStayFinite(double extent)
    {
        var p = Project([new(-extent, -extent), new(extent, extent)]);
        var a = p.Project(new(-extent, -extent)); var b = p.Project(new(extent, extent));
        Assert.True(double.IsFinite(a.X) && double.IsFinite(a.Y) && double.IsFinite(b.X) && double.IsFinite(b.Y));
        Assert.Equal(20, a.X, 8); Assert.Equal(180, b.X, 8);
    }

    [Fact]
    public void ConstantHugeXDoesNotEraseTinyYRange()
    {
        var p = Project([new(1e300, 0), new(1e300, 1e-300)]);
        Assert.Equal(100, p.Project(new(1e300, 0)).X, 8);
        Assert.Equal(20, p.Project(new(1e300, 1e-300)).Y, 8);
    }

    [Fact]
    public void PaddingIsCappedForTinyViewports()
    {
        var p = Project([new(0, 0), new(1, 1)], 10, 10, 100);
        Assert.Equal(2.5, p.Project(new(0, 0)).X, 8);
    }

    private static TrajectoryProjection Project(Point2D[] points, double width = 200, double height = 200, double padding = 20) =>
        new TrajectoryProjectionService().Create(points, [], width, height, padding);
}
