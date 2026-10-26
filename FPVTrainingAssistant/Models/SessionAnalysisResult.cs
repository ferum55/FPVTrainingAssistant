namespace FPVTrainingAssistant.Models;

public sealed class SessionAnalysisResult
{
    public required string SessionId { get; init; }
    public required string ProfileId { get; init; }
    public required int ProfileVersion { get; init; }
    public required double FinalScore { get; init; }
    public required ExerciseMetrics Metrics { get; init; }
    public required IReadOnlyList<EvaluationResult> CriterionResults { get; init; }
    public required IReadOnlyList<DetectedMistake> Mistakes { get; init; }
    public required IReadOnlyList<Recommendation> Recommendations { get; init; }
    public RouteAnalysisResult? RouteAnalysis { get; init; }
    public bool IsSuccessful { get; init; }
    public string? Summary { get; init; }
}
