using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class TrajectoryViewModelTests
{
    [Fact]
    public void StartsAtFirstActualSampleAndUsesSampleSteps()
    {
        var vm = Create();
        Assert.Equal(2, vm.CurrentTimeSec);
        Assert.Equal(2, vm.ReplayStartTimeSec); Assert.Equal(10, vm.ReplayEndTimeSec);
        vm.StepBackwardCommand.Execute(null); Assert.Equal(2, vm.CurrentTimeSec);
        vm.StepForwardCommand.Execute(null); Assert.Equal(5, vm.CurrentTimeSec);
        vm.StepForwardCommand.Execute(null); Assert.Equal(10, vm.CurrentTimeSec);
        vm.StepForwardCommand.Execute(null); Assert.Equal(10, vm.CurrentTimeSec);
        vm.StepBackwardCommand.Execute(null); Assert.Equal(5, vm.CurrentTimeSec);
        vm.RestartCommand.Execute(null); Assert.Equal(2, vm.CurrentTimeSec); Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void SeekingInterpolatesStateAndPauses()
    {
        var vm = Create();
        vm.PlayPauseCommand.Execute(null);
        vm.CurrentTimeSec = 3.5;
        Assert.False(vm.IsPlaying);
        Assert.Equal(5, vm.CurrentState!.X);
        vm.StepForwardCommand.Execute(null); Assert.Equal(5, vm.CurrentTimeSec);
        vm.CurrentTimeSec = 3.5;
        vm.StepBackwardCommand.Execute(null); Assert.Equal(2, vm.CurrentTimeSec);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void ElapsedTimeAndSpeedDrivePlaybackWithoutSleeps(double speed)
    {
        var vm = Create();
        vm.PlaybackSpeed = speed;
        Assert.Equal(2, vm.CurrentTimeSec);
        vm.PlayPauseCommand.Execute(null); vm.AdvanceBy(1);
        Assert.Equal(2 + speed, vm.CurrentTimeSec);
        vm.Pause(); vm.AdvanceBy(2); Assert.Equal(2 + speed, vm.CurrentTimeSec);
        vm.PlayPauseCommand.Execute(null); vm.AdvanceBy(100);
        Assert.Equal(10, vm.CurrentTimeSec); Assert.False(vm.IsPlaying);
        Assert.False(vm.PlayPauseCommand.CanExecute(null));
        vm.RestartCommand.Execute(null); Assert.Equal(2, vm.CurrentTimeSec); Assert.False(vm.IsPlaying);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(double.NaN)]
    public void InvalidSpeedIsRejected(double speed) => Assert.Throws<ArgumentOutOfRangeException>(() => Create().PlaybackSpeed = speed);

    [Fact]
    public void RecentEventsIncludeOnlyPreviousTwoSecondsWithoutFutureEvents()
    {
        var vm = Create(events: [Event("Before", 2), Event("AtStart", 3), Event("Now", 5), Event("Future", 5.1)]);
        vm.CurrentTimeSec = 5;
        Assert.Equal(new[] { "AtStart", "Now" }, vm.RecentEvents.Select(e => e.EventType));
        vm.CurrentTimeSec = 10; Assert.True(vm.HasNoRecentEvents);
    }

    [Fact]
    public void RouteMetricsAreTheOriginalAnalysisObject()
    {
        var route = new RouteAnalysisResult(20, 30, 50, 2.5, 8, 8);
        var vm = Create(route: route);
        Assert.Same(route, vm.RouteMetrics);
        Assert.Equal("20.00%", vm.GreenTimeText);
        Assert.Equal("2.50 m", vm.AverageDeviationText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EmptyOrSingleSampleHasSafePresentationButNoPlayback(int count)
    {
        var vm = Create(samples: new[] { Sample(7, 3) }.Take(count).ToArray());
        Assert.False(vm.CanReplay); Assert.True(vm.HasReplayNotice);
        Assert.False(vm.PlayPauseCommand.CanExecute(null));
        Assert.Equal(count == 0 ? 0 : 7, vm.CurrentTimeSec);
        Assert.Equal(count == 0, vm.CurrentState is null);
    }

    [Fact]
    public void NoRouteDoesNotPreventReplay()
    {
        var vm = Create();
        Assert.True(vm.HasNoRoute); Assert.False(vm.HasRouteMetrics); Assert.True(vm.CanReplay);
        Assert.Empty(vm.ZoneSegments);
    }

    [Fact]
    public async Task NewImportResetsGeometryTimeAndPlaybackAndNavigationPreservesSession()
    {
        var main = new MainViewModel(new NoDialog(), new SessionImportService(), new ExerciseProfileProvider(), new SessionAnalyzer());
        Assert.False(main.NavigateTrajectoryCommand.CanExecute(null));
        await main.ImportFileAsync(Fixture("valid_obstacle_course.json"));
        var old = main.TrajectoryView!;
        Assert.True(main.NavigateTrajectoryCommand.CanExecute(null));
        main.ResultView!.ViewTrajectoryCommand.Execute(null);
        Assert.Same(old, main.CurrentViewModel);
        old.CurrentTimeSec = 5;
        old.PlayPauseCommand.Execute(null);
        var session = main.ImportedSession;
        main.NavigateResultCommand.Execute(null);
        Assert.False(old.IsPlaying); Assert.Same(session, main.ImportedSession);
        await main.ImportFileAsync(Fixture("valid_target_mission.json"));
        Assert.NotSame(old, main.TrajectoryView);
        Assert.Equal(main.TrajectoryView!.ReplayStartTimeSec, main.TrajectoryView.CurrentTimeSec);
        Assert.False(main.TrajectoryView.IsPlaying); Assert.True(main.TrajectoryView.HasNoRoute);
        Assert.Empty(main.TrajectoryView.ReferencePoints);
    }

    [Fact]
    public void ZoneSubdivisionInterpolatesRadiiAndUsesOnlyVisualRedMargin()
    {
        var zones = new RouteZoneVisualizationService(subdivisions: 2).Create(
            [Point(0, 0, green: 1, yellow: 2), Point(1, 10, green: 3, yellow: 6)]);
        Assert.Equal(2, zones.Count);
        Assert.Equal(1.5, zones[0].GreenRadiusM);
        Assert.Equal(3, zones[0].YellowRadiusM);
        Assert.Equal(5, zones[0].RedRadiusM);
        Assert.Equal(new Point2D(5, 0), zones[0].End);
        Assert.Equal(7.5, zones[1].RedRadiusM);
    }

    [Fact]
    public void ConfiguredVisualMarginIsIncludedInProjectionBounds()
    {
        var zones = new RouteZoneVisualizationService(minimumRedMarginM: 9).Create([Point(0, 0), Point(1, 10)]);
        var bounds = RouteZoneVisualizationService.BoundsOf(zones);
        var projection = new TrajectoryProjectionService().Create([], bounds, 400, 300, 20);
        Assert.Equal(-12, projection.MinX);
        Assert.Equal(22, projection.MaxX);
        Assert.Equal(-12, projection.MinY);
        Assert.Equal(12, projection.MaxY);
    }

    private static TrajectoryViewModel Create(IReadOnlyList<TelemetrySample>? samples = null, List<SessionEvent>? events = null, RouteAnalysisResult? route = null)
    {
        var session = Session(ExerciseTypes.TargetMission, events: events, telemetry: samples ?? [Sample(2), Sample(5, 10), Sample(10, 20)]);
        var profile = Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.MissionSuccess)]);
        var analyzed = new SessionAnalyzer().Analyze(session, profile);
        var result = new SessionAnalysisResult
        {
            SessionId = analyzed.SessionId, ProfileId = analyzed.ProfileId, ProfileVersion = analyzed.ProfileVersion,
            FinalScore = analyzed.FinalScore, Metrics = analyzed.Metrics, CriterionResults = analyzed.CriterionResults,
            Mistakes = analyzed.Mistakes, Recommendations = analyzed.Recommendations, IsSuccessful = true, RouteAnalysis = route
        };
        return new(session, profile, result);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "TestSessions", name);
    private sealed class NoDialog : IFileDialogService { public string? ChooseSessionFile() => null; }
}
