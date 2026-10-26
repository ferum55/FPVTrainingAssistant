using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class ExtendedTelemetryReplayTests
{
    private static TelemetrySample Sample(double t) => new()
    {
        TimeSec = t, X = t, Y = t, Z = t, SpeedMps = t, AltitudeM = t,
        PitchDeg = 0, RollDeg = 0, YawDeg = 0,
        VerticalSpeedMps = -2 + t, CameraTiltDeg = t == 0 ? 179 : -179, CameraFovDeg = 70 + t * 20,
        BatteryVoltageV = 16 + t, CellVoltageV = 4 + t, CurrentA = -2 + t, ConsumedMah = 100 * t,
        ControlRssiDbm = -80 + 10 * t, Failsafe = t == 0, Armed = t > 0, FlightMode = t == 0 ? "FutureMode" : "ANGLE"
    };

    [Fact]
    public void ExtendedNumbersUseLinearInterpolationAndCameraTiltUsesShortestAngle()
    {
        var state = new TelemetryReplayService([Sample(0), Sample(2)]).GetStateAtTime(1)!;
        Assert.Equal(-1, state.VerticalSpeedMps);
        Assert.Equal(-180, state.CameraTiltDeg);
        Assert.Equal(90, state.CameraFovDeg);
        Assert.Equal(17, state.BatteryVoltageV);
        Assert.Equal(5, state.CellVoltageV);
        Assert.Equal(-1, state.CurrentA);
        Assert.Equal(100, state.ConsumedMah);
        Assert.Equal(-70, state.ControlRssiDbm);
    }

    [Fact]
    public void DiscreteFlightFieldsHoldEarlierSampleUntilExactNextTimestamp()
    {
        var replay = new TelemetryReplayService([Sample(0), Sample(2)]);
        Assert.True(replay.GetStateAtTime(1)!.Failsafe);
        Assert.False(replay.GetStateAtTime(1)!.Armed);
        Assert.Equal("FutureMode", replay.GetStateAtTime(1)!.FlightMode);
        Assert.False(replay.GetStateAtTime(2)!.Failsafe);
        Assert.True(replay.GetStateAtTime(2)!.Armed);
        Assert.Equal("ANGLE", replay.GetStateAtTime(2)!.FlightMode);
    }

    [Fact]
    public void MissingEndpointsStayUnavailableAndExactSamplesRetainTheirValues()
    {
        var replay = new TelemetryReplayService([Sample(0), AnalysisTestData.Sample(2)]);
        Assert.Equal(70, replay.GetStateAtTime(0)!.CameraFovDeg);
        var midpoint = replay.GetStateAtTime(1)!;
        Assert.Null(midpoint.CameraFovDeg);
        Assert.Null(midpoint.CameraTiltDeg);
        Assert.Null(midpoint.BatteryVoltageV);
        Assert.Null(midpoint.ConsumedMah);
        Assert.Null(replay.GetStateAtTime(2)!.FlightMode);
    }
}
