namespace FPVTrainingAssistant.Models;

public sealed class Recommendation
{
    public required string RecommendationId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(RecommendationId));
    }

    public required string SessionId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(SessionId));
    }

    public required string Category { get; init; }
    public required string Text { get; init; }
    public int Priority { get; init; }

    public required string Evidence
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(Evidence));
    }
}
