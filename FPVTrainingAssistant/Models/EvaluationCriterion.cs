namespace FPVTrainingAssistant.Models;

public sealed class EvaluationCriterion
{
    public required string CriterionId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(CriterionId));
    }

    public required string Name { get; init; }
    public required string Description { get; init; }
    public required double Weight { get; init; }
    public required string RuleType { get; init; }
    public bool IsEnabled { get; init; } = true;
    public double? Threshold { get; init; }
    public Dictionary<string, double>? Parameters { get; init; }
}
