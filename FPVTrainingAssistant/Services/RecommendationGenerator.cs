using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class RecommendationGenerator
{
    public IReadOnlyList<Recommendation> Generate(string sessionId, IReadOnlyList<DetectedMistake> mistakes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(mistakes);
        var recommendations = new List<Recommendation>();
        Add("checkpoint-accuracy", "CheckpointAccuracy", 2, ["MISSED_CHECKPOINT", "LOW_CHECKPOINT_ACCURACY"], "Practice precise checkpoint passage and reduce missed gates.");
        Add("collision-avoidance", "CollisionAvoidance", 1, ["COLLISION"], "Focus on obstacle clearance and smoother trajectory corrections.");
        Add("route-adherence", "RouteAdherence", 1, ["ROUTE_DEVIATION"], "Practice maintaining the recommended route and reducing lateral deviation.");
        Add("false-target-detection", "TargetIdentification", 1, ["FALSE_TARGET_DETECTION"], "Improve target identification before confirming detection.");
        Add("target-search", "TargetIdentification", 2, ["TARGET_NOT_IDENTIFIED"], "Practice systematic target search and visual identification.");
        Add("target-hit", "TargetApproach", 2, ["TARGET_NOT_HIT"], "Practice the final approach and alignment with the simulator target.");
        Add("mission-failed", "MissionCompletion", 1, ["MISSION_FAILED"], "Review the failed mission sequence and repeat the exercise focusing on the detected weaknesses.");
        return Array.AsReadOnly(recommendations.OrderBy(r => r.Priority).ThenBy(r => r.RecommendationId, StringComparer.Ordinal).ToArray());

        void Add(string suffix, string category, int priority, string[] codes, string text)
        {
            var evidence = mistakes.Where(m => codes.Contains(m.Code, StringComparer.Ordinal))
                .Select(m => m.Evidence).Distinct(StringComparer.Ordinal).OrderBy(e => e, StringComparer.Ordinal).ToArray();
            if (evidence.Length == 0) return;
            recommendations.Add(new Recommendation
            {
                RecommendationId = sessionId + ":rec:" + suffix,
                SessionId = sessionId, Category = category, Text = text, Priority = priority,
                Evidence = string.Join(" ", evidence)
            });
        }
    }
}
