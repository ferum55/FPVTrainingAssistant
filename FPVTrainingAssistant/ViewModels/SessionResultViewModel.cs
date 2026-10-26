using System.Globalization;
using System.Windows.Input;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed class SessionResultViewModel : ViewModelBase
{
    public SessionResultViewModel(TrainingSession session, ExerciseProfile profile, SessionAnalysisResult result, IReadOnlyList<ValidationIssue> warnings, ICommand? viewTrajectoryCommand = null, ICommand? open3DReplayCommand = null, string origin = "Imported session")
    {
        OperatorId = session.OperatorId;
        Origin = origin;
        SessionId = session.SessionId;
        ExerciseType = DisplayText.Name(session.ExerciseType);
        SourceSimulator = session.SourceSimulator;
        StartedAt = session.StartedAtUtc.ToUniversalTime().ToString("dd MMM yyyy  ·  HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        Duration = session.Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s";
        Outcome = session.Success switch { true => "Succeeded", false => "Failed", null => "Not reported" };
        AssistantScore = result.FinalScore;
        AssistantScoreText = DisplayText.Number(result.FinalScore);
        SimulatorScoreText = session.SimulatorScore is { } simulator ? DisplayText.Number(simulator) : "Not provided";
        ProfileName = profile.Name;
        ProfileIdentity = profile.ProfileId + " · v" + profile.Version.ToString(CultureInfo.InvariantCulture);
        Criteria = Array.AsReadOnly(result.CriterionResults.Select(r => new CriterionResultViewModel(r, profile.Criteria.FirstOrDefault(c => c.CriterionId == r.CriterionId))).ToArray());
        Mistakes = Array.AsReadOnly(result.Mistakes.Select(m => new MistakeViewModel(m)).ToArray());
        Recommendations = Array.AsReadOnly(result.Recommendations.OrderBy(r => r.Priority).Select(r => new RecommendationViewModel(r)).ToArray());
        Events = Array.AsReadOnly(session.Events.OrderBy(e => e.TimeSec).Select(e => new EventViewModel(e)).ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
        ViewTrajectoryCommand = viewTrajectoryCommand ?? new RelayCommand(() => { }, () => false);
        Open3DReplayCommand = open3DReplayCommand ?? new RelayCommand(() => { }, () => false);
        HasTrajectoryData = session.Telemetry.Count >= 2;
    }

    public string OperatorId { get; }
    public string Origin { get; }
    public string SessionId { get; }
    public string ExerciseType { get; }
    public string SourceSimulator { get; }
    public string StartedAt { get; }
    public string Duration { get; }
    public string Outcome { get; }
    public double AssistantScore { get; }
    public string AssistantScoreText { get; }
    public string SimulatorScoreText { get; }
    public string ProfileName { get; }
    public string ProfileIdentity { get; }
    public IReadOnlyList<CriterionResultViewModel> Criteria { get; }
    public IReadOnlyList<MistakeViewModel> Mistakes { get; }
    public IReadOnlyList<RecommendationViewModel> Recommendations { get; }
    public IReadOnlyList<EventViewModel> Events { get; }
    public IReadOnlyList<ValidationIssue> Warnings { get; }
    public bool HasWarnings => Warnings.Count > 0;
    public bool HasNoMistakes => Mistakes.Count == 0;
    public bool HasNoRecommendations => Recommendations.Count == 0;
    public bool HasNoEvents => Events.Count == 0;
    public ICommand ViewTrajectoryCommand { get; }
    public ICommand Open3DReplayCommand { get; }
    public bool HasTrajectoryData { get; }
}
