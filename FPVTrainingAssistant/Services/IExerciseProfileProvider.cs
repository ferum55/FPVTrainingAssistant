using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public interface IExerciseProfileProvider
{
    ExerciseProfile? Resolve(TrainingSession session);
    Task<ExerciseProfile?> ResolveAsync(TrainingSession session, bool usePersistence) => Task.FromResult(Resolve(session));
}
