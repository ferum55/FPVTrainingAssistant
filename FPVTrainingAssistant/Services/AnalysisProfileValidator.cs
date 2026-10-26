using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

internal static class AnalysisProfileValidator
{
    public static EvaluationCriterion[] Validate(TrainingSession session, ExerciseProfile profile)
    {
        if (session.ProfileId != profile.ProfileId) Fail("Session ProfileId does not match the exercise profile.");
        if (session.ProfileVersion != profile.Version || profile.Version <= 0) Fail("Session/profile version mismatch or invalid version.");
        if (session.ExerciseType != profile.ExerciseType) Fail("Session and profile exercise types must match exactly.");
        if (profile.ExerciseType is not (ExerciseTypes.ObstacleCourse or ExerciseTypes.TargetMission)) Fail("Analysis supports only ObstacleCourse and TargetMission.");
        if (!profile.IsActive) Fail("The exercise profile is inactive.");
        if (string.IsNullOrWhiteSpace(profile.Name)) Fail("The exercise profile requires a name.");
        if (profile.Criteria is null || profile.Criteria.Count == 0) Fail("The profile has no enabled criteria.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var criterion in profile.Criteria!)
        {
            if (criterion is null) Fail("Criteria cannot contain null entries.");
            if (string.IsNullOrWhiteSpace(criterion!.CriterionId) || !ids.Add(criterion.CriterionId)) Fail("Criterion IDs must be nonempty and unique within a profile.");
            if (!double.IsFinite(criterion.Weight) || criterion.Weight < 0) Fail("Criterion weights must be finite and nonnegative.");
            if (!criterion.IsEnabled) continue;
            if (criterion.Weight <= 0) Fail("Every enabled criterion must have a positive weight.");
            if (string.IsNullOrWhiteSpace(criterion.Name) || string.IsNullOrWhiteSpace(criterion.Description)) Fail("Enabled criteria require a name and description.");
            if (criterion.Threshold is { } threshold && !double.IsFinite(threshold)) Fail("Criterion thresholds must be finite when supplied.");
            if (criterion.Parameters?.Any(p => string.IsNullOrWhiteSpace(p.Key) || !double.IsFinite(p.Value)) == true) Fail("Criterion parameters require nonempty keys and finite values.");
            ValidateRule(criterion, profile.ExerciseType);
        }

        var enabled = profile.Criteria!.Where(c => c.IsEnabled).ToArray();
        if (enabled.Length == 0) Fail("The profile has no enabled criteria or positive total weight.");
        if (enabled.Any(c => c.RuleType == RuleTypes.RouteAdherence)) RouteAnalyzer.ValidateRoute(profile.ReferencePoints);
        return enabled;
    }

    private static void ValidateRule(EvaluationCriterion criterion, string exerciseType)
    {
        switch (criterion.RuleType)
        {
            case RuleTypes.CheckpointAccuracy:
                if (exerciseType != ExerciseTypes.ObstacleCourse) Fail("CheckpointAccuracy requires ObstacleCourse.");
                break;
            case RuleTypes.CollisionAvoidance:
                if (CriterionEvaluator.Parameter(criterion, "PenaltyPerCollision", 20) < 0) Fail("PenaltyPerCollision must be nonnegative.");
                break;
            case RuleTypes.RouteAdherence:
                if (CriterionEvaluator.Parameter(criterion, "YellowZoneFactor", 0.5) is < 0 or > 1) Fail("YellowZoneFactor must be between 0 and 1.");
                break;
            case RuleTypes.CompletionTime:
                var target = CriterionEvaluator.Parameter(criterion, "TargetTimeSec", 0);
                var maximum = CriterionEvaluator.Parameter(criterion, "MaximumTimeSec", 0);
                if (target <= 0 || maximum <= target) Fail("CompletionTime requires TargetTimeSec > 0 and MaximumTimeSec > TargetTimeSec.");
                break;
            case RuleTypes.TargetIdentification:
            case RuleTypes.TargetHit:
            case RuleTypes.TargetDamage:
                if (exerciseType != ExerciseTypes.TargetMission) Fail("Target criteria require TargetMission.");
                if (criterion.RuleType == RuleTypes.TargetDamage)
                {
                    foreach (var key in new[] { "Default", "Critical", "Engine", "Track", "Weapon", "Hull" })
                    {
                        if (CriterionEvaluator.Parameter(criterion, "DamageScore_" + key, 50) is < 0 or > 100) Fail("Configured DamageScore values must be between 0 and 100.");
                    }
                }
                break;
            case RuleTypes.MissionSuccess:
                break;
            default:
                Fail("Unsupported RuleType: " + criterion.RuleType);
                break;
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string message) => throw new AnalysisConfigurationException(message);
}
