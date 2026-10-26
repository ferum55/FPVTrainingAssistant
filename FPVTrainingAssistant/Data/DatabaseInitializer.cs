using System.Diagnostics;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed class DatabaseInitializer(DatabaseConnectionFactory connections) : IDatabaseInitializer
{
    // Microsoft.Data.Sqlite executes synchronous I/O; move the whole operation off the UI thread.
    public Task<DatabaseInitializationResult> InitializeAsync() => Task.Run(() =>
    {
        try
        {
            connections.CreateDirectory();
            using var c = connections.Open();
            using var t = c.BeginTransaction();
            var tables = Query(c,t,"SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", r => r.GetString(0));
            if (tables.Count > 0)
            {
                if (!tables.Contains("app_metadata")) return new DatabaseInitializationResult(false,"History is unavailable: this database has no supported schema version.");
                var version = Query(c,t,"SELECT value FROM app_metadata WHERE key='schema_version'",r => r.GetString(0)).SingleOrDefault();
                if (version != DatabaseSchema.Version) return new DatabaseInitializationResult(false,"History is unavailable: unsupported database schema version. Expected version 1.");
            }
            Execute(c,t,DatabaseSchema.Create);
            Execute(c,t,"INSERT INTO app_metadata(key,value) VALUES('schema_version',$p0) ON CONFLICT(key) DO NOTHING", DatabaseSchema.Version);
            foreach (var p in ExerciseProfileProvider.BuiltInProfiles())
                if (ExerciseProfileRepository.Load(c,t,p.ProfileId,p.Version) is null) ExerciseProfileRepository.Insert(c,t,p);
            t.Commit();
            Execute(c,null,"PRAGMA journal_mode = WAL;");
            return new DatabaseInitializationResult(true,"History storage is ready.");
        }
        catch (Exception e)
        {
            Trace.TraceError("History initialization: {0}", e);
            return new DatabaseInitializationResult(false,"History storage is unavailable. Import and analysis remain available; sessions cannot be saved.");
        }
    });
}
