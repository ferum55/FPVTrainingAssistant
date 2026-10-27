using System.Globalization;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed class TrajectoryViewModel : ViewModelBase
{
    private readonly TelemetryReplayService _replay;
    private readonly double[] _eventTimes;
    private readonly EventViewModel[] _events;
    private double _currentTimeSec = double.NaN;
    private double _playbackSpeed = 1;
    private bool _isPlaying;
    private ReplayState? _currentState;
    private IReadOnlyList<EventViewModel> _recentEvents = [];
    private int _recentStart = -1, _recentEnd = -1;

    public TrajectoryViewModel(TrainingSession session, ExerciseProfile profile, SessionAnalysisResult analysis, RouteZoneVisualizationService? visualZones = null)
    {
        Session = session; Profile = profile; Analysis = analysis;
        _replay = new(session.Telemetry);
        TrajectoryPoints = Array.AsReadOnly(session.Telemetry.Select(s => new Point2D(s.X, s.Y)).ToArray());
        ReferencePoints = Array.AsReadOnly(profile.ReferencePoints.OrderBy(p => p.SequenceNo).ToArray());
        ZoneSegments = (visualZones ?? new RouteZoneVisualizationService()).Create(ReferencePoints);
        RouteVisualBounds = RouteZoneVisualizationService.BoundsOf(ZoneSegments);
        HasRoute = ZoneSegments.Count > 0;
        var events = session.Events.OrderBy(e => e.TimeSec).ToArray();
        _eventTimes = events.Select(e => e.TimeSec).ToArray();
        _events = events.Select(e => new EventViewModel(e)).ToArray();
        PlayPauseCommand = new(() => { if (IsPlaying) Pause(); else IsPlaying = true; }, () => CanReplay && (IsPlaying || CurrentTimeSec < ReplayEndTimeSec));
        RestartCommand = new(() => { Pause(); UpdateTime(ReplayStartTimeSec); }, () => HasTelemetry);
        StepForwardCommand = new(() => { Pause(); UpdateTime(_replay.NextSampleTime(CurrentTimeSec)); }, () => CanReplay);
        StepBackwardCommand = new(() => { Pause(); UpdateTime(_replay.PreviousSampleTime(CurrentTimeSec)); }, () => CanReplay);
        UpdateTime(ReplayStartTimeSec);
    }

    public TrainingSession Session { get; }
    public ExerciseProfile Profile { get; }
    public SessionAnalysisResult Analysis { get; }
    public string SessionTitle => DisplayText.Name(Session.ExerciseType) + " · " + Session.SessionId;
    public string SessionDurationText => Session.Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s session";
    public IReadOnlyList<Point2D> TrajectoryPoints { get; }
    public IReadOnlyList<ReferencePoint> ReferencePoints { get; }
    public IReadOnlyList<RouteZoneSegment> ZoneSegments { get; }
    public IReadOnlyList<Point2D> RouteVisualBounds { get; }
    public bool HasRoute { get; }
    public bool HasNoRoute => !HasRoute;
    public bool HasTelemetry => _replay.Count > 0;
    public bool HasNoTelemetry => !HasTelemetry;
    public bool CanReplay => _replay.Count >= 2 && ReplayEndTimeSec > ReplayStartTimeSec;
    public string TelemetryStatus => CanReplay ? "" : HasTelemetry ? "One telemetry sample is available; playback requires at least two." : "No telemetry is available for replay.";
    public bool HasReplayNotice => !CanReplay;
    public double ReplayStartTimeSec => _replay.StartTimeSec;
    public double ReplayEndTimeSec => _replay.EndTimeSec;
    public IReadOnlyList<double> PlaybackSpeeds { get; } = Array.AsReadOnly(new[] { 0.5, 1, 2 });
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (value is not (0.5 or 1 or 2)) throw new ArgumentOutOfRangeException(nameof(value), "Playback speed must be 0.5x, 1x or 2x.");
            SetProperty(ref _playbackSpeed, value);
        }
    }
    public double CurrentTimeSec
    {
        get => _currentTimeSec;
        set { if (!double.IsFinite(value)) return; Pause(); UpdateTime(value); }
    }
    public string TimelineText => DisplayText.Number(CurrentTimeSec) + " / " + DisplayText.Number(ReplayEndTimeSec) + " s";
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (!SetProperty(ref _isPlaying, value)) return;
            OnPropertyChanged(nameof(PlayPauseText));
            PlayPauseCommand.RaiseCanExecuteChanged();
        }
    }
    public string PlayPauseText => IsPlaying ? "Pause" : "Play";
    public ReplayState? CurrentState { get => _currentState; private set => SetProperty(ref _currentState, value); }
    public IReadOnlyList<EventViewModel> RecentEvents { get => _recentEvents; private set { SetProperty(ref _recentEvents, value); OnPropertyChanged(nameof(HasNoRecentEvents)); } }
    public bool HasNoRecentEvents => RecentEvents.Count == 0;
    public RouteAnalysisResult? RouteMetrics => Analysis.RouteAnalysis;
    public bool HasRouteMetrics => RouteMetrics is not null;
    public string GreenTimeText => Metric(RouteMetrics?.GreenTimePercent, " %");
    public string YellowTimeText => Metric(RouteMetrics?.YellowTimePercent, " %");
    public string RedTimeText => Metric(RouteMetrics?.RedTimePercent, " %");
    public string AverageDeviationText => Metric(RouteMetrics?.AverageDeviationM, " m");
    public string MaximumDeviationText => Metric(RouteMetrics?.MaximumDeviationM, " m");
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand StepForwardCommand { get; }
    public RelayCommand StepBackwardCommand { get; }

    public void Pause() => IsPlaying = false;

    public void AdvanceBy(double elapsedRealSeconds)
    {
        if (!double.IsFinite(elapsedRealSeconds) || elapsedRealSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedRealSeconds));
        if (IsPlaying) UpdateTime(Math.Min(ReplayEndTimeSec, CurrentTimeSec + elapsedRealSeconds * PlaybackSpeed));
    }

    private void UpdateTime(double time)
    {
        var clamped = HasTelemetry ? Math.Clamp(time, ReplayStartTimeSec, ReplayEndTimeSec) : 0;
        if (!SetProperty(ref _currentTimeSec, clamped, nameof(CurrentTimeSec))) return;
        CurrentState = _replay.GetStateAtTime(clamped);
        OnPropertyChanged(nameof(TimelineText));
        var start = EventBound(clamped - 2, upper: false);
        var end = EventBound(clamped, upper: true);
        if (start != _recentStart || end != _recentEnd)
        {
            _recentStart = start; _recentEnd = end;
            RecentEvents = Array.AsReadOnly(_events[start..end]);
        }
        if (clamped >= ReplayEndTimeSec) Pause();
        PlayPauseCommand.RaiseCanExecuteChanged();
    }

    private int EventBound(double time, bool upper)
    {
        var low = 0; var high = _eventTimes.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_eventTimes[middle] < time || (upper && _eventTimes[middle] == time)) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private static string Metric(double? value, string unit) => value is { } number ? DisplayText.Number(number) + unit : "Not available";
}
