namespace FPVTrainingAssistant.Models;

public sealed class SessionEvent
{
    public required string EventId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(EventId));
    }

    public required double TimeSec { get; init; }
    public required string EventType { get; init; }
    public string? EntityId { get; init; }
    public string? Value { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
