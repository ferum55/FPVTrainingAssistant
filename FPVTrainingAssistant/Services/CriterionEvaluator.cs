using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

internal static class CriterionEvaluator
{
    public static EvaluationResult Evaluate(string sessionId, EvaluationCriterion criterion, ExerciseMetrics metrics, RouteAnalysisResult? route)
    {
        var value = criterion.RuleType switch
        {
            RuleTypes.CheckpointAccuracy => Checkpoints(metrics),
            RuleTypes.CollisionAvoidance => Collisions(criterion, metrics),
            RuleTypes.RouteAdherence => Route(criterion, route!),
            RuleTypes.CompletionTime => Time(criterion, metrics),
            RuleTypes.TargetIdentification => Identification(metrics),
            RuleTypes.TargetHit => Hit(metrics),
            RuleTypes.TargetDamage => Damage(criterion, metrics),
            RuleTypes.MissionSuccess => Mission(metrics),
            _ => throw new AnalysisConfigurationException("Unsupported RuleType: " + criterion.RuleType)
        };
        return new EvaluationResult
        {
            ResultId = sessionId + ":" + criterion.CriterionId,
            SessionId = sessionId,
            CriterionId = criterion.CriterionId,
            Score = Math.Clamp(value.Score, 0, 100),
            MeasuredValue = value.Measured,
            Explanation = value.Explanation
        };
    }

    internal static double Parameter(EvaluationCriterion criterion, string key, double fallback) =>
        criterion.Parameters is { } parameters && parameters.TryGetValue(key, out var value) ? value : fallback;

    private static (double Score, double Measured, string Explanation) Checkpoints(ExerciseMetrics m) =>
        (m.CheckpointAccuracyPercent, m.CheckpointAccuracyPercent,
            F($"{m.PassedCheckpointCount} of {m.TotalCheckpointCount} checkpoints were passed; {m.MissedCheckpointCount} missed. Checkpoint accuracy is {m.CheckpointAccuracyPercent:F2}%." ) +
            (m.TotalCheckpointCount == 0 ? " No checkpoint events were recorded; the score is zero." : ""));

    private static (double Score, double Measured, string Explanation) Collisions(EvaluationCriterion c, ExerciseMetrics m)
    {
        var penalty = Parameter(c, "PenaltyPerCollision", 20);
        return (100 - m.CollisionCount * penalty, m.CollisionCount,
            F($"{m.CollisionCount} collisions recorded; penalty {penalty:F2} points per collision, clamped to 0..100."));
    }

    private static (double Score, double Measured, string Explanation) Route(EvaluationCriterion c, RouteAnalysisResult route)
    {
        var factor = Parameter(c, "YellowZoneFactor", 0.5);
        return (route.GreenTimePercent + factor * route.YellowTimePercent, route.GreenTimePercent,
            F($"Route time: {route.GreenTimePercent:F2}% green, {route.YellowTimePercent:F2}% yellow, {route.RedTimePercent:F2}% red. Average deviation {route.AverageDeviationM:F2} m; maximum deviation {route.MaximumDeviationM:F2} m. Yellow contribution factor {factor:F2}."));
    }

    private static (double Score, double Measured, string Explanation) Time(EvaluationCriterion c, ExerciseMetrics m)
    {
        var target = Parameter(c, "TargetTimeSec", 0);
        var maximum = Parameter(c, "MaximumTimeSec", 0);
        var duration = m.CompletionTimeSec;
        var score = duration <= target ? 100 : duration >= maximum ? 0 : 100 * ((maximum - duration) / (maximum - target));
        return (score, duration, F($"Completion time {duration:F2} s; target {target:F2} s, maximum {maximum:F2} s. Time score is capped at 100."));
    }

    private static (double Score, double Measured, string Explanation) Identification(ExerciseMetrics m) =>
        (m.DetectionAccuracyPercent, m.DetectionAccuracyPercent,
            F($"{m.CorrectDetectionCount} correct detections and {m.FalseDetectionCount} false detections; identification accuracy {m.DetectionAccuracyPercent:F2}%." ) +
            (m.TotalDetectionAttempts == 0 ? " No detection attempts were recorded; the score is zero." : ""));

    private static (double Score, double Measured, string Explanation) Hit(ExerciseMetrics m) =>
        (m.TargetHitCount > 0 ? 100 : 0, m.TargetHitCount,
            F($"{m.TargetHitCount} confirmed TargetHit events recorded. ") + (m.TargetHitCount > 0 ? "A target hit was confirmed." : "No target hit was recorded; detection alone does not confirm a hit."));

    private static (double Score, double Measured, string Explanation) Damage(EvaluationCriterion c, ExerciseMetrics m)
    {
        if (m.DamageZones.Count == 0) return (0, 0, "No TargetDamageZone event was recorded; the damage-category score is zero.");
        string? bestZone = null;
        double bestScore = -1;
        foreach (var zone in m.DamageZones)
        {
            var category = zone.ToLowerInvariant() switch
            {
                "critical" => "Critical", "engine" => "Engine", "weapon" or "gun" or "barrel" => "Weapon",
                "track" or "tracks" => "Track", "hull" => "Hull", _ => "Default"
            };
            var fallback = category switch { "Critical" => 100, "Engine" => 90, "Weapon" => 80, "Track" => 70, "Hull" => 60, _ => 50 };
            var score = Parameter(c, "DamageScore_" + category, fallback);
            if (score > bestScore) { bestScore = score; bestZone = zone; }
        }
        return (bestScore, bestScore, F($"Best recorded damage zone '{bestZone}' gives {bestScore:F2} training-category points from {m.DamageZones.Count} TargetDamageZone events. This is an assessment category, not a physical effectiveness model."));
    }

    private static (double Score, double Measured, string Explanation) Mission(ExerciseMetrics m) =>
        (m.MissionSuccess == true ? 100 : 0, m.MissionSuccess == true ? 1 : 0,
            m.MissionSuccess switch { true => "Session.Success is true; the mission succeeded.", false => "Session.Success is false; the mission failed.", null => "Session.Success is unknown; success is not assumed and the score is zero." });

    private static string F(FormattableString value) => FormattableString.Invariant(value);
}
