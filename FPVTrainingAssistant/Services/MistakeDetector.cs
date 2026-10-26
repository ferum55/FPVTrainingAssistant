using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

internal static class MistakeDetector
{
    public static IReadOnlyList<DetectedMistake> Detect(TrainingSession session, ExerciseMetrics metrics, RouteAnalysisResult? route, double redThreshold)
    {
        var mistakes = new List<DetectedMistake>();
        var obstacle = session.ExerciseType == ExerciseTypes.ObstacleCourse;
        foreach (var e in session.Events.OrderBy(e => e.TimeSec).ThenBy(e => e.EventId, StringComparer.Ordinal))
        {
            var evidence = FormattableString.Invariant($"Event {e.EventId}: {e.EventType} at {e.TimeSec:F2} s, entity '{e.EntityId ?? "unspecified"}'.");
            if (obstacle && e.EventType == SessionEventTypes.CheckpointMissed)
            {
                mistakes.Add(new("MISSED_CHECKPOINT", "CheckpointAccuracy", "A checkpoint was recorded as missed.", e.TimeSec, e.EntityId, evidence));
            }
            if (obstacle && e.EventType == SessionEventTypes.Collision)
            {
                mistakes.Add(new("COLLISION", "CollisionAvoidance", "A collision was recorded.", e.TimeSec, e.EntityId, evidence));
            }
            if (!obstacle && e.EventType == SessionEventTypes.FalseTargetDetection)
            {
                mistakes.Add(new("FALSE_TARGET_DETECTION", "TargetIdentification", "A false target confirmation was recorded.", e.TimeSec, e.EntityId, evidence));
            }
        }

        if (obstacle)
        {
            if (metrics.CheckpointAccuracyPercent < 80)
            {
                mistakes.Add(new("LOW_CHECKPOINT_ACCURACY", "CheckpointAccuracy", "Recorded checkpoint accuracy is below 80%.", null, null,
                    FormattableString.Invariant($"{metrics.PassedCheckpointCount} passed, {metrics.MissedCheckpointCount} missed; accuracy {metrics.CheckpointAccuracyPercent:F2}%.")));
            }
            if (route is not null && route.RedTimePercent > redThreshold)
            {
                mistakes.Add(new("ROUTE_DEVIATION", "RouteAdherence", "Red-zone exposure exceeds the configured threshold.", null, null,
                    FormattableString.Invariant($"Red-zone time {route.RedTimePercent:F2}% exceeds {redThreshold:F2}%; evaluated time {route.TotalEvaluatedTimeSec:F2} s, maximum deviation {route.MaximumDeviationM:F2} m.")));
            }
        }
        else
        {
            if (metrics.CorrectDetectionCount == 0)
            {
                mistakes.Add(new("TARGET_NOT_IDENTIFIED", "TargetIdentification", "No correct target detection was recorded.", null, null,
                    FormattableString.Invariant($"{metrics.CorrectDetectionCount} TargetDetected events and {metrics.FalseDetectionCount} FalseTargetDetection events recorded.")));
            }
            if (metrics.TargetHitCount == 0)
            {
                mistakes.Add(new("TARGET_NOT_HIT", "TargetApproach", "No confirmed target hit was recorded.", null, null,
                    FormattableString.Invariant($"0 TargetHit events recorded; {metrics.CorrectDetectionCount} correct detection events do not imply a hit.")));
            }
            if (metrics.MissionSuccess == false)
            {
                mistakes.Add(new("MISSION_FAILED", "MissionCompletion", "The session explicitly reports mission failure.", null, null, "TrainingSession.Success is false."));
            }
        }
        return mistakes.AsReadOnly();
    }
}
