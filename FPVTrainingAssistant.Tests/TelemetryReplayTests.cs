using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class TelemetryReplayTests
{
    [Theory]
    [InlineData(-100, 2, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(10, 10, 10)]
    [InlineData(100, 10, 10)]
    public void ExactEndpointsAndClamping(double requested, double time, double x)
    {
        var state = new TelemetryReplayService([Sample(2, 0), Sample(10, 10)]).GetStateAtTime(requested)!;
        Assert.Equal(time, state.TimeSec); Assert.Equal(x, state.X);
    }

    [Fact]
    public void PositionSpeedAltitudeAndPercentagesAreInterpolated()
    {
        var state = new TelemetryReplayService([Sample(2, 0, 80), Sample(10, 10, 40)]).GetStateAtTime(6)!;
        Assert.Equal(5, state.X); Assert.Equal(10, state.Y); Assert.Equal(15, state.Z);
        Assert.Equal(2.5, state.SpeedMps); Assert.Equal(20, state.AltitudeM);
        Assert.Equal(60d, state.BatteryPercent); Assert.Equal(60d, state.ControlLinkQualityPercent); Assert.Equal(60d, state.VideoLinkQualityPercent);
    }

    [Fact]
    public void NonuniformTimesUseActualTimeFactor()
    {
        var state = new TelemetryReplayService([Sample(0, 0), Sample(1, 10), Sample(9, 50)]).GetStateAtTime(3)!;
        Assert.Equal(20, state.X);
    }

    [Theory]
    [InlineData(0, 90, 45)]
    [InlineData(179, -179, -180)]
    [InlineData(-179, 179, -180)]
    [InlineData(170, -170, -180)]
    public void EveryAngleFollowsShortestPath(double a, double b, double expected)
    {
        var state = new TelemetryReplayService([Sample(0, 0, angle: a), Sample(10, 0, angle: b)]).GetStateAtTime(5)!;
        Assert.Equal(expected, state.YawDeg, 10);
        Assert.Equal(expected, state.PitchDeg, 10);
        Assert.Equal(expected, state.RollDeg, 10);
    }

    [Fact]
    public void OptionalValuesAreNullBetweenSamplesIfEitherEndpointIsMissing()
    {
        var replay = new TelemetryReplayService([Sample(0, 0, 100), Sample(10, 10)]);
        Assert.Null(replay.GetStateAtTime(5)!.BatteryPercent);
        Assert.Null(replay.GetStateAtTime(5)!.ControlLinkQualityPercent);
        Assert.Null(replay.GetStateAtTime(5)!.VideoLinkQualityPercent);
        Assert.Equal(100d, replay.GetStateAtTime(0)!.BatteryPercent);
        Assert.Null(replay.GetStateAtTime(10)!.BatteryPercent);
    }

    [Fact]
    public void EmptyAndSingleSampleSequencesAreSafe()
    {
        Assert.Null(new TelemetryReplayService([]).GetStateAtTime(0));
        var replay = new TelemetryReplayService([Sample(7, 3)]);
        Assert.Equal(7, replay.GetStateAtTime(-100)!.TimeSec);
        Assert.Equal(3, replay.GetStateAtTime(100)!.X);
    }

    [Fact]
    public void SearchWorksWithSeveralThousandSamples()
    {
        var samples = Enumerable.Range(0, 6000).Select(i => Sample(i * 0.1, i)).ToArray();
        var replay = new TelemetryReplayService(samples);
        Assert.Equal(3210.5, replay.GetStateAtTime(321.05)!.X, 8);
        Assert.Equal(321.1, replay.NextSampleTime(321.05), 8);
        Assert.Equal(321, replay.PreviousSampleTime(321.05), 8);
    }

    [Fact]
    public void InvalidTimeOrderingIsRejectedOnceAtConstruction() =>
        Assert.Throws<ArgumentException>(() => new TelemetryReplayService([Sample(1, 0), Sample(1, 1)]));

    private static TelemetrySample Sample(double time, double x, double? percent = null, double angle = 0) => new()
    {
        TimeSec = time, X = x, Y = 2 * x, Z = 3 * x, SpeedMps = x / 2, AltitudeM = 4 * x,
        PitchDeg = angle, RollDeg = angle, YawDeg = angle,
        BatteryPercent = percent, ControlLinkQualityPercent = percent, VideoLinkQualityPercent = percent
    };
}
