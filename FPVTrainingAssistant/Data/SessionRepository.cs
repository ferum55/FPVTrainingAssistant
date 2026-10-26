using System.Diagnostics;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed partial class SessionRepository(DatabaseConnectionFactory connections) : ISessionRepository
{
    public Task<SessionSaveResult> SaveAsync(StoredSession snapshot) => Task.Run(() =>
    {
        try
        {
            var s = snapshot.Session; var p = snapshot.Profile; var a = snapshot.Analysis;
            if (!new SessionValidator().Validate(s).IsValid || !a.IsSuccessful || a.SessionId != s.SessionId || a.ProfileId != s.ProfileId ||
                a.ProfileVersion != s.ProfileVersion || p.ProfileId != s.ProfileId || p.Version != s.ProfileVersion || p.ExerciseType != s.ExerciseType)
                return new SessionSaveResult(SessionSaveState.Failed,"The session was analyzed but could not be saved to history. Its session/profile snapshot is inconsistent.");
            using var c = connections.Open();
            using var t = c.BeginTransaction();
            if (Query(c,t,"SELECT session_id FROM training_sessions WHERE session_id=$p0", r => r.GetString(0),s.SessionId).Count > 0)
                return new SessionSaveResult(SessionSaveState.Duplicate,"This session already exists in history and was not saved again.");
            var existing = ExerciseProfileRepository.Load(c,t,p.ProfileId,p.Version);
            if (existing is not null && !ExerciseProfileRepository.SameSnapshot(existing,p))
                return new SessionSaveResult(SessionSaveState.Failed,"The session was analyzed but could not be saved to history. This profile version already has a different saved configuration.");
            if (existing is null) ExerciseProfileRepository.Insert(c,t,p);
            var now = Utc(DateTimeOffset.UtcNow);
            Execute(c,t,"INSERT INTO operators(operator_id,created_at_utc,updated_at_utc) VALUES($p0,$p1,$p1) ON CONFLICT(operator_id) DO NOTHING",s.OperatorId,now);
            Execute(c,t,"""
                INSERT INTO training_sessions(session_id,contract_version,operator_id,profile_id,profile_version,exercise_type,
                started_at_utc,ended_at_utc,source_simulator,success,simulator_score,assistant_final_score,imported_assistant_score,analysis_successful,analysis_summary,metrics_json,created_at_utc)
                VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$p9,$p10,$p11,$p12,$p13,$p14,$p15,$p16)
                """,s.SessionId,s.ContractVersion,s.OperatorId,s.ProfileId,s.ProfileVersion,s.ExerciseType,Utc(s.StartedAtUtc),Utc(s.EndedAtUtc),s.SourceSimulator,
                s.Success,s.SimulatorScore,a.FinalScore,s.AssistantFinalScore,a.IsSuccessful,a.Summary,Json(a.Metrics),now);
            SaveTelemetry(c,t,s);
            for (var i=0;i<s.Events.Count;i++)
            {
                var e=s.Events[i];
                Execute(c,t,"INSERT INTO session_events(session_id,event_id,ordinal,time_sec,event_type,entity_id,value,metadata_json) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7)",
                    s.SessionId,e.EventId,i,e.TimeSec,e.EventType,e.EntityId,e.Value,Json(e.Metadata));
            }
            for (var i=0;i<a.CriterionResults.Count;i++)
            {
                var r=a.CriterionResults[i];
                if (r.SessionId != s.SessionId) throw new InvalidOperationException("Evaluation/session identity mismatch.");
                Execute(c,t,"INSERT INTO evaluation_results(result_id,session_id,profile_id,profile_version,criterion_id,ordinal,measured_value,score,explanation) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8)",
                    r.ResultId,s.SessionId,p.ProfileId,p.Version,r.CriterionId,i,r.MeasuredValue,r.Score,r.Explanation);
            }
            for (var i=0;i<a.Recommendations.Count;i++)
            {
                var r=a.Recommendations[i];
                if (r.SessionId != s.SessionId) throw new InvalidOperationException("Recommendation/session identity mismatch.");
                Execute(c,t,"INSERT INTO recommendations(recommendation_id,session_id,ordinal,category,text,priority,evidence) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6)",
                    r.RecommendationId,s.SessionId,i,r.Category,r.Text,r.Priority,r.Evidence);
            }
            for (var i=0;i<a.Mistakes.Count;i++)
            {
                var m=a.Mistakes[i];
                Execute(c,t,"INSERT INTO detected_mistakes(session_id,ordinal,code,category,description,time_sec,related_entity_id,evidence) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7)",
                    s.SessionId,i,m.Code,m.Category,m.Description,m.TimeSec,m.RelatedEntityId,m.Evidence);
            }
            if (a.RouteAnalysis is { } route)
                Execute(c,t,"INSERT INTO route_analysis(session_id,green_time_percent,yellow_time_percent,red_time_percent,average_deviation_m,maximum_deviation_m,total_evaluated_time_sec) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6)",
                    s.SessionId,route.GreenTimePercent,route.YellowTimePercent,route.RedTimePercent,route.AverageDeviationM,route.MaximumDeviationM,route.TotalEvaluatedTimeSec);
            SaveReplay(c,t,s);
            t.Commit();
            return new SessionSaveResult(SessionSaveState.Saved,"Session saved to history.");
        }
        catch (Exception e)
        {
            Trace.TraceError("Session save: {0}", e);
            return new SessionSaveResult(SessionSaveState.Failed,"The session was analyzed but could not be saved to history.");
        }
    });
}
