using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class ExerciseProfileProvider : IExerciseProfileProvider
{
    public ExerciseProfile? Resolve(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.ProfileVersion != 1) return null;
        return (session.ProfileId, session.ExerciseType) switch
        {
            ("synthetic-obstacle-course", ExerciseTypes.ObstacleCourse) => DemoProfile(
                DefaultExerciseProfiles.DefaultObstacleCourse(
                [
                    new(1.5, 3) { SequenceNo = 0, X = 0, Y = 0, Z = 2, PointType = "SyntheticRoute" },
                    new(1.5, 3) { SequenceNo = 1, X = 8, Y = 0, Z = 2, PointType = "SyntheticRoute" },
                    new(1.5, 3) { SequenceNo = 2, X = 18, Y = 0, Z = 2, PointType = "SyntheticRoute" }
                ]), "synthetic-obstacle-course", "Synthetic obstacle course demo"),
            ("synthetic-target-mission", ExerciseTypes.TargetMission) => DemoProfile(
                DefaultExerciseProfiles.DefaultTargetMission(), "synthetic-target-mission", "Synthetic target mission demo"),
            _ => null
        };
    }

    private static ExerciseProfile DemoProfile(ExerciseProfile template, string id, string name) => new()
    {
        ProfileId = id, Name = name, ExerciseType = template.ExerciseType, Version = template.Version,
        Criteria = template.Criteria, ReferencePoints = template.ReferencePoints
    };
}
