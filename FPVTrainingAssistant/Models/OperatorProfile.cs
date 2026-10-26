namespace FPVTrainingAssistant.Models;

public sealed class OperatorProfile
{
    public required string OperatorId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(OperatorId));
    }

    public string? DisplayName { get; init; }
    public string? Notes { get; init; }
}
