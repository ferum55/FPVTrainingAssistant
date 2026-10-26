using System.Text.Json;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class MockSimulatorReplayViewModelTests
{
    [Theory]
    [InlineData("replay_obstacle_course_v1_1.json")]
    [InlineData("replay_target_mission_v1_1.json")]
    public async Task BothImportedFixturesInitializeActualSessionMetadata(string file)
    {
        var imported = await new SessionImportService().ImportAsync(ReplayTestFiles.FixturePath(file));
        var session = imported.Session!;
        var profile = new ExerciseProfileProvider().Resolve(session)!;
        using var vm = new SimulatorReplayMockViewModel(session, profile, new SessionAnalyzer().Analyze(session, profile));
        Assert.True(vm.IsAvailable);
        Assert.Same(session, vm.Session);
        Assert.Equal(session.SessionId, vm.SessionId);
        Assert.Equal(session.Replay!.EnvironmentId, vm.EnvironmentId);
        Assert.Equal(session.Replay.ScenarioId, vm.ScenarioId);
        Assert.Equal(session.SourceSimulator, vm.SourceSimulator);
        Assert.Contains("Telemetry samples: " + session.Telemetry.Count, vm.DataSummary);
        Assert.Contains("Events: " + session.Events.Count, vm.DataSummary);
        Assert.Equal(session.Replay.Entities.Count, vm.DynamicEntities.Count);
        Assert.Equal(session.Telemetry[0].TimeSec, vm.CurrentTimeSec);
        Assert.False(vm.IsPlaying);
        Assert.Equal("FPV", vm.CameraMode);
    }

    [Fact]
    public void FirstAndLastTimelineBoundsComeFromActualSamples()
    {
        using var vm = MockReplayTestData.ViewModel(edit: document =>
        {
            var samples = document["telemetry"]!.AsArray();
            samples.RemoveAt(samples.Count - 1);
            samples.RemoveAt(0);
        });
        Assert.Equal(0.5, vm.CurrentTimeSec);
        Assert.Equal(0.5, vm.ReplayStartTimeSec);
        Assert.Equal(9.5, vm.ReplayEndTimeSec);
    }

    [Fact]
    public void PlayPauseUsesTheExistingStage5Commands()
    {
        using var vm = MockReplayTestData.ViewModel();
        Assert.Same(vm.Playback.PlayPauseCommand, vm.PlayPauseCommand);
        vm.PlayPauseCommand.Execute(null);
        Assert.True(vm.IsPlaying);
        vm.AdvanceBy(0.25);
        Assert.Equal(0.25, vm.CurrentTimeSec);
        vm.PlayPauseCommand.Execute(null);
        vm.AdvanceBy(5);
        Assert.False(vm.IsPlaying);
        Assert.Equal(0.25, vm.CurrentTimeSec);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void PlaybackSpeedsAdvanceByRealElapsedTime(double speed)
    {
        using var vm = MockReplayTestData.ViewModel();
        vm.PlaybackSpeed = speed;
        vm.PlayPauseCommand.Execute(null);
        vm.AdvanceBy(2);
        Assert.Equal(2 * speed, vm.CurrentTimeSec);
    }

    [Fact]
    public void RestartAndSampleStepsHaveStage5Semantics()
    {
        using var vm = MockReplayTestData.ViewModel();
        vm.CurrentTimeSec = 2.25;
        vm.PlayPauseCommand.Execute(null);
        vm.StepForwardCommand.Execute(null);
        Assert.Equal(2.5, vm.CurrentTimeSec);
        Assert.False(vm.IsPlaying);
        vm.StepBackwardCommand.Execute(null);
        Assert.Equal(2, vm.CurrentTimeSec);
        vm.RestartCommand.Execute(null);
        Assert.Equal(vm.ReplayStartTimeSec, vm.CurrentTimeSec);
        Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void SeekingUsesActualInterpolatedStateAndPauses()
    {
        using var vm = MockReplayTestData.ViewModel();
        vm.PlayPauseCommand.Execute(null);
        vm.CurrentTimeSec = 3.25;
        var expected = new TelemetryReplayService(vm.Session.Telemetry).GetStateAtTime(3.25);
        Assert.Equal(expected, vm.CurrentReplayState);
        Assert.False(vm.IsPlaying);
        Assert.Equal("3.25 s", Assert.Single(vm.Telemetry, t => t.Label == "Time").Value);
        Assert.Equal("15.00°", Assert.Single(vm.Telemetry, t => t.Label == "Camera tilt").Value);
        Assert.Equal("100.00°", Assert.Single(vm.Telemetry, t => t.Label == "Horizontal FOV").Value);
    }

    [Fact]
    public void ReplayClampsAtEndAndNeverLoops()
    {
        using var vm = MockReplayTestData.ViewModel();
        vm.PlayPauseCommand.Execute(null);
        vm.AdvanceBy(100);
        Assert.Equal(vm.ReplayEndTimeSec, vm.CurrentTimeSec);
        Assert.False(vm.IsPlaying);
        Assert.False(vm.PlayPauseCommand.CanExecute(null));
        vm.AdvanceBy(100);
        Assert.Equal(vm.ReplayEndTimeSec, vm.CurrentTimeSec);
        vm.CurrentTimeSec = -100;
        Assert.Equal(vm.ReplayStartTimeSec, vm.CurrentTimeSec);
    }

    [Fact]
    public void CameraSwitchesPreserveTimeAndPlayingState()
    {
        using var vm = MockReplayTestData.ViewModel();
        vm.CurrentTimeSec = 4.25;
        vm.PlayPauseCommand.Execute(null);
        vm.SetThirdPersonModeCommand.Execute(null);
        Assert.Equal("Third Person", vm.CameraMode);
        Assert.True(vm.IsThirdPersonMode);
        Assert.False(vm.IsFpVMode);
        Assert.Equal(4.25, vm.CurrentTimeSec);
        Assert.True(vm.IsPlaying);
        vm.SetFpVModeCommand.Execute(null);
        Assert.Equal("FPV", vm.CameraMode);
        Assert.True(vm.IsFpVMode);
        Assert.Equal(4.25, vm.CurrentTimeSec);
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void VisibilityTogglesPreserveTimeAndPlayback()
    {
        using var vm = MockReplayTestData.ViewModel();
        Assert.True(vm.IsMinimapVisible);
        Assert.True(vm.IsEventLogVisible);
        vm.CurrentTimeSec = 2;
        vm.PlayPauseCommand.Execute(null);
        vm.ToggleMinimapCommand.Execute(null);
        vm.ToggleEventLogCommand.Execute(null);
        Assert.False(vm.IsMinimapVisible);
        Assert.False(vm.IsEventLogVisible);
        Assert.Equal("Show Minimap", vm.MinimapToggleText);
        Assert.Equal("Show Event Log", vm.EventLogToggleText);
        Assert.Equal(2, vm.CurrentTimeSec);
        Assert.True(vm.IsPlaying);
        vm.ToggleMinimapCommand.Execute(null);
        vm.ToggleEventLogCommand.Execute(null);
        Assert.True(vm.IsMinimapVisible);
        Assert.True(vm.IsEventLogVisible);
    }

    [Fact]
    public void MissingOptionalTelemetryDisplaysNA()
    {
        using var vm = MockReplayTestData.ViewModel(edit: document =>
        {
            foreach (var sample in document["telemetry"]!.AsArray())
            {
                sample!["cameraTiltDeg"] = null;
                sample["cameraFovDeg"] = null;
                sample["armed"] = null;
            }
        });
        vm.CurrentTimeSec = 0.25;
        Assert.Equal("N/A", Assert.Single(vm.Telemetry, t => t.Label == "Camera tilt").Value);
        Assert.Equal("N/A", Assert.Single(vm.Telemetry, t => t.Label == "Armed").Value);
        Assert.DoesNotContain(vm.Telemetry, t => string.IsNullOrWhiteSpace(t.Value));
    }

    [Fact]
    public void Version10RetainsAnalysisAnd2DReplayButReportsPreviewUnavailable()
    {
        using var vm = MockReplayTestData.ViewModel("valid_obstacle_course.json");
        Assert.False(vm.IsAvailable);
        Assert.Contains("Contract 1.1 replay context", vm.UnavailableMessage);
        Assert.True(vm.Playback.CanReplay);
        Assert.Equal(82.67, vm.Playback.Analysis.FinalScore);
    }

    [Fact]
    public void ReplayDoesNotMutateSessionAndDisposeStopsItsClockAdapter()
    {
        var request = MockReplayTestData.Request();
        var original = JsonSerializer.Serialize(request.Session);
        var vm = new SimulatorReplayMockViewModel(request.Session, request.Profile, request.Analysis);
        vm.PlayPauseCommand.Execute(null);
        vm.AdvanceBy(2);
        vm.SetThirdPersonModeCommand.Execute(null);
        vm.Dispose();
        Assert.False(vm.IsPlaying);
        vm.AdvanceBy(5);
        vm.CurrentTimeSec = 8;
        Assert.Equal(2, vm.CurrentTimeSec);
        Assert.Equal(original, JsonSerializer.Serialize(request.Session));
        using var reopened = new SimulatorReplayMockViewModel(request.Session, request.Profile, request.Analysis);
        Assert.Equal(reopened.ReplayStartTimeSec, reopened.CurrentTimeSec);
        Assert.Equal("FPV", reopened.CameraMode);
        Assert.False(reopened.IsPlaying);
    }

    [Fact]
    public void EventLogIsChronologicalMarksExactTimeAndHighlightsLatestStableTie()
    {
        using var vm = MockReplayTestData.ViewModel("replay_target_mission_v1_1.json", document =>
        {
            var events = document["events"]!.AsArray();
            var first = events[0]!.DeepClone();
            events.RemoveAt(0);
            events.Add(first);
        });
        Assert.Equal(vm.Session.Events.OrderBy(e => e.TimeSec).Select(e => e.EventId), vm.Events.Select(e => e.EventId));
        vm.CurrentTimeSec = 4;
        Assert.Contains(vm.Events, e => !e.HasOccurred && e.ProgressText == "Upcoming");
        vm.CurrentTimeSec = 8;
        Assert.All(vm.Events.Where(e => e.TimeSec <= 8), e => Assert.True(e.HasOccurred));
        Assert.All(vm.Events.Where(e => e.TimeSec > 8), e => Assert.False(e.HasOccurred));
        var latest = Assert.Single(vm.Events, e => e.IsCurrent);
        Assert.Equal("TargetDamageZone", latest.Display.EventType);
        Assert.Equal("Latest", latest.ProgressText);
        Assert.Contains("damageZone: central", latest.Display.Details);
        Assert.DoesNotContain("Dictionary", latest.Display.Details);
        vm.CurrentTimeSec = 0;
        Assert.DoesNotContain(vm.Events, e => e.HasOccurred || e.IsCurrent);
    }

    [Theory]
    [InlineData("replay_obstacle_course_v1_1.json", true)]
    [InlineData("replay_target_mission_v1_1.json", false)]
    public void MinimapUsesActualPathOptionalRouteAndProjectedActiveEntities(string file, bool route)
    {
        using var vm = MockReplayTestData.ViewModel(file);
        Assert.Equal(vm.Session.Telemetry.Select(s => new Point2D(s.X, s.Y)), vm.Minimap.Trajectory);
        Assert.Equal(route, vm.Minimap.HasRoute);
        var first = vm.Minimap.Project(vm.MinimapFrame, 300, 200);
        vm.CurrentTimeSec = 5;
        var later = vm.Minimap.Project(vm.MinimapFrame, 300, 200);
        Assert.NotEqual(first.Drone, later.Drone);
        Assert.NotEqual(Assert.Single(first.Entities).Position, Assert.Single(later.Entities).Position);
        var entity = Assert.Single(vm.DynamicEntities);
        var actual = Assert.Single(later.Entities);
        Assert.Equal(entity.EntityId, actual.EntityId);
        Assert.Equal(vm.Minimap.Projection(300, 200).Project(new(entity.Transform!.X, entity.Transform.Y)), actual.Position);
        Assert.InRange(actual.Position.X, 0, 300);
        Assert.InRange(actual.Position.Y, 0, 200);
        Assert.Same(vm.Minimap.Projection(300, 200), vm.Minimap.Projection(300, 200));
    }

    [Fact]
    public void InactiveOrNotYetRecordedEntitiesAreOmittedFromMinimapMarkers()
    {
        using var vm = MockReplayTestData.ViewModel(edit: document =>
        {
            var samples = document["replay"]!["entities"]![0]!["samples"]!.AsArray();
            samples.RemoveAt(0);
            samples[0]!["isActive"] = false;
        });
        Assert.Empty(vm.Minimap.Project(vm.MinimapFrame, 300, 200).Entities);
        vm.CurrentTimeSec = 2;
        Assert.Empty(vm.Minimap.Project(vm.MinimapFrame, 300, 200).Entities);
        vm.CurrentTimeSec = 4;
        Assert.Single(vm.Minimap.Project(vm.MinimapFrame, 300, 200).Entities);
    }
}
