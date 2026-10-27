using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Data;

public enum ProfileSaveState { Saved, Invalid, Conflict, Failed }
public sealed record ProfileSaveResult(ProfileSaveState State, string Message, ExerciseProfile? Profile = null, IReadOnlyList<string>? Issues = null);
public interface IExerciseProfileRepository
{
    Task<IReadOnlyList<ExerciseProfile>> GetProfilesAsync();
    Task<ExerciseProfile?> LoadAsync(string id, int version);
    Task<ExerciseProfile?> GetActiveVersionAsync(string id);
    Task<ProfileSaveResult> CreateNewVersionAsync(ExerciseProfile workingCopy, int expectedLatestVersion, bool activate);
    Task<ProfileSaveResult> SetActiveVersionAsync(string id, int version);
}
