using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Data;

namespace FPVTrainingAssistant.Services;

public sealed class ExerciseProfileProvider(IExerciseProfileRepository? repository = null) : IExerciseProfileProvider
{
    public async Task<ExerciseProfile?> ResolveAsync(TrainingSession session, bool usePersistence)
    {
        if (!usePersistence || repository is null) return Resolve(session);
        var profile = await repository.LoadAsync(session.ProfileId,session.ProfileVersion);
        return profile?.ExerciseType == session.ExerciseType ? profile : null;
    }
    public ExerciseProfile? Resolve(TrainingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return Resolve(session.ProfileId, session.ProfileVersion, session.ExerciseType);
    }

    public static IReadOnlyList<ExerciseProfile> BuiltInProfiles() =>
        [Resolve("synthetic-obstacle-course", 1, ExerciseTypes.ObstacleCourse)!, Resolve("synthetic-target-mission", 1, ExerciseTypes.TargetMission)!];

    private static ExerciseProfile? Resolve(string profileId, int profileVersion, string exerciseType)
    {
        if (profileVersion != 1) return null;
        return (profileId, exerciseType) switch
        {
            ("synthetic-obstacle-course", ExerciseTypes.ObstacleCourse) => DemoProfile(
                DefaultExerciseProfiles.DefaultObstacleCourse(
                [
                    new(1.5, 3) { SequenceNo = 0, X = 0, Y = 0, Z = 2, PointType = "SyntheticRoute" },
                    new(1.5, 3) { SequenceNo = 1, X = 8, Y = 0, Z = 2, PointType = "SyntheticRoute" },
                    new(1.5, 3) { SequenceNo = 2, X = 8, Y = 8, Z = 2, PointType = "SyntheticRoute" },
                    new(1.5, 3) { SequenceNo = 3, X = 18, Y = 8, Z = 2, PointType = "SyntheticRoute" }
                ]), "synthetic-obstacle-course", "Synthetic obstacle course demo"),
            ("synthetic-target-mission", ExerciseTypes.TargetMission) => DemoProfile(
                DefaultExerciseProfiles.DefaultTargetMission(), "synthetic-target-mission", "Synthetic target mission demo"),
            _ => null
        };
    }

    private static ExerciseProfile DemoProfile(ExerciseProfile template, string id, string name) => new()
    {
        ProfileId = id,
        Name = name,
        ExerciseType = template.ExerciseType,
        Version = template.Version,
        Criteria = template.Criteria,
        ReferencePoints = template.ReferencePoints
    };
}
