using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public interface ISessionAnalyzer
{
    SessionAnalysisResult Analyze(TrainingSession session, ExerciseProfile profile);
}
