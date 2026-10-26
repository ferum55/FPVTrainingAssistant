using System.IO;
using FPVTrainingAssistant.Models;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed partial class SessionRepository
{
    public Task<StoredSession?> LoadAsync(string sessionId) => Task.Run(() =>
    {
        using var c=connections.Open();
        using var t=c.BeginTransaction(deferred:true);
        var header=Query(c,t,"SELECT * FROM training_sessions WHERE session_id=$p0",r => new
        {
            Session=new TrainingSession
            {
                SessionId=Text(r,"session_id")!,ContractVersion=Text(r,"contract_version")!,OperatorId=Text(r,"operator_id")!,
                ProfileId=Text(r,"profile_id")!,ProfileVersion=r.GetInt32(r.GetOrdinal("profile_version")),ExerciseType=Text(r,"exercise_type")!,
                StartedAtUtc=Date(Text(r,"started_at_utc")!),EndedAtUtc=Date(Text(r,"ended_at_utc")!),SourceSimulator=Text(r,"source_simulator")!,
                Success=Bool(r,"success"),SimulatorScore=Number(r,"simulator_score"),AssistantFinalScore=Number(r,"imported_assistant_score"),
                Telemetry=LoadTelemetry(c,t,sessionId),Replay=LoadReplay(c,t,sessionId)
            },
            FinalScore=Number(r,"assistant_final_score")!.Value,Successful=Bool(r,"analysis_successful")!.Value,
            Summary=Text(r,"analysis_summary"),Metrics=ReadJson<ExerciseMetrics>(Text(r,"metrics_json")) ?? throw new InvalidDataException("Missing saved metrics.")
        },sessionId).SingleOrDefault();
        if (header is null) return null;
        var s=header.Session;
        s.Events.AddRange(Query(c,t,"SELECT * FROM session_events WHERE session_id=$p0 ORDER BY time_sec,ordinal",r => new SessionEvent
        {
            EventId=Text(r,"event_id")!,EventType=Text(r,"event_type")!,TimeSec=Number(r,"time_sec")!.Value,
            EntityId=Text(r,"entity_id"),Value=Text(r,"value"),Metadata=ReadJson<Dictionary<string,string>>(Text(r,"metadata_json"))
        },sessionId));
        var profile=ExerciseProfileRepository.Load(c,t,s.ProfileId,s.ProfileVersion) ?? throw new InvalidDataException("Missing saved profile version.");
        var analysis=new SessionAnalysisResult
        {
            SessionId=s.SessionId,ProfileId=s.ProfileId,ProfileVersion=s.ProfileVersion,FinalScore=header.FinalScore,
            IsSuccessful=header.Successful,Summary=header.Summary,Metrics=header.Metrics,
            CriterionResults=Query(c,t,"SELECT * FROM evaluation_results WHERE session_id=$p0 ORDER BY ordinal",r => new EvaluationResult
            { ResultId=Text(r,"result_id")!,SessionId=s.SessionId,CriterionId=Text(r,"criterion_id")!,MeasuredValue=Number(r,"measured_value"),Score=Number(r,"score")!.Value,Explanation=Text(r,"explanation")! },sessionId).AsReadOnly(),
            Recommendations=Query(c,t,"SELECT * FROM recommendations WHERE session_id=$p0 ORDER BY ordinal",r => new Recommendation
            { RecommendationId=Text(r,"recommendation_id")!,SessionId=s.SessionId,Category=Text(r,"category")!,Text=Text(r,"text")!,Priority=r.GetInt32(r.GetOrdinal("priority")),Evidence=Text(r,"evidence")! },sessionId).AsReadOnly(),
            Mistakes=Query(c,t,"SELECT * FROM detected_mistakes WHERE session_id=$p0 ORDER BY ordinal",r => new DetectedMistake
            (Text(r,"code")!,Text(r,"category")!,Text(r,"description")!,Number(r,"time_sec"),Text(r,"related_entity_id"),Text(r,"evidence")!),sessionId).AsReadOnly(),
            RouteAnalysis=Query(c,t,"SELECT * FROM route_analysis WHERE session_id=$p0",r => new RouteAnalysisResult
            (Number(r,"green_time_percent")!.Value,Number(r,"yellow_time_percent")!.Value,Number(r,"red_time_percent")!.Value,
            Number(r,"average_deviation_m")!.Value,Number(r,"maximum_deviation_m")!.Value,Number(r,"total_evaluated_time_sec")!.Value),sessionId).SingleOrDefault()
        };
        if (!new Services.SessionValidator().Validate(s).IsValid) throw new InvalidDataException("Saved session data is invalid.");
        t.Commit();
        return new StoredSession(s,profile,analysis);
    });
}
