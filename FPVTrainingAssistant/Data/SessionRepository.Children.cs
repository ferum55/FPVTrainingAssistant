using FPVTrainingAssistant.Models;
using Microsoft.Data.Sqlite;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed partial class SessionRepository
{
    private static void SaveTelemetry(SqliteConnection c,SqliteTransaction t,TrainingSession s)
    {
        // Prepare once and reuse for the potentially large telemetry stream.
        using var command = Command(c,t,"""
            INSERT INTO telemetry_samples(session_id,time_sec,x,y,z,speed_mps,altitude_m,pitch_deg,roll_deg,yaw_deg,
            battery_percent,control_link_quality_percent,video_link_quality_percent,vertical_speed_mps,camera_tilt_deg,camera_fov_deg,
            battery_voltage_v,cell_voltage_v,current_a,consumed_mah,control_rssi_dbm,failsafe,flight_mode,armed)
            VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$p9,$p10,$p11,$p12,$p13,$p14,$p15,$p16,$p17,$p18,$p19,$p20,$p21,$p22,$p23)
            """,new object?[24]);
        command.Prepare();
        foreach (var v in s.Telemetry)
        {
            object?[] values = [s.SessionId,v.TimeSec,v.X,v.Y,v.Z,v.SpeedMps,v.AltitudeM,v.PitchDeg,v.RollDeg,v.YawDeg,
                v.BatteryPercent,v.ControlLinkQualityPercent,v.VideoLinkQualityPercent,v.VerticalSpeedMps,v.CameraTiltDeg,v.CameraFovDeg,
                v.BatteryVoltageV,v.CellVoltageV,v.CurrentA,v.ConsumedMah,v.ControlRssiDbm,v.Failsafe,v.FlightMode,v.Armed];
            for (var i=0;i<values.Length;i++) command.Parameters[i].Value=values[i] ?? DBNull.Value;
            command.ExecuteNonQuery();
        }
    }

    private static void SaveReplay(SqliteConnection c,SqliteTransaction t,TrainingSession s)
    {
        if (s.Replay is not { } replay) return;
        Execute(c,t,"INSERT INTO replay_contexts(session_id,environment_id,scenario_id) VALUES($p0,$p1,$p2)",s.SessionId,replay.EnvironmentId,replay.ScenarioId);
        for (var i=0;i<replay.Entities.Count;i++)
        {
            var track=replay.Entities[i];
            Execute(c,t,"INSERT INTO replay_entity_tracks(session_id,entity_id,ordinal,entity_type,archetype_id,metadata_json) VALUES($p0,$p1,$p2,$p3,$p4,$p5)",
                s.SessionId,track.EntityId,i,track.EntityType,track.ArchetypeId,Json(track.Metadata));
            foreach (var sample in track.Samples)
                Execute(c,t,"INSERT INTO replay_entity_samples(session_id,entity_id,time_sec,x,y,z,pitch_deg,roll_deg,yaw_deg,is_active,state,metadata_json) VALUES($p0,$p1,$p2,$p3,$p4,$p5,$p6,$p7,$p8,$p9,$p10,$p11)",
                    s.SessionId,track.EntityId,sample.TimeSec,sample.X,sample.Y,sample.Z,sample.PitchDeg,sample.RollDeg,sample.YawDeg,sample.IsActive,sample.State,Json(sample.Metadata));
        }
    }

    private static List<TelemetrySample> LoadTelemetry(SqliteConnection c,SqliteTransaction t,string id) =>
        Query(c,t,"SELECT * FROM telemetry_samples WHERE session_id=$p0 ORDER BY time_sec",r => new TelemetrySample
        {
            TimeSec=Number(r,"time_sec")!.Value,X=Number(r,"x")!.Value,Y=Number(r,"y")!.Value,Z=Number(r,"z")!.Value,
            SpeedMps=Number(r,"speed_mps")!.Value,AltitudeM=Number(r,"altitude_m")!.Value,
            PitchDeg=Number(r,"pitch_deg")!.Value,RollDeg=Number(r,"roll_deg")!.Value,YawDeg=Number(r,"yaw_deg")!.Value,
            BatteryPercent=Number(r,"battery_percent"),ControlLinkQualityPercent=Number(r,"control_link_quality_percent"),VideoLinkQualityPercent=Number(r,"video_link_quality_percent"),
            VerticalSpeedMps=Number(r,"vertical_speed_mps"),CameraTiltDeg=Number(r,"camera_tilt_deg"),CameraFovDeg=Number(r,"camera_fov_deg"),
            BatteryVoltageV=Number(r,"battery_voltage_v"),CellVoltageV=Number(r,"cell_voltage_v"),CurrentA=Number(r,"current_a"),ConsumedMah=Number(r,"consumed_mah"),
            ControlRssiDbm=Number(r,"control_rssi_dbm"),Failsafe=Bool(r,"failsafe"),FlightMode=Text(r,"flight_mode"),Armed=Bool(r,"armed")
        },id);

    private static ReplayContext? LoadReplay(SqliteConnection c,SqliteTransaction t,string id)
    {
        var context=Query(c,t,"SELECT * FROM replay_contexts WHERE session_id=$p0",r => new ReplayContext
        { EnvironmentId=Text(r,"environment_id")!,ScenarioId=Text(r,"scenario_id") },id).SingleOrDefault();
        if (context is null) return null;
        context.Entities.AddRange(Query(c,t,"SELECT * FROM replay_entity_tracks WHERE session_id=$p0 ORDER BY ordinal",r => new ReplayEntityTrack
        {
            EntityId=Text(r,"entity_id")!,EntityType=Text(r,"entity_type")!,ArchetypeId=Text(r,"archetype_id"),Metadata=ReadJson<Dictionary<string,string>>(Text(r,"metadata_json"))
        },id));
        foreach (var track in context.Entities)
            track.Samples.AddRange(Query(c,t,"SELECT * FROM replay_entity_samples WHERE session_id=$p0 AND entity_id=$p1 ORDER BY time_sec",r => new ReplayEntitySample
            {
                TimeSec=Number(r,"time_sec")!.Value,X=Number(r,"x")!.Value,Y=Number(r,"y")!.Value,Z=Number(r,"z")!.Value,
                PitchDeg=Number(r,"pitch_deg")!.Value,RollDeg=Number(r,"roll_deg")!.Value,YawDeg=Number(r,"yaw_deg")!.Value,
                IsActive=Bool(r,"is_active"),State=Text(r,"state"),Metadata=ReadJson<Dictionary<string,string>>(Text(r,"metadata_json"))
            },id,track.EntityId));
        return context;
    }
}
