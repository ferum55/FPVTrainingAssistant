using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly ISessionImportService _importer;
    private readonly IExerciseProfileProvider _profiles;
    private readonly ISessionAnalyzer _analyzer;
    private readonly ISimulatorReplayLauncher _replayLauncher;
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
        IExerciseProfileProvider profiles, ISessionAnalyzer analyzer, ISimulatorReplayLauncher? replayLauncher = null)
    {
        _fileDialog = fileDialog;
        _importer = importer;
        _profiles = profiles;
        _analyzer = analyzer;
        _replayLauncher = replayLauncher ?? new SimulatorReplayLauncher();
        ImportCommand = new(PickAndImportAsync, () => !IsBusy, _ => ShowUnexpectedFailure());
        ImportView = new(ImportCommand);
        _currentViewModel = ImportView;
        NavigateImportCommand = new(() => CurrentViewModel = ImportView, () => !IsBusy);
        NavigateResultCommand = new(() => CurrentViewModel = ResultView!, () => !IsBusy && HasResult);
        NavigateTrajectoryCommand = new(() => CurrentViewModel = TrajectoryView!, () => !IsBusy && HasTrajectory);
        Open3DReplayCommand = new(Open3DReplay, () => !IsBusy && HasResult && ImportedSession is not null);
    }

    public ImportSessionViewModel ImportView { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public RelayCommand NavigateImportCommand { get; }
    public RelayCommand NavigateResultCommand { get; }
    public RelayCommand NavigateTrajectoryCommand { get; }
    public RelayCommand Open3DReplayCommand { get; }
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
        }
    }
    public bool IsImportScreen => ReferenceEquals(CurrentViewModel, ImportView);
    public bool IsResultScreen => CurrentViewModel is SessionResultViewModel;
    public bool IsTrajectoryScreen => CurrentViewModel is TrajectoryViewModel;
    public string ScreenTitle => IsImportScreen ? "Import session" : IsTrajectoryScreen ? "Trajectory & replay" : "Session result";
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
            Profile = _profiles.Resolve(ImportedSession);
            if (Profile is null)
            {
                ImportView.Fail("Exercise profile unavailable", "No compatible exercise profile is available for this session. The profile ID, version and exercise type must match a built-in demo profile.", ValidationWarnings);
                StatusText = "Compatible profile required";
                return;
            }

            StatusText = "Analyzing session...";
            var session = ImportedSession;
            var profile = Profile;
            AnalysisResult = await Task.Run(() => _analyzer.Analyze(session, profile));
            TrajectoryView = new(session, profile, AnalysisResult);
            ResultView = new(session, profile, AnalysisResult, ValidationWarnings, NavigateTrajectoryCommand, Open3DReplayCommand);
            CurrentViewModel = ResultView;
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

    private void RefreshReplayCapability() => ReplayCapability =
        _replayCapabilities.Evaluate(ImportedSession, SourceFilePath, _replayLauncher.SimulatorExecutablePath);

    private void Open3DReplay()
    {
        try
        {
            RefreshReplayCapability();
            if (ReplayCapability is not { CanLaunchSimulatorReplay: true })
            {
                ReplayLaunchMessage = ReplayCapability?.Limitations.FirstOrDefault() ?? "3D replay is unavailable.";
                return;
            }
            TrajectoryView?.Pause();
            ReplayLaunchMessage = _replayLauncher.Launch(SourceFilePath).Message;
        }
        catch (Exception)
        {
            ReplayLaunchMessage = "The simulator could not be started. Check the executable configuration and try again.";
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
