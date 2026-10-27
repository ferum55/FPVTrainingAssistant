using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public interface ISessionAnalyzer
{
    SessionAnalysisResult Analyze(TrainingSession session, ExerciseProfile profile);
    SessionAnalysisResult AnalyzeRequestedVersion(TrainingSession session, ExerciseProfile profile) => Analyze(session,profile);
}
