using FPVTrainingAssistant.Models;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed class HistoryRepository(DatabaseConnectionFactory connections) : IHistoryRepository
{
    public Task<IReadOnlyList<OperatorHistory>> ListOperatorsAsync() => Task.Run<IReadOnlyList<OperatorHistory>>(() =>
    {
        using var c = connections.Open();
        return Query(c,null,"""
            SELECT o.operator_id,o.display_name,o.notes,COUNT(s.session_id) AS count,MAX(s.started_at_utc) AS last_time,AVG(s.assistant_final_score) AS average
            FROM operators o LEFT JOIN training_sessions s ON s.operator_id=o.operator_id
            GROUP BY o.operator_id ORDER BY COALESCE(o.display_name,o.operator_id),o.operator_id
            """, r => new OperatorHistory(new OperatorProfile { OperatorId=Text(r,"operator_id")!,DisplayName=Text(r,"display_name"),Notes=Text(r,"notes") },
                r.GetInt32(r.GetOrdinal("count")),Text(r,"last_time") is { } date ? Date(date) : null,Number(r,"average")));
    });

    public Task<IReadOnlyList<SessionHistoryEntry>> ListSessionsAsync(string operatorId,string? exerciseType) => Task.Run<IReadOnlyList<SessionHistoryEntry>>(() =>
    {
        using var c = connections.Open();
        return Query(c,null,"""
            SELECT session_id,operator_id,exercise_type,started_at_utc,ended_at_utc,assistant_final_score,simulator_score,success
            FROM training_sessions WHERE operator_id=$p0 AND ($p1 IS NULL OR exercise_type=$p1)
            ORDER BY started_at_utc DESC,session_id DESC
            """, r => new SessionHistoryEntry(Text(r,"session_id")!,Text(r,"operator_id")!,Text(r,"exercise_type")!,Date(Text(r,"started_at_utc")!),
                (Date(Text(r,"ended_at_utc")!)-Date(Text(r,"started_at_utc")!)).TotalSeconds,Number(r,"assistant_final_score")!.Value,Number(r,"simulator_score"),Bool(r,"success")),operatorId,exerciseType);
    });

    public Task<bool> UpdateOperatorAsync(string operatorId,string? displayName,string? notes) => Task.Run(() =>
    {
        using var c = connections.Open();
        return Execute(c,null,"UPDATE operators SET display_name=$p0,notes=$p1,updated_at_utc=$p2 WHERE operator_id=$p3",
            string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),string.IsNullOrWhiteSpace(notes) ? null : notes,Utc(DateTimeOffset.UtcNow),operatorId) == 1;
    });
}
