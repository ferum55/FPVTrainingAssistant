using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.Data;

namespace FPVTrainingAssistant.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly ISessionImportService _importer;
    private readonly IExerciseProfileProvider _profiles;
    private readonly ISessionAnalyzer _analyzer;
    private readonly ISimulatorReplayLauncher _replayLauncher;
    private readonly ISessionRepository? _sessions;
    private readonly IDatabaseInitializer? _databaseInitializer;
    private readonly IApplicationSettingsService? _applicationSettings;
    private bool _persistenceAvailable;
    private string _storageMessage = "";
    private SessionSaveResult? _lastSaveResult;
    private readonly ReplayCapabilityService _replayCapabilities = new();
    private string? _sourceFilePath;
    private ReplayCapability? _replayCapability;
    private string _replayLaunchMessage = "";
    private ViewModelBase _currentViewModel;
    private bool _isBusy;
    private string _statusText = "Ready to import";
    private TrainingSession? _importedSession;
    private ExerciseProfile? _profile;
    private SessionAnalysisResult? _analysisResult;
    private SessionResultViewModel? _resultView;
    private TrajectoryViewModel? _trajectoryView;
    private IReadOnlyList<ValidationIssue> _validationWarnings = [];

    public MainViewModel(IFileDialogService fileDialog, ISessionImportService importer,
        IExerciseProfileProvider profiles, ISessionAnalyzer analyzer, ISimulatorReplayLauncher? replayLauncher = null,
        ISessionRepository? sessions = null, IHistoryRepository? history = null, IDatabaseInitializer? databaseInitializer = null,
        IExerciseProfileRepository? profileRepository = null, IApplicationSettingsService? applicationSettings = null)
    {
        _fileDialog = fileDialog;
        _importer = importer;
        _profiles = profiles;
        _analyzer = analyzer;
        _replayLauncher = replayLauncher ?? new MockSimulatorReplayLauncher();
        _sessions = sessions;
        _databaseInitializer = databaseInitializer;
        _applicationSettings=applicationSettings;
        _persistenceAvailable = databaseInitializer is null && sessions is not null && history is not null;
        ImportCommand = new(PickAndImportAsync, () => !IsBusy, _ => ShowUnexpectedFailure());
        ImportView = new(ImportCommand);
        _currentViewModel = ImportView;
        NavigateImportCommand = new(() => CurrentViewModel = ImportView, () => !IsBusy);
        NavigateResultCommand = new(() => CurrentViewModel = ResultView!, () => !IsBusy && HasResult);
        NavigateTrajectoryCommand = new(() => CurrentViewModel = TrajectoryView!, () => !IsBusy && HasTrajectory);
        Open3DReplayCommand = new(Open3DReplay, () => !IsBusy && HasResult && ImportedSession is not null);
        HistoryView = history is null ? null : new HistoryViewModel(history, OpenHistoricalAsync);
        NavigateHistoryCommand = new(async () => { CurrentViewModel = HistoryView!; await HistoryView!.RefreshAsync(); }, () => !IsBusy && PersistenceAvailable && HistoryView is not null);
        SettingsView=profileRepository is not null && applicationSettings is not null ? new SettingsViewModel(profileRepository,applicationSettings) : null;
        NavigateSettingsCommand=new(async()=> {CurrentViewModel=SettingsView!;await SettingsView!.RefreshAsync();},()=>!IsBusy && PersistenceAvailable && SettingsView is not null);
    }

    public ImportSessionViewModel ImportView { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public RelayCommand NavigateImportCommand { get; }
    public RelayCommand NavigateResultCommand { get; }
    public RelayCommand NavigateTrajectoryCommand { get; }
    public RelayCommand Open3DReplayCommand { get; }
    public AsyncRelayCommand NavigateHistoryCommand { get; }
    public HistoryViewModel? HistoryView { get; }
    public SettingsViewModel? SettingsView { get; }
    public AsyncRelayCommand NavigateSettingsCommand { get; }
    public bool PersistenceAvailable
    {
        get => _persistenceAvailable;
        private set { if (SetProperty(ref _persistenceAvailable, value)) {NavigateHistoryCommand.RaiseCanExecuteChanged();NavigateSettingsCommand.RaiseCanExecuteChanged();} }
    }
    public string StorageMessage { get => _storageMessage; private set { SetProperty(ref _storageMessage, value); OnPropertyChanged(nameof(HasStorageMessage)); } }
    public bool HasStorageMessage => !string.IsNullOrEmpty(StorageMessage);
    public SessionSaveResult? LastSaveResult { get => _lastSaveResult; private set => SetProperty(ref _lastSaveResult, value); }
    public string? SourceFilePath { get => _sourceFilePath; private set => SetProperty(ref _sourceFilePath, value); }
    public ReplayCapability? ReplayCapability { get => _replayCapability; private set => SetProperty(ref _replayCapability, value); }
    public string ReplayLaunchMessage
    {
        get => _replayLaunchMessage;
        private set
        {
            if (SetProperty(ref _replayLaunchMessage, value)) OnPropertyChanged(nameof(HasReplayLaunchMessage));
        }
    }
    public bool HasReplayLaunchMessage => !string.IsNullOrEmpty(ReplayLaunchMessage);
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel is TrajectoryViewModel trajectory && !ReferenceEquals(value, trajectory)) trajectory.Pause();
            if (!SetProperty(ref _currentViewModel, value)) return;
            OnPropertyChanged(nameof(ScreenTitle));
            OnPropertyChanged(nameof(IsImportScreen));
            OnPropertyChanged(nameof(IsResultScreen));
            OnPropertyChanged(nameof(IsTrajectoryScreen));
            OnPropertyChanged(nameof(IsHistoryScreen));
            OnPropertyChanged(nameof(IsSettingsScreen));
        }
    }
    public bool IsImportScreen => ReferenceEquals(CurrentViewModel, ImportView);
    public bool IsResultScreen => CurrentViewModel is SessionResultViewModel;
    public bool IsTrajectoryScreen => CurrentViewModel is TrajectoryViewModel;
    public bool IsHistoryScreen => CurrentViewModel is HistoryViewModel;
    public bool IsSettingsScreen=>CurrentViewModel is SettingsViewModel;
    public string ScreenTitle => IsImportScreen ? "Import session" : IsSettingsScreen ? "Settings" : IsHistoryScreen ? "History & progress" : IsTrajectoryScreen ? "Trajectory & replay" : "Session result";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            ImportView.IsBusy = value;
            ImportCommand.RaiseCanExecuteChanged();
            NavigateImportCommand.RaiseCanExecuteChanged();
            NavigateResultCommand.RaiseCanExecuteChanged();
            NavigateTrajectoryCommand.RaiseCanExecuteChanged();
            Open3DReplayCommand.RaiseCanExecuteChanged();
            NavigateHistoryCommand.RaiseCanExecuteChanged();
            NavigateSettingsCommand.RaiseCanExecuteChanged();
        }
    }
    public TrainingSession? ImportedSession { get => _importedSession; private set => SetProperty(ref _importedSession, value); }
    public ExerciseProfile? Profile { get => _profile; private set => SetProperty(ref _profile, value); }
    public SessionAnalysisResult? AnalysisResult { get => _analysisResult; private set => SetProperty(ref _analysisResult, value); }
    public IReadOnlyList<ValidationIssue> ValidationWarnings { get => _validationWarnings; private set => SetProperty(ref _validationWarnings, value); }
    public SessionResultViewModel? ResultView
    {
        get => _resultView;
        private set
        {
            if (!SetProperty(ref _resultView, value)) return;
            OnPropertyChanged(nameof(HasResult));
            NavigateResultCommand.RaiseCanExecuteChanged();
            Open3DReplayCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasResult => ResultView is not null;
    public TrajectoryViewModel? TrajectoryView
    {
        get => _trajectoryView;
        private set
        {
            _trajectoryView?.Pause();
            if (!SetProperty(ref _trajectoryView, value)) return;
            OnPropertyChanged(nameof(HasTrajectory));
            NavigateTrajectoryCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasTrajectory => TrajectoryView?.CanReplay == true;

    private async Task PickAndImportAsync()
    {
        var path = _fileDialog.ChooseSessionFile();
        if (path is not null) await ImportFileAsync(path);
    }

    public async Task ImportFileAsync(string filePath)
    {
        if (IsBusy) return;
        IsBusy = true;
        CurrentViewModel = ImportView;
        try
        {
            ImportView.Begin(filePath);
            LastSaveResult = null;
            ImportedSession = null;
            SourceFilePath = null;
            ReplayCapability = null;
            ReplayLaunchMessage = "";
            Profile = null;
            AnalysisResult = null;
            ResultView = null;
            TrajectoryView = null;
            ValidationWarnings = [];
            StatusText = "Importing session...";
            var import = await _importer.ImportAsync(filePath);
            ValidationWarnings = import.Validation.Warnings;
            if (!import.IsSuccess)
            {
                ImportView.Fail("Session could not be imported", "Review the details below and choose another file.", import.Validation.Issues,
                    import.TechnicalError is null ? null : "The file could not be read. Check that it is accessible and try again.");
                StatusText = "Import needs attention";
                return;
            }

            ImportedSession = import.Session!;
            SourceFilePath = import.SourceFilePath;
            RefreshReplayCapability();
            Profile = await _profiles.ResolveAsync(ImportedSession,PersistenceAvailable);
            if (Profile is null)
            {
                ImportView.Fail("Exercise profile unavailable", "No compatible exercise profile is available for this session. The profile ID, version and exercise type must match an available profile exactly.", ValidationWarnings);
                StatusText = "Compatible profile required";
                return;
            }

            StatusText = "Analyzing session...";
            var session = ImportedSession;
            var profile = Profile;
            AnalysisResult = await Task.Run(() => _analyzer.AnalyzeRequestedVersion(session, profile));
            var snapshot = new StoredSession(session, profile, AnalysisResult);
            if (PersistenceAvailable && _sessions is not null)
            {
                StatusText = "Saving session...";
                try { LastSaveResult = await _sessions.SaveAsync(snapshot); }
                catch { LastSaveResult = new(SessionSaveState.Failed, "The session was analyzed but could not be saved to history."); }
                StorageMessage = LastSaveResult.Message;
            }
            else if (_sessions is not null) StorageMessage = "The session was analyzed but could not be saved to history. History storage is unavailable.";
            ActivateSession(snapshot, "Imported session", SourceFilePath);
            StatusText = "Analysis complete";
        }
        catch (AnalysisConfigurationException exception)
        {
            ImportView.Fail("Analysis configuration error", exception.Message, ValidationWarnings);
            StatusText = "Analysis needs attention";
        }
        catch (Exception)
        {
            ShowUnexpectedFailure();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InitializePersistenceAsync()
    {
        if (_databaseInitializer is null) return;
        IsBusy = true; StatusText = "Opening history storage...";
        try
        {
            var result = await _databaseInitializer.InitializeAsync();
            PersistenceAvailable = result.IsSuccess;
            StorageMessage = result.IsSuccess ? "" : result.Message;
            if(result.IsSuccess && _applicationSettings is not null)
            {
                var preferences=await _applicationSettings.LoadAsync();
                if(!preferences.IsSuccess) StorageMessage=preferences.Message;
            }
        }
        catch { PersistenceAvailable = false; StorageMessage = "History storage is unavailable. Import and analysis remain available."; }
        finally { IsBusy = false; StatusText = "Ready to import"; }
    }

    private void ActivateSession(StoredSession snapshot, string origin, string? path)
    {
        ImportedSession = snapshot.Session; Profile = snapshot.Profile; AnalysisResult = snapshot.Analysis; SourceFilePath = path;
        RefreshReplayCapability();
        TrajectoryView = new(snapshot.Session, snapshot.Profile, snapshot.Analysis);
        TrajectoryView.PlaybackSpeed=_applicationSettings?.Current.DefaultReplaySpeed ?? 1;
        ResultView = new(snapshot.Session, snapshot.Profile, snapshot.Analysis, ValidationWarnings, NavigateTrajectoryCommand, Open3DReplayCommand, origin);
        CurrentViewModel = ResultView;
    }

    public async Task OpenHistoricalAsync(string sessionId)
    {
        if (IsBusy || !PersistenceAvailable || _sessions is null) return;
        IsBusy = true;
        try
        {
            var saved = await _sessions.LoadAsync(sessionId);
            if (saved is null) { StorageMessage = "History session could not be found."; return; }
            ValidationWarnings = []; ReplayLaunchMessage = ""; LastSaveResult = null;
            ActivateSession(saved, "Historical session", null);
            StorageMessage = "Opened saved history snapshot. Scores were not recalculated.";
            StatusText = "Historical session loaded";
        }
        catch { StorageMessage = "History session could not be loaded."; }
        finally { IsBusy = false; }
    }

    private void RefreshReplayCapability() => ReplayCapability =
        _replayCapabilities.Evaluate(ImportedSession, SourceFilePath, _replayLauncher.SimulatorExecutablePath);

    private void Open3DReplay()
    {
        try
        {
            RefreshReplayCapability();
            var available = _replayLauncher.IsMock ? ReplayCapability?.CanPreviewReplay == true : ReplayCapability?.CanLaunchExternalSimulator == true;
            if (!available)
            {
                ReplayLaunchMessage = (_replayLauncher.IsMock ? ReplayCapability?.PreviewLimitations : ReplayCapability?.Limitations)?.FirstOrDefault() ?? "3D replay is unavailable.";
                return;
            }
            if (!_replayLauncher.IsMock) TrajectoryView?.Pause();
            ReplayLaunchMessage = _replayLauncher.Launch(new ReplayLaunchRequest(ImportedSession!, Profile!, AnalysisResult!, SourceFilePath)).Message;
        }
        catch (Exception)
        {
            ReplayLaunchMessage = _replayLauncher.IsMock ? "The replay prototype could not be opened. Try again."
                : "The simulator could not be started. Check the executable configuration and try again.";
        }
    }

    private void ShowUnexpectedFailure()
    {
        CurrentViewModel = ImportView;
        ImportView.Fail("Session could not be reviewed", "The operation could not be completed. Choose another file or try again.", ValidationWarnings,
            "An unexpected processing error occurred. No result was saved.");
        StatusText = "Review needs attention";
    }
}
