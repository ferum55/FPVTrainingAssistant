namespace FPVTrainingAssistant.Models;

public sealed record EntityReplayState(string EntityId, string EntityType, string? ArchetypeId, ReplayEntitySample? Transform)
{
    public bool IsAvailable => Transform is not null;
    public bool IsActive => IsAvailable && Transform!.IsActive != false;
    public string StateText => !IsAvailable ? "Not yet available" : Transform!.State ?? "N/A";
}
