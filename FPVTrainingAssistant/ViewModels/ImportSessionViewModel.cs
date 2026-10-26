using System.IO;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed class ImportSessionViewModel(AsyncRelayCommand importCommand) : ViewModelBase
{
    private string? _filePath;
    private bool _isBusy;
    private bool _hasFailure;
    private string _failureHeading = "";
    private string _failureMessage = "";
    private string? _technicalError;
    private IReadOnlyList<ValidationIssue> _issues = [];

    public AsyncRelayCommand ImportCommand { get; } = importCommand;
    public string? FilePath { get => _filePath; private set { if (SetProperty(ref _filePath, value)) { OnPropertyChanged(nameof(FileName)); OnPropertyChanged(nameof(HasFile)); } } }
    public string FileName => FilePath is null ? "No file selected" : Path.GetFileName(FilePath);
    public bool HasFile => FilePath is not null;
    public bool IsBusy { get => _isBusy; internal set => SetProperty(ref _isBusy, value); }
    public bool HasFailure { get => _hasFailure; private set => SetProperty(ref _hasFailure, value); }
    public string FailureHeading { get => _failureHeading; private set => SetProperty(ref _failureHeading, value); }
    public string FailureMessage { get => _failureMessage; private set => SetProperty(ref _failureMessage, value); }
    public string? TechnicalError { get => _technicalError; private set { if (SetProperty(ref _technicalError, value)) OnPropertyChanged(nameof(HasTechnicalError)); } }
    public bool HasTechnicalError => !string.IsNullOrWhiteSpace(TechnicalError);
    public IReadOnlyList<ValidationIssue> Issues { get => _issues; private set => SetProperty(ref _issues, value); }

    internal void Begin(string filePath)
    {
        FilePath = filePath;
        HasFailure = false;
        TechnicalError = null;
        Issues = [];
    }

    internal void Fail(string heading, string message, IReadOnlyList<ValidationIssue> issues, string? technicalError = null)
    {
        FailureHeading = heading;
        FailureMessage = message;
        Issues = issues;
        TechnicalError = technicalError;
        HasFailure = true;
    }
}
