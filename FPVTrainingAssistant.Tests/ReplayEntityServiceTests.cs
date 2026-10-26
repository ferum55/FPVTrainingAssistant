using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class ReplayEntityServiceTests
{
    private static ReplayEntitySample Sample(double time, double x, double yaw = 0, bool? active = null, string? state = null) =>
        new() { TimeSec = time, X = x, Y = x * 2, Z = x / 2, PitchDeg = yaw, RollDeg = yaw, YawDeg = yaw, IsActive = active, State = state };
    private static ReplayEntityTrack Track(params ReplayEntitySample[] samples) =>
        new() { EntityId = "future-entity", EntityType = "FutureType", ArchetypeId = "future-visual", Samples = samples.ToList() };

    [Fact]
    public void BeforeFirstSampleHasNoTransformOrActiveMarker()
    {
        var state = new ReplayEntityService(Track(Sample(2, 4), Sample(4, 8))).GetStateAtTime(1);
        Assert.False(state.IsAvailable);
        Assert.False(state.IsActive);
        Assert.Null(state.Transform);
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(4, 8)]
    [InlineData(20, 8)]
    public void ExactInterpolatedAndFinalTransformsUseRecordedData(double time, double expectedX)
    {
        var state = new ReplayEntityService(Track(Sample(2, 4), Sample(4, 8))).GetStateAtTime(time);
        Assert.True(state.IsActive);
        Assert.Equal(expectedX, state.Transform!.X);
        Assert.Equal(expectedX * 2, state.Transform.Y);
        Assert.Equal(expectedX / 2, state.Transform.Z);
        Assert.Equal("future-entity", state.EntityId);
        Assert.Equal("FutureType", state.EntityType);
        Assert.Equal("future-visual", state.ArchetypeId);
    }

    [Fact]
    public void EveryAngleFollowsTheSameShortestPathAsDroneReplay()
    {
        var entity = new ReplayEntityService(Track(Sample(0, 0, 179), Sample(2, 2, -179))).GetStateAtTime(1).Transform!;
        Assert.Equal(-180, entity.PitchDeg);
        Assert.Equal(-180, entity.RollDeg);
        Assert.Equal(-180, entity.YawDeg);
    }

    [Fact]
    public void Exact180DegreeTieTakesNegativeDirection()
    {
        var entity = new ReplayEntityService(Track(Sample(0, 0), Sample(2, 2, 180))).GetStateAtTime(1);
        Assert.Equal(-90, entity.Transform!.YawDeg);
    }

    [Theory]
    [InlineData(0, true, "FutureState")]
    [InlineData(1, true, "FutureState")]
    [InlineData(2, false, "destroyed")]
    [InlineData(10, false, "destroyed")]
    public void ActivityAndUnknownStatesChangeAtRecordedTimestamp(double time, bool active, string expected)
    {
        var service = new ReplayEntityService(Track(Sample(0, 0, state: "FutureState"), Sample(2, 2, active: false, state: "destroyed")));
        var state = service.GetStateAtTime(time);
        Assert.Equal(active, state.IsActive);
        Assert.Equal(expected, state.StateText);
    }

    [Fact]
    public void StateLabelAloneDoesNotInventInactivity()
    {
        var state = new ReplayEntityService(Track(Sample(0, 2, state: "destroyed"))).GetStateAtTime(50);
        Assert.True(state.IsActive);
        Assert.Equal(2, state.Transform!.X);
    }

    [Fact]
    public void SeekingBackRestoresEarlierActivityWithoutChangingTrack()
    {
        var track = Track(Sample(0, 0, active: true), Sample(2, 2, active: false));
        var service = new ReplayEntityService(track);
        Assert.False(service.GetStateAtTime(10).IsActive);
        Assert.True(service.GetStateAtTime(1).IsActive);
        Assert.True(track.Samples[0].IsActive);
        Assert.False(track.Samples[1].IsActive);
    }

    [Fact]
    public void InvalidTrackAndNaNTimeAreRejectedClearly()
    {
        Assert.Throws<ArgumentException>(() => new ReplayEntityService(Track()));
        Assert.Throws<ArgumentException>(() => new ReplayEntityService(Track(Sample(2, 1), Sample(1, 2))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReplayEntityService(Track(Sample(0, 1))).GetStateAtTime(double.NaN));
    }
}
