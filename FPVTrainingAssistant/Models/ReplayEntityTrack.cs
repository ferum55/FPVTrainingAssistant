namespace FPVTrainingAssistant.Models;

public sealed class ReplayEntityTrack
{
    public required string EntityId { get; init; }
    public required string EntityType { get; init; }
    public string? ArchetypeId { get; init; }
    public List<ReplayEntitySample> Samples { get; init; } = [];
    public Dictionary<string, string>? Metadata { get; init; }
}
