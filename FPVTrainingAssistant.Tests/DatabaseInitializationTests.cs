using FPVTrainingAssistant.Data;

namespace FPVTrainingAssistant.Tests;

public sealed class DatabaseInitializationTests
{
    [Fact]
    public async Task FreshDatabaseHasVersionTablesForeignKeysAndSeededProfiles()
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        Assert.True(File.Exists(db.DatabasePath));
        Assert.Equal("2", db.Scalar("SELECT value FROM app_metadata WHERE key='schema_version'"));
        Assert.Equal(1L, db.Scalar("PRAGMA foreign_keys"));
        Assert.Equal("wal", db.Scalar("PRAGMA journal_mode"));
        foreach (var table in new[] { "operators", "training_sessions", "telemetry_samples", "session_events", "exercise_profiles", "reference_points", "evaluation_criteria", "evaluation_results", "recommendations", "detected_mistakes", "route_analysis", "replay_contexts", "replay_entity_tracks", "replay_entity_samples", "app_metadata" })
            Assert.Equal(1L, db.Scalar($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'"));
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM exercise_profiles"));
        Assert.Equal(4L, db.Scalar("SELECT COUNT(*) FROM reference_points"));
        Assert.Equal(8L, db.Scalar("SELECT COUNT(*) FROM evaluation_criteria"));
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM operators"));
    }

    [Fact]
    public async Task InitializationIsIdempotentAndDoesNotOverwriteExistingProfileVersion()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        db.Execute("UPDATE exercise_profiles SET name='Retained historical name' WHERE profile_id='synthetic-obstacle-course'");
        await db.Initialize();
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM exercise_profiles"));
        Assert.Equal("Retained historical name", db.Scalar("SELECT name FROM exercise_profiles WHERE profile_id='synthetic-obstacle-course'"));
    }

    [Fact]
    public async Task UnsupportedSchemaFailsClearlyWithoutOverwritingData()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        db.Execute("UPDATE app_metadata SET value='99' WHERE key='schema_version'");
        var result = await db.Initializer.InitializeAsync();
        Assert.False(result.IsSuccess); Assert.Contains("unsupported database schema version", result.Message);
        Assert.Equal("99", db.Scalar("SELECT value FROM app_metadata WHERE key='schema_version'"));
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM exercise_profiles"));
    }

    [Fact]
    public async Task ForeignKeysRejectOrphanedChildrenOnEveryOpenedConnection()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => db.Execute("INSERT INTO replay_contexts VALUES('absent-session','environment',NULL)"));
        Assert.Equal(1L, db.Scalar("PRAGMA foreign_keys"));
    }

    [Fact]
    public void DefaultPathIsPerUserAndHasNoDeveloperSpecificPath()
    {
        var path = new ApplicationDataPathProvider().DatabasePath;
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPVTrainingAssistant", "assistant.db"), path);
    }
}
