using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public interface IExerciseProfileProvider
{
    ExerciseProfile? Resolve(TrainingSession session);
}
