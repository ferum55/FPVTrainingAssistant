using System.Text.Json.Serialization;

namespace FPVTrainingAssistant.Models;

public sealed class TrainingSession
{
    public string ContractVersion { get; init; } = "1.0";

    public required string SessionId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(SessionId));
    }

    public required string OperatorId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(OperatorId));
    }

    public required string ProfileId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(ProfileId));
    }

    public required int ProfileVersion { get; init; }
    public required string ExerciseType { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset EndedAtUtc { get; init; }
    public required string SourceSimulator { get; init; }
    public bool? Success { get; init; }
    public double? SimulatorScore { get; init; }

    public double? AssistantFinalScore
    {
        get;
        init => field = value is { } score ? ModelValue.Percent(score, nameof(AssistantFinalScore)) : null;
    }

    public List<TelemetrySample> Telemetry
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(Telemetry));
    } = [];

    public List<SessionEvent> Events
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(Events));
    } = [];

    /// <summary>
    /// Derived elapsed time; reversed session timestamps are invalid, not clamped to zero.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => EndedAtUtc >= StartedAtUtc
        ? EndedAtUtc - StartedAtUtc
        : throw new InvalidOperationException("Session end time must not precede its start time.");
}
