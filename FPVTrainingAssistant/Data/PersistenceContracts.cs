using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Data;

public sealed record StoredSession(TrainingSession Session, ExerciseProfile Profile, SessionAnalysisResult Analysis);
public enum SessionSaveState { Saved, Duplicate, Failed }
public sealed record SessionSaveResult(SessionSaveState State, string Message);
public sealed record DatabaseInitializationResult(bool IsSuccess, string Message);
public interface IDatabaseInitializer { Task<DatabaseInitializationResult> InitializeAsync(); }
public interface ISessionRepository
{
    Task<SessionSaveResult> SaveAsync(StoredSession session);
    Task<StoredSession?> LoadAsync(string sessionId);
}
public sealed record OperatorHistory(OperatorProfile Profile, int SessionCount, DateTimeOffset? LastSessionUtc, double? AverageScore)
{
    public string Label => string.IsNullOrWhiteSpace(Profile.DisplayName) ? Profile.OperatorId : Profile.DisplayName + " · " + Profile.OperatorId;
    public string Summary => $"{SessionCount} sessions · Last: {LastSessionUtc?.ToLocalTime().ToString("g") ?? "N/A"} · Average: {AverageScore?.ToString("F2",System.Globalization.CultureInfo.InvariantCulture) ?? "N/A"}";
}
public sealed record SessionHistoryEntry(string SessionId, string OperatorId, string ExerciseType, DateTimeOffset StartedAtUtc,
    double DurationSec, double AssistantScore, double? SimulatorScore, bool? Success)
{
    public string LocalDate => StartedAtUtc.ToLocalTime().ToString("g");
    public string Outcome => Success switch { true => "Succeeded", false => "Failed", null => "Not reported" };
}
public interface IHistoryRepository
{
    Task<IReadOnlyList<OperatorHistory>> ListOperatorsAsync();
    Task<IReadOnlyList<SessionHistoryEntry>> ListSessionsAsync(string operatorId, string? exerciseType);
    Task<bool> UpdateOperatorAsync(string operatorId, string? displayName, string? notes);
}
