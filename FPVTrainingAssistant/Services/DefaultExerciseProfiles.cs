using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public static class DefaultExerciseProfiles
{
    // This template deliberately has no course-specific route. Supply one before analysis.
    public static ExerciseProfile DefaultObstacleCourse(IEnumerable<ReferencePoint>? referencePoints = null) => new()
    {
        ProfileId = "default-obstacle-course", Name = "Default obstacle course", ExerciseType = ExerciseTypes.ObstacleCourse, Version = 1,
        ReferencePoints = referencePoints?.ToList() ?? [],
        Criteria =
        [
            Criterion(RuleTypes.CheckpointAccuracy, 40),
            Criterion(RuleTypes.RouteAdherence, 30),
            Criterion(RuleTypes.CollisionAvoidance, 20),
            Criterion(RuleTypes.CompletionTime, 10, new() { ["TargetTimeSec"] = 60, ["MaximumTimeSec"] = 180 })
        ]
    };

    public static ExerciseProfile DefaultTargetMission() => new()
    {
        ProfileId = "default-target-mission", Name = "Default target mission", ExerciseType = ExerciseTypes.TargetMission, Version = 1,
        Criteria =
        [
            Criterion(RuleTypes.TargetIdentification, 35), Criterion(RuleTypes.TargetHit, 30),
            Criterion(RuleTypes.TargetDamage, 20), Criterion(RuleTypes.MissionSuccess, 15)
        ]
    };

    private static EvaluationCriterion Criterion(string rule, double weight, Dictionary<string, double>? parameters = null) => new()
    {
        CriterionId = rule, Name = rule, Description = "Evaluate " + rule + " using documented training rules.",
        RuleType = rule, Weight = weight, Parameters = parameters
    };
}
