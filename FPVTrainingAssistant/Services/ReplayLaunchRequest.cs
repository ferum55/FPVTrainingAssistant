using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed record ReplayLaunchRequest(TrainingSession Session, ExerciseProfile Profile,
    SessionAnalysisResult Analysis, string? SourceFilePath);

// UI boundary: implementations own window activation and lifetime, never a process.
public interface IReplayMockWindowService
{
    void Show(ReplayLaunchRequest request);
}
