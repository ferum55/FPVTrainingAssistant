namespace FPVTrainingAssistant.Data;

internal static class DatabaseSchema
{
    public const string Version = "2";
    public const string UpgradeToVersion2 = """
        CREATE TABLE IF NOT EXISTS app_settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
        INSERT INTO app_settings(key,value) VALUES('default_replay_speed','1') ON CONFLICT(key) DO NOTHING;
        INSERT INTO app_settings(key,value) VALUES('show_minimap','True') ON CONFLICT(key) DO NOTHING;
        INSERT INTO app_settings(key,value) VALUES('show_event_log','True') ON CONFLICT(key) DO NOTHING;
        """;
    public const string Create = """
        CREATE TABLE IF NOT EXISTS app_metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS operators(
            operator_id TEXT PRIMARY KEY, display_name TEXT, notes TEXT,
            created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS exercise_profiles(
            profile_id TEXT NOT NULL, version INTEGER NOT NULL CHECK(version > 0), name TEXT NOT NULL,
            exercise_type TEXT NOT NULL, is_active INTEGER NOT NULL CHECK(is_active IN (0,1)),
            PRIMARY KEY(profile_id, version));
        CREATE TABLE IF NOT EXISTS reference_points(
            profile_id TEXT NOT NULL, profile_version INTEGER NOT NULL, sequence_no INTEGER NOT NULL,
            x REAL NOT NULL, y REAL NOT NULL, z REAL NOT NULL, point_type TEXT NOT NULL,
            green_radius_m REAL NOT NULL CHECK(green_radius_m >= 0), yellow_radius_m REAL NOT NULL CHECK(yellow_radius_m >= green_radius_m),
            PRIMARY KEY(profile_id, profile_version, sequence_no),
            FOREIGN KEY(profile_id, profile_version) REFERENCES exercise_profiles(profile_id,version));
        CREATE TABLE IF NOT EXISTS evaluation_criteria(
            profile_id TEXT NOT NULL, profile_version INTEGER NOT NULL, criterion_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
            name TEXT NOT NULL, description TEXT NOT NULL, weight REAL NOT NULL, rule_type TEXT NOT NULL,
            is_enabled INTEGER NOT NULL CHECK(is_enabled IN (0,1)), threshold REAL, parameters_json TEXT,
            PRIMARY KEY(profile_id,profile_version,criterion_id),
            UNIQUE(profile_id,profile_version,ordinal),
            FOREIGN KEY(profile_id,profile_version) REFERENCES exercise_profiles(profile_id,version));
        CREATE TABLE IF NOT EXISTS training_sessions(
            session_id TEXT PRIMARY KEY, contract_version TEXT NOT NULL, operator_id TEXT NOT NULL,
            profile_id TEXT NOT NULL, profile_version INTEGER NOT NULL, exercise_type TEXT NOT NULL,
            started_at_utc TEXT NOT NULL, ended_at_utc TEXT NOT NULL, source_simulator TEXT NOT NULL,
            success INTEGER CHECK(success IN (0,1)), simulator_score REAL, assistant_final_score REAL NOT NULL CHECK(assistant_final_score BETWEEN 0 AND 100),
            imported_assistant_score REAL CHECK(imported_assistant_score BETWEEN 0 AND 100),
            analysis_successful INTEGER NOT NULL CHECK(analysis_successful IN (0,1)), analysis_summary TEXT,
            metrics_json TEXT NOT NULL, created_at_utc TEXT NOT NULL,
            UNIQUE(session_id,profile_id,profile_version),
            FOREIGN KEY(operator_id) REFERENCES operators(operator_id),
            FOREIGN KEY(profile_id,profile_version) REFERENCES exercise_profiles(profile_id,version));
        CREATE INDEX IF NOT EXISTS ix_sessions_operator_time ON training_sessions(operator_id,started_at_utc DESC,session_id);
        CREATE TABLE IF NOT EXISTS telemetry_samples(
            session_id TEXT NOT NULL, time_sec REAL NOT NULL CHECK(time_sec >= 0),
            x REAL NOT NULL, y REAL NOT NULL, z REAL NOT NULL, speed_mps REAL NOT NULL, altitude_m REAL NOT NULL,
            pitch_deg REAL NOT NULL, roll_deg REAL NOT NULL, yaw_deg REAL NOT NULL,
            battery_percent REAL, control_link_quality_percent REAL, video_link_quality_percent REAL,
            vertical_speed_mps REAL, camera_tilt_deg REAL, camera_fov_deg REAL,
            battery_voltage_v REAL, cell_voltage_v REAL, current_a REAL, consumed_mah REAL,
            control_rssi_dbm REAL, failsafe INTEGER CHECK(failsafe IN (0,1)), flight_mode TEXT, armed INTEGER CHECK(armed IN (0,1)),
            PRIMARY KEY(session_id,time_sec), FOREIGN KEY(session_id) REFERENCES training_sessions(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS session_events(
            session_id TEXT NOT NULL, event_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
            time_sec REAL NOT NULL, event_type TEXT NOT NULL, entity_id TEXT, value TEXT, metadata_json TEXT,
            PRIMARY KEY(session_id,event_id), UNIQUE(session_id,ordinal),
            FOREIGN KEY(session_id) REFERENCES training_sessions(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS evaluation_results(
            result_id TEXT PRIMARY KEY, session_id TEXT NOT NULL, profile_id TEXT NOT NULL, profile_version INTEGER NOT NULL,
            criterion_id TEXT NOT NULL, ordinal INTEGER NOT NULL, measured_value REAL,
            score REAL NOT NULL CHECK(score BETWEEN 0 AND 100), explanation TEXT NOT NULL,
            FOREIGN KEY(session_id,profile_id,profile_version) REFERENCES training_sessions(session_id,profile_id,profile_version) ON DELETE CASCADE,
            FOREIGN KEY(profile_id,profile_version,criterion_id) REFERENCES evaluation_criteria(profile_id,profile_version,criterion_id));
        CREATE TABLE IF NOT EXISTS recommendations(
            recommendation_id TEXT PRIMARY KEY, session_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
            category TEXT NOT NULL, text TEXT NOT NULL, priority INTEGER NOT NULL, evidence TEXT NOT NULL,
            FOREIGN KEY(session_id) REFERENCES training_sessions(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS detected_mistakes(
            id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
            code TEXT NOT NULL, category TEXT NOT NULL, description TEXT NOT NULL,
            time_sec REAL, related_entity_id TEXT, evidence TEXT NOT NULL,
            FOREIGN KEY(session_id) REFERENCES training_sessions(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS route_analysis(
            session_id TEXT PRIMARY KEY, green_time_percent REAL NOT NULL, yellow_time_percent REAL NOT NULL,
            red_time_percent REAL NOT NULL, average_deviation_m REAL NOT NULL, maximum_deviation_m REAL NOT NULL,
            total_evaluated_time_sec REAL NOT NULL,
            FOREIGN KEY(session_id) REFERENCES training_sessions(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS replay_contexts(
            session_id TEXT PRIMARY KEY, environment_id TEXT NOT NULL, scenario_id TEXT,
            FOREIGN KEY(session_id) REFERENCES training_sessions(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS replay_entity_tracks(
            session_id TEXT NOT NULL, entity_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
            entity_type TEXT NOT NULL, archetype_id TEXT, metadata_json TEXT,
            PRIMARY KEY(session_id,entity_id),
            FOREIGN KEY(session_id) REFERENCES replay_contexts(session_id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS replay_entity_samples(
            session_id TEXT NOT NULL, entity_id TEXT NOT NULL, time_sec REAL NOT NULL,
            x REAL NOT NULL, y REAL NOT NULL, z REAL NOT NULL, pitch_deg REAL NOT NULL, roll_deg REAL NOT NULL, yaw_deg REAL NOT NULL,
            is_active INTEGER CHECK(is_active IN (0,1)), state TEXT, metadata_json TEXT,
            PRIMARY KEY(session_id,entity_id,time_sec),
            FOREIGN KEY(session_id,entity_id) REFERENCES replay_entity_tracks(session_id,entity_id) ON DELETE CASCADE);
        """;
}
