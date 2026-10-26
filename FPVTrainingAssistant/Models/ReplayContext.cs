namespace FPVTrainingAssistant.Models;

/// <summary>Neutral environment identity and recorded dynamic entities, without static map assets.</summary>
public sealed class ReplayContext
{
    public required string EnvironmentId { get; init; }
    public string? ScenarioId { get; init; }
    public List<ReplayEntityTrack> Entities { get; init; } = [];
}
