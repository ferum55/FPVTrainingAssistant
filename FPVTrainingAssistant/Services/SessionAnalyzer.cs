using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class SessionAnalyzer : ISessionAnalyzer
{
    private readonly double _redZoneMistakeThresholdPercent;

    public SessionAnalyzer(double redZoneMistakeThresholdPercent = 10)
    {
        if (!double.IsFinite(redZoneMistakeThresholdPercent) || redZoneMistakeThresholdPercent is < 0 or > 100)
        {
            throw new AnalysisConfigurationException("Red-zone mistake threshold must be finite and between 0 and 100 percent.");
        }
        _redZoneMistakeThresholdPercent = redZoneMistakeThresholdPercent;
    }

    // The caller supplies an already validated session. Analysis has no IO or UI side effects.
    public SessionAnalysisResult Analyze(TrainingSession session, ExerciseProfile profile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        var criteria = AnalysisProfileValidator.Validate(session, profile);
        var metrics = CalculateMetrics(session);
        var routeRequired = criteria.Any(c => c.RuleType == RuleTypes.RouteAdherence);
        RouteAnalysisResult? route = null;
        if (routeRequired || (profile.ReferencePoints.Count >= 2 && session.Telemetry.Count >= 2))
        {
            route = new RouteAnalyzer().Analyze(session.Telemetry, profile.ReferencePoints);
        }

        var results = criteria.Select(c => CriterionEvaluator.Evaluate(session.SessionId, c, metrics, route)).ToArray();
        // Algebraically equivalent to weight / sum(weights), without overflowing the sum.
        var scale = criteria.Max(c => c.Weight);
        var total = criteria.Sum(c => c.Weight / scale);
        var weighted = results.Select((result, i) => result.Score * ((criteria[i].Weight / scale) / total)).Sum();
        var finalScore = Math.Round(Math.Clamp(weighted, 0, 100), 2, MidpointRounding.AwayFromZero);
        var mistakes = MistakeDetector.Detect(session, metrics, route, _redZoneMistakeThresholdPercent);
        var recommendations = new RecommendationGenerator().Generate(session.SessionId, mistakes);

        return new SessionAnalysisResult
        {
            SessionId = session.SessionId, ProfileId = profile.ProfileId, ProfileVersion = profile.Version,
            FinalScore = finalScore, Metrics = metrics, CriterionResults = Array.AsReadOnly(results),
            Mistakes = mistakes, Recommendations = recommendations, RouteAnalysis = route, IsSuccessful = true,
            Summary = FormattableString.Invariant($"Assistant score {finalScore:F2}/100 from {results.Length} enabled criteria; {mistakes.Count} detected mistakes.")
        };
    }

    private static ExerciseMetrics CalculateMetrics(TrainingSession session)
    {
        var counts = session.Events.GroupBy(e => e.EventType, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        int Count(string name) => counts.GetValueOrDefault(name);
        var passed = Count(SessionEventTypes.CheckpointPassed);
        var missed = Count(SessionEventTypes.CheckpointMissed);
        var correct = Count(SessionEventTypes.TargetDetected);
        var falseDetections = Count(SessionEventTypes.FalseTargetDetection);
        var zones = session.Events.Where(e => e.EventType == SessionEventTypes.TargetDamageZone)
            .OrderBy(e => e.TimeSec).ThenBy(e => e.EventId, StringComparer.Ordinal)
            .Select(e => e.Metadata!["damageZone"]).ToArray();
        return new ExerciseMetrics
        {
            PassedCheckpointCount = passed, MissedCheckpointCount = missed, TotalCheckpointCount = passed + missed,
            CheckpointAccuracyPercent = passed + missed > 0 ? (double)passed / (passed + missed) * 100 : 0,
            CollisionCount = Count(SessionEventTypes.Collision), CompletionTimeSec = session.Duration.TotalSeconds,
            CorrectDetectionCount = correct, FalseDetectionCount = falseDetections, TotalDetectionAttempts = correct + falseDetections,
            DetectionAccuracyPercent = correct + falseDetections > 0 ? (double)correct / (correct + falseDetections) * 100 : 0,
            TargetHitCount = Count(SessionEventTypes.TargetHit), DamageZones = Array.AsReadOnly(zones), MissionSuccess = session.Success
        };
    }
}
