namespace FPVTrainingAssistant.Models;

/// <summary>Conventional names, not an exhaustive set of allowed event types.</summary>
public static class SessionEventTypes
{
    public const string CheckpointPassed = nameof(CheckpointPassed);
    public const string CheckpointMissed = nameof(CheckpointMissed);
    public const string Collision = nameof(Collision);
    public const string TargetDetected = nameof(TargetDetected);
    public const string FalseTargetDetection = nameof(FalseTargetDetection);
    public const string TargetHit = nameof(TargetHit);
    public const string TargetDamageZone = nameof(TargetDamageZone);
    public const string ObjectiveCompleted = nameof(ObjectiveCompleted);
    public const string Crash = nameof(Crash);
    public const string MissionCompleted = nameof(MissionCompleted);
}
