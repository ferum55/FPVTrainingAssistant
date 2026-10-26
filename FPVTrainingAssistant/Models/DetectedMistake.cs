namespace FPVTrainingAssistant.Models;

public sealed record DetectedMistake(
    string Code,
    string Category,
    string Description,
    double? TimeSec,
    string? RelatedEntityId,
    string Evidence);
