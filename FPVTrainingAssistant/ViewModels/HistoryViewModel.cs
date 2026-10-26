using FPVTrainingAssistant.Data;

namespace FPVTrainingAssistant.ViewModels;

public sealed record ScoreProgressPoint(string SessionId, DateTimeOffset StartedAtUtc, double Score);

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly IHistoryRepository _repository;
    private IReadOnlyList<OperatorHistory> _operators = [];
    private IReadOnlyList<SessionHistoryEntry> _sessions = [];
    private OperatorHistory? _selectedOperator;
    private SessionHistoryEntry? _selectedSession;
    private string _selectedExercise = "All exercises", _displayName = "", _notes = "", _message = "";
    private bool _isBusy;
    private int _generation;

    public HistoryViewModel(IHistoryRepository repository, Func<string, Task> openResult)
    {
        _repository = repository;
        RefreshCommand = new(RefreshAsync, () => !IsBusy, _ => Message = "History could not be loaded.");
        SaveOperatorCommand = new(SaveOperatorAsync, () => !IsBusy && SelectedOperator is not null, _ => Message = "Operator details could not be updated.");
        OpenResultCommand = new(async () =>
        {
            if (SelectedSession is null) return;
            IsBusy = true;
            try { await openResult(SelectedSession.SessionId); }
            catch { Message = "History session could not be opened."; }
            finally { IsBusy = false; }
        }, () => !IsBusy && SelectedSession is not null);
    }

    public IReadOnlyList<OperatorHistory> Operators { get => _operators; private set { SetProperty(ref _operators, value); OnPropertyChanged(nameof(HasOperators)); OnPropertyChanged(nameof(EmptyMessage)); } }
    public IReadOnlyList<SessionHistoryEntry> Sessions
    {
        get => _sessions;
        private set
        {
            SetProperty(ref _sessions, value);
            foreach (var name in new[] { nameof(HasSessions), nameof(HasNoSessions), nameof(EmptyMessage), nameof(SessionCountText), nameof(AverageScoreText), nameof(BestScoreText), nameof(LatestScoreText), nameof(ProgressPoints) }) OnPropertyChanged(name);
        }
    }
    public OperatorHistory? SelectedOperator
    {
        get => _selectedOperator;
        set
        {
            if (!SetProperty(ref _selectedOperator, value)) return;
            DisplayName = value?.Profile.DisplayName ?? "";
            Notes = value?.Profile.Notes ?? "";
            OnPropertyChanged(nameof(SelectedOperatorId));
            SaveOperatorCommand.RaiseCanExecuteChanged();
            SelectionLoad = LoadSessionsAsync();
        }
    }
    public string SelectedOperatorId => SelectedOperator?.Profile.OperatorId ?? "";
    public SessionHistoryEntry? SelectedSession { get => _selectedSession; set { if (SetProperty(ref _selectedSession, value)) OpenResultCommand.RaiseCanExecuteChanged(); } }
    public IReadOnlyList<string> ExerciseFilters { get; } = ["All exercises", "ObstacleCourse", "TargetMission"];
    public string SelectedExercise { get => _selectedExercise; set { if (SetProperty(ref _selectedExercise, value)) SelectionLoad = LoadSessionsAsync(); } }
    public Task SelectionLoad { get; private set; } = Task.CompletedTask;
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string Message { get => _message; private set { SetProperty(ref _message, value); OnPropertyChanged(nameof(HasMessage)); } }
    public bool HasMessage => !string.IsNullOrEmpty(Message);
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanInteract));
            RefreshCommand.RaiseCanExecuteChanged(); SaveOperatorCommand.RaiseCanExecuteChanged(); OpenResultCommand.RaiseCanExecuteChanged();
        }
    }
    public bool CanInteract => !IsBusy;
    public bool HasOperators => Operators.Count > 0;
    public bool HasSessions => Sessions.Count > 0;
    public bool HasNoSessions => !HasSessions;
    public string EmptyMessage => !HasOperators ? "No training history is available yet." : SelectedExercise != "All exercises" ? "No sessions match the selected exercise filter." : "No sessions are available for this operator.";
    public string SessionCountText => Sessions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string AverageScoreText => HasSessions ? DisplayText.Number(Sessions.Average(s => s.AssistantScore)) : "N/A";
    public string BestScoreText => HasSessions ? DisplayText.Number(Sessions.Max(s => s.AssistantScore)) : "N/A";
    public string LatestScoreText => HasSessions ? DisplayText.Number(Sessions[0].AssistantScore) : "N/A";
    public IReadOnlyList<ScoreProgressPoint> ProgressPoints => Sessions.OrderBy(s => s.StartedAtUtc).ThenBy(s => s.SessionId, StringComparer.Ordinal)
        .Select(s => new ScoreProgressPoint(s.SessionId, s.StartedAtUtc, s.AssistantScore)).ToArray();
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SaveOperatorCommand { get; }
    public AsyncRelayCommand OpenResultCommand { get; }

    public async Task RefreshAsync()
    {
        IsBusy = true; Message = "";
        try
        {
            var id = SelectedOperatorId;
            Operators = await _repository.ListOperatorsAsync();
            var selected = Operators.FirstOrDefault(o => o.Profile.OperatorId == id) ?? Operators.FirstOrDefault();
            if (ReferenceEquals(selected, SelectedOperator)) SelectionLoad = LoadSessionsAsync();
            else SelectedOperator = selected;
            await SelectionLoad;
        }
        catch { Message = "History could not be loaded."; }
        finally { IsBusy = false; }
    }

    private async Task LoadSessionsAsync()
    {
        var generation = ++_generation;
        var id = SelectedOperator?.Profile.OperatorId;
        var exercise = SelectedExercise == "All exercises" ? null : SelectedExercise;
        IsBusy = true; Message = ""; SelectedSession = null; Sessions = [];
        try
        {
            var sessions = id is null ? Array.Empty<SessionHistoryEntry>() : await _repository.ListSessionsAsync(id, exercise);
            if (generation != _generation) return;
            Sessions = sessions; SelectedSession = Sessions.FirstOrDefault();
        }
        catch { if (generation == _generation) Message = "History could not be loaded."; }
        finally { if (generation == _generation) IsBusy = false; }
    }

    private async Task SaveOperatorAsync()
    {
        if (SelectedOperator is null) return;
        var id = SelectedOperator.Profile.OperatorId;
        IsBusy = true;
        try
        {
            if (!await _repository.UpdateOperatorAsync(id, DisplayName, Notes)) { Message = "Operator details could not be updated."; return; }
            await RefreshAsync();
            Message = "Operator details saved.";
        }
        catch { Message = "Operator details could not be updated."; }
        finally { IsBusy = false; }
    }
}
