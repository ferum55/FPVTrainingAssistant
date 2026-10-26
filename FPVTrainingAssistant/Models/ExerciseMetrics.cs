namespace FPVTrainingAssistant.Models;

public sealed class ExerciseMetrics
{
    public int PassedCheckpointCount { get; init; }
    public int MissedCheckpointCount { get; init; }
    public int TotalCheckpointCount { get; init; }
    public double CheckpointAccuracyPercent { get; init; }
    public int CollisionCount { get; init; }
    public double CompletionTimeSec { get; init; }
    public int CorrectDetectionCount { get; init; }
    public int FalseDetectionCount { get; init; }
    public int TotalDetectionAttempts { get; init; }
    public double DetectionAccuracyPercent { get; init; }
    public int TargetHitCount { get; init; }
    public IReadOnlyList<string> DamageZones { get; init; } = [];
    public bool? MissionSuccess { get; init; }
}
