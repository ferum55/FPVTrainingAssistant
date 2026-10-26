namespace FPVTrainingAssistant.Models;

public sealed record RouteAnalysisResult(
    double GreenTimePercent,
    double YellowTimePercent,
    double RedTimePercent,
    double AverageDeviationM,
    double MaximumDeviationM,
    double TotalEvaluatedTimeSec);
