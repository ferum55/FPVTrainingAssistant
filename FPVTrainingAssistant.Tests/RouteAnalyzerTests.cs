using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class RouteAnalyzerTests
{
    [Theory]
    [InlineData(0, 100, 0, 0)]
    [InlineData(1, 100, 0, 0)]
    [InlineData(2, 0, 100, 0)]
    [InlineData(3, 0, 100, 0)]
    [InlineData(4, 0, 0, 100)]
    public void ZonesAndExactBoundaries(double y, double green, double yellow, double red)
    {
        var result = Analyze([Sample(0, 0, y), Sample(10, 10, y)]);
        Assert.Equal(green, result.GreenTimePercent, 10);
        Assert.Equal(yellow, result.YellowTimePercent, 10);
        Assert.Equal(red, result.RedTimePercent, 10);
        Assert.Equal(y, result.AverageDeviationM, 10);
        Assert.Equal(y, result.MaximumDeviationM, 10);
    }

    [Fact]
    public void NonuniformIntervalsWeightMidpointsAndAverageByElapsedTime()
    {
        // Interval midpoints: y=0 for 1 s, y=2 for 3 s, y=5 for 6 s.
        var result = Analyze([Sample(0, 0, 0), Sample(1, 0, 0), Sample(4, 0, 4), Sample(10, 0, 6)]);
        Assert.Equal(10, result.GreenTimePercent, 10);
        Assert.Equal(30, result.YellowTimePercent, 10);
        Assert.Equal(60, result.RedTimePercent, 10);
        Assert.Equal(3.6, result.AverageDeviationM, 10);
        Assert.Equal(5, result.MaximumDeviationM, 10);
        Assert.Equal(10, result.TotalEvaluatedTimeSec);
        Assert.Equal(100, result.GreenTimePercent + result.YellowTimePercent + result.RedTimePercent, 10);
    }

    [Fact]
    public void PolylineChoosesNearestSegmentAndOrdersReferencePoints()
    {
        var result = new RouteAnalyzer().Analyze([Sample(0, 9, 4), Sample(2, 9, 6)],
            [Point(2, 10, 10), Point(0, 0), Point(1, 10)]);
        Assert.Equal(1, result.AverageDeviationM, 10);
        Assert.Equal(100, result.GreenTimePercent, 10);
    }

    [Fact]
    public void RadiiAreInterpolatedAtClosestProjection()
    {
        var result = new RouteAnalyzer().Analyze([Sample(0, 5, 2), Sample(1, 5, 2)],
            [Point(0, 0, green: 1, yellow: 2), Point(1, 10, green: 3, yellow: 6)]);
        Assert.Equal(100, result.GreenTimePercent); // Interpolated green radius = 2.
    }

    [Fact]
    public void ProjectionClampsToEndpoint()
    {
        var result = Analyze([Sample(0, 13, 4), Sample(1, 13, 4)]);
        Assert.Equal(5, result.MaximumDeviationM, 10);
        Assert.Equal(100, result.RedTimePercent);
    }

    [Fact]
    public void DuplicatePointAndZeroLengthSegmentAreIgnored()
    {
        var result = new RouteAnalyzer().Analyze([Sample(0, 1), Sample(1, 9)],
            [Point(0, 0), Point(1, 0), Point(2, 10)]);
        Assert.Equal(100, result.GreenTimePercent);
    }

    [Fact]
    public void AllZeroLengthSegmentsAreRejected() =>
        Assert.Throws<AnalysisConfigurationException>(() => new RouteAnalyzer().Analyze([Sample(0), Sample(1)], [Point(0, 0), Point(1, 0)]));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FewerThanTwoRoutePointsAreRejected(int count) =>
        Assert.Throws<AnalysisConfigurationException>(() => new RouteAnalyzer().Analyze([Sample(0), Sample(1)], new[] { Point(0, 0) }.Take(count).ToArray()));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FewerThanTwoTelemetrySamplesAreRejected(int count) =>
        Assert.Throws<ArgumentException>(() => Analyze(new[] { Sample(0) }.Take(count).ToArray()));

    [Fact]
    public void ZeroElapsedTimeIsRejected() =>
        Assert.Throws<ArgumentException>(() => Analyze([Sample(0), Sample(0)]));

    [Fact]
    public void ZDoesNotAffectRouteDistance()
    {
        var result = Analyze([Sample(0, 0, 0, 100), Sample(1, 10, 0, -100)]);
        Assert.Equal(0, result.MaximumDeviationM);
        Assert.Equal(100, result.GreenTimePercent);
    }

    [Fact]
    public void EqualDistanceTiesUseFirstOrderedSegment()
    {
        var result = new RouteAnalyzer().Analyze([Sample(0, 5, 5), Sample(1, 5, 5)],
            [Point(0, 0, green: 1, yellow: 2), Point(1, 10, green: 1, yellow: 2), Point(2, 10, 10, green: 20, yellow: 20)]);
        Assert.Equal(100, result.RedTimePercent);
    }

    [Fact]
    public void NoDurationIsExtrapolatedBeyondRecordedSamples()
    {
        var result = Analyze([Sample(5, 0), Sample(8, 10)]);
        Assert.Equal(3, result.TotalEvaluatedTimeSec);
    }

    private static RouteAnalysisResult Analyze(IReadOnlyList<TelemetrySample> telemetry) =>
        new RouteAnalyzer().Analyze(telemetry, [Point(0, 0), Point(1, 10)]);
}
