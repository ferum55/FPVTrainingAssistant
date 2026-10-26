using System.Text.Json;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class SessionValidatorTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteTelemetryIsRejected(double value)
    {
        var session = Session();
        session.Telemetry[0] = new TelemetrySample
        {
            TimeSec = value, X = value, Y = value, Z = value,
            SpeedMps = value, AltitudeM = value, PitchDeg = value, RollDeg = value, YawDeg = value,
            BatteryPercent = value, ControlLinkQualityPercent = value, VideoLinkQualityPercent = value
        };
        var result = new SessionValidator().Validate(session);
        Assert.False(result.IsValid);
        Assert.Equal(12, result.Errors.Count(issue => issue.Code == "NonFiniteNumber"));
    }

    [Fact]
    public void NullSessionProducesAnIssue() =>
        Assert.False(new SessionValidator().Validate(null).IsValid);

    [Fact]
    public void LargeFinitePhysicalValuesAreNotArbitrarilyLimited()
    {
        var session = Session();
        session.Telemetry[0] = new TelemetrySample
        {
            TimeSec = 0, X = 1e15, Y = -1e15, Z = 1e15, SpeedMps = 1e10,
            AltitudeM = -1e15, PitchDeg = 1e15, RollDeg = -1e15, YawDeg = 1e15
        };
        Assert.True(new SessionValidator().Validate(session).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void LinkQualityPercentagesAreChecked(double percent)
    {
        var session = Session();
        session.Telemetry[0] = new TelemetrySample
        {
            TimeSec = 0, X = 0, Y = 0, Z = 0, SpeedMps = 0, AltitudeM = 0,
            PitchDeg = 0, RollDeg = 0, YawDeg = 0,
            ControlLinkQualityPercent = percent, VideoLinkQualityPercent = percent
        };
        var result = new SessionValidator().Validate(session);
        Assert.Equal(2, result.Errors.Count(issue => issue.Code == "PercentageOutOfRange"));
    }

    private static TrainingSession Session() => JsonSerializer.Deserialize<TrainingSession>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestSessions", "valid_obstacle_course.json")), SessionJson.Options)!;
}
