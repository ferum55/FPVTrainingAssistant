namespace FPVTrainingAssistant.Models;

public sealed class EvaluationResult
{
    public required string ResultId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(ResultId));
    }

    public required string SessionId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(SessionId));
    }

    public required string CriterionId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(CriterionId));
    }

    public double? MeasuredValue { get; init; }

    public required double Score
    {
        get;
        init => field = ModelValue.Percent(value, nameof(Score));
    }

    public required string Explanation
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(Explanation));
    }
}
