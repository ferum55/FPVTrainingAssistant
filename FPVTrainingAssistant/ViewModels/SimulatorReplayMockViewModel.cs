using System.ComponentModel;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed record TelemetryReadout(string Label, string Value);

public sealed class SimulatorReplayMockViewModel : ViewModelBase, IDisposable
{
    private readonly ReplayEntityService[] _entityReplay;
    private string _cameraMode = "FPV";
    private bool _isMinimapVisible = true, _isEventLogVisible = true, _disposed;
    private IReadOnlyList<EntityReplayState> _dynamicEntities = [];
    private IReadOnlyList<TelemetryReadout> _telemetry = [];
    private ReplayMinimapFrame _minimapFrame = new(null, []);

    public SimulatorReplayMockViewModel(TrainingSession session, ExerciseProfile profile, SessionAnalysisResult analysis)
    {
        Session = session;
        var capability = new ReplayCapabilityService().Evaluate(session, null, null);
        IsAvailable = capability.CanPreviewReplay;
        UnavailableMessage = string.Join(" ", capability.PreviewLimitations);
        Playback = new(session, profile, analysis);
        Minimap = new(session, profile);
        _entityReplay = (session.Replay?.Entities ?? []).Select(t => new ReplayEntityService(t)).ToArray();
        Events = Array.AsReadOnly(session.Events.OrderBy(e => e.TimeSec).Select(e => new ReplayEventViewModel(e)).ToArray());
        SetFpVModeCommand = new(() => CameraMode = "FPV");
        SetThirdPersonModeCommand = new(() => CameraMode = "Third Person");
        ToggleMinimapCommand = new(() => IsMinimapVisible = !IsMinimapVisible);
        ToggleEventLogCommand = new(() => IsEventLogVisible = !IsEventLogVisible);
        Playback.PropertyChanged += PlaybackChanged;
        UpdateFrame();
    }

    public TrainingSession Session { get; }
    // Composition reuses Stage 5 playback commands, timeline semantics and TelemetryReplayService.
    public TrajectoryViewModel Playback { get; }
    public ReplayMinimapViewModel Minimap { get; }
    public bool IsAvailable { get; }
    public string UnavailableMessage { get; }
    public string SessionId => Session.SessionId;
    public string EnvironmentId => Session.Replay?.EnvironmentId ?? "N/A";
    public string ScenarioId => Session.Replay?.ScenarioId ?? "N/A";
    public string ExerciseType => DisplayText.Name(Session.ExerciseType);
    public string SourceSimulator => Session.SourceSimulator;
    public string DataSummary => $"Contract {Session.ContractVersion}  ·  Telemetry samples: {Session.Telemetry.Count}  ·  Events: {Session.Events.Count}  ·  Dynamic entities: {_entityReplay.Length}";
    public string StatusText => IsAvailable ? "Replay data loaded · Mock integration preview" : UnavailableMessage;
    public double CurrentTimeSec { get => Playback.CurrentTimeSec; set { if (!_disposed) Playback.CurrentTimeSec = value; } }
    public double ReplayStartTimeSec => Playback.ReplayStartTimeSec;
    public double ReplayEndTimeSec => Playback.ReplayEndTimeSec;
    public ReplayState? CurrentReplayState => Playback.CurrentState;
    public bool IsPlaying => Playback.IsPlaying;
    public double PlaybackSpeed { get => Playback.PlaybackSpeed; set => Playback.PlaybackSpeed = value; }
    public IReadOnlyList<double> PlaybackSpeeds => Playback.PlaybackSpeeds;
    public string TimelineText => Playback.TimelineText;
    public string PlayPauseText => Playback.PlayPauseText;
    public double HeadingRotation => -(CurrentReplayState?.YawDeg ?? 0);
    public string CameraMode
    {
        get => _cameraMode;
        private set
        {
            if (!SetProperty(ref _cameraMode, value)) return;
            OnPropertyChanged(nameof(IsFpVMode)); OnPropertyChanged(nameof(IsThirdPersonMode));
        }
    }
    public bool IsFpVMode => CameraMode == "FPV";
    public bool IsThirdPersonMode => !IsFpVMode;
    public bool IsMinimapVisible { get => _isMinimapVisible; private set { if (SetProperty(ref _isMinimapVisible, value)) OnPropertyChanged(nameof(MinimapToggleText)); } }
    public bool IsEventLogVisible { get => _isEventLogVisible; private set { if (SetProperty(ref _isEventLogVisible, value)) OnPropertyChanged(nameof(EventLogToggleText)); } }
    public string MinimapToggleText => IsMinimapVisible ? "Hide Minimap" : "Show Minimap";
    public string EventLogToggleText => IsEventLogVisible ? "Hide Event Log" : "Show Event Log";
    public IReadOnlyList<ReplayEventViewModel> Events { get; }
    public bool HasNoEvents => Events.Count == 0;
    public IReadOnlyList<EntityReplayState> DynamicEntities { get => _dynamicEntities; private set => SetProperty(ref _dynamicEntities, value); }
    public ReplayMinimapFrame MinimapFrame { get => _minimapFrame; private set => SetProperty(ref _minimapFrame, value); }
    public IReadOnlyList<TelemetryReadout> Telemetry { get => _telemetry; private set => SetProperty(ref _telemetry, value); }
    public string FlightHud => "MODE  " + (CurrentReplayState?.FlightMode ?? "N/A") + "\nARMED  " + Boolean(CurrentReplayState?.Armed);
    public string BatteryHud => "BAT  " + Number(CurrentReplayState?.BatteryPercent, " %") + "\nPACK  " + Number(CurrentReplayState?.BatteryVoltageV, " V");
    public string LinkHud => "LQ  " + Number(CurrentReplayState?.ControlLinkQualityPercent, " %") + "\nRSSI  " + Number(CurrentReplayState?.ControlRssiDbm, " dBm");
    public string MotionHud => "SPEED  " + Number(CurrentReplayState?.SpeedMps, " m/s") + "\nALT  " + Number(CurrentReplayState?.AltitudeM, " m");
    public string PositionText => $"X {Number(CurrentReplayState?.X)}   Y {Number(CurrentReplayState?.Y)}   Z {Number(CurrentReplayState?.Z)} m";
    public string AttitudeText => $"Pitch {Number(CurrentReplayState?.PitchDeg)}°   Roll {Number(CurrentReplayState?.RollDeg)}°   Yaw {Number(CurrentReplayState?.YawDeg)}°";
    public string CameraText => "Tilt " + Number(CurrentReplayState?.CameraTiltDeg, "°") + "  ·  Horizontal FOV " + Number(CurrentReplayState?.CameraFovDeg, "°");
    public RelayCommand PlayPauseCommand => Playback.PlayPauseCommand;
    public RelayCommand RestartCommand => Playback.RestartCommand;
    public RelayCommand StepForwardCommand => Playback.StepForwardCommand;
    public RelayCommand StepBackwardCommand => Playback.StepBackwardCommand;
    public RelayCommand SetFpVModeCommand { get; }
    public RelayCommand SetThirdPersonModeCommand { get; }
    public RelayCommand ToggleMinimapCommand { get; }
    public RelayCommand ToggleEventLogCommand { get; }

    public void AdvanceBy(double seconds) { if (!_disposed && IsAvailable) Playback.AdvanceBy(seconds); }
    public void Pause() => Playback.Pause();

    private void PlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrajectoryViewModel.CurrentState)) UpdateFrame();
        OnPropertyChanged(e.PropertyName == nameof(TrajectoryViewModel.CurrentState) ? nameof(CurrentReplayState) : e.PropertyName);
    }

    private void UpdateFrame()
    {
        DynamicEntities = Array.AsReadOnly(_entityReplay.Select(e => e.GetStateAtTime(CurrentTimeSec)).ToArray());
        MinimapFrame = new(CurrentReplayState, DynamicEntities);
        var latest = -1;
        for (var i = 0; i < Events.Count; i++) if (Events[i].TimeSec <= CurrentTimeSec) latest = i;
        for (var i = 0; i < Events.Count; i++) Events[i].Update(CurrentTimeSec, i == latest);
        var s = CurrentReplayState;
        Telemetry = Array.AsReadOnly(new[]
        {
            new TelemetryReadout("Time", Number(s?.TimeSec, " s")),
            new("X / Y / Z", PositionText), new("Speed", Number(s?.SpeedMps, " m/s")),
            new("Vertical speed", Number(s?.VerticalSpeedMps, " m/s")), new("Altitude", Number(s?.AltitudeM, " m")),
            new("Pitch / Roll / Yaw", AttitudeText), new("Camera tilt", Number(s?.CameraTiltDeg, "°")),
            new("Horizontal FOV", Number(s?.CameraFovDeg, "°")), new("Battery", Number(s?.BatteryPercent, " %")),
            new("Pack / Cell", Number(s?.BatteryVoltageV, " V") + " / " + Number(s?.CellVoltageV, " V")),
            new("Current", Number(s?.CurrentA, " A")), new("Consumed", Number(s?.ConsumedMah, " mAh")),
            new("Control LQ", Number(s?.ControlLinkQualityPercent, " %")), new("RSSI", Number(s?.ControlRssiDbm, " dBm")),
            new("Failsafe", Boolean(s?.Failsafe)), new("Video LQ", Number(s?.VideoLinkQualityPercent, " %")),
            new("Flight mode", s?.FlightMode ?? "N/A"), new("Armed", Boolean(s?.Armed))
        });
        foreach (var name in new[] { nameof(FlightHud), nameof(BatteryHud), nameof(LinkHud), nameof(MotionHud), nameof(PositionText), nameof(AttitudeText), nameof(CameraText), nameof(HeadingRotation) })
            OnPropertyChanged(name);
    }

    private static string Number(double? value, string unit = "") => value is { } n ? DisplayText.Number(n) + unit : "N/A";
    private static string Boolean(bool? value) => value is { } b ? b ? "Yes" : "No" : "N/A";

    public void Dispose()
    {
        if (_disposed) return;
        Pause();
        Playback.PropertyChanged -= PlaybackChanged;
        _disposed = true;
    }
}
