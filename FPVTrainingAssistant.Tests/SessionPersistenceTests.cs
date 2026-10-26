using System.Text.Json;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

public sealed class SessionPersistenceTests
{
    [Theory]
    [InlineData("valid_obstacle_course.json", 82.67)]
    [InlineData("valid_target_mission.json", 90)]
    [InlineData("replay_obstacle_course_v1_1.json", 82.67)]
    [InlineData("replay_target_mission_v1_1.json", 90)]
    public async Task CompleteSnapshotRoundTripsWithoutAnalyzerOrSourcePath(string file, double score)
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var original = PersistenceTestDatabase.Snapshot(file);
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(original)).State);
        var loaded = await new SessionRepository(new DatabaseConnectionFactory(db)).LoadAsync(original.Session.SessionId);
        Assert.NotNull(loaded);
        Assert.Equal(JsonSerializer.Serialize(original.Session), JsonSerializer.Serialize(loaded.Session));
        Assert.Equal(JsonSerializer.Serialize(original.Profile), JsonSerializer.Serialize(loaded.Profile));
        Assert.Equal(JsonSerializer.Serialize(original.Analysis), JsonSerializer.Serialize(loaded.Analysis));
        Assert.Equal(score, loaded.Analysis.FinalScore);
        Assert.Equal(750, loaded.Session.SimulatorScore);
        Assert.Null(loaded.Session.AssistantFinalScore);
        Assert.Equal(score, db.Scalar("SELECT assistant_final_score FROM training_sessions"));
        Assert.Equal(21L, db.Scalar("SELECT COUNT(*) FROM telemetry_samples"));
        Assert.Equal(original.Analysis.CriterionResults.Count, Convert.ToInt32(db.Scalar("SELECT COUNT(*) FROM evaluation_results")));
        Assert.Equal(original.Analysis.Recommendations.Count, Convert.ToInt32(db.Scalar("SELECT COUNT(*) FROM recommendations")));
        Assert.Equal(original.Analysis.Mistakes.Count, Convert.ToInt32(db.Scalar("SELECT COUNT(*) FROM detected_mistakes")));
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
        var trajectory = new TrajectoryViewModel(loaded.Session, loaded.Profile, loaded.Analysis);
        Assert.True(trajectory.CanReplay);
        trajectory.CurrentTimeSec = 3.25;
        Assert.Equal(3.25, trajectory.CurrentState!.TimeSec);
        if (loaded.Session.ContractVersion == "1.1")
        {
            using var mock = new SimulatorReplayMockViewModel(loaded.Session, loaded.Profile, loaded.Analysis);
            Assert.True(mock.IsAvailable); Assert.Single(mock.DynamicEntities);
            Assert.True(new ReplayCapabilityService().Evaluate(loaded.Session, null, null).CanPreviewReplay);
            Assert.Equal(6L, db.Scalar("SELECT COUNT(*) FROM replay_entity_samples"));
        }
        else Assert.Null(loaded.Session.Replay);
    }

    [Fact]
    public async Task DuplicateRetainsExistingValuesAndDoesNotMergeChildren()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var first = PersistenceTestDatabase.Snapshot();
        var second = PersistenceTestDatabase.Snapshot(edit: d => { d["events"] = new JsonArray(); d["simulatorScore"] = 123; });
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(first)).State);
        Assert.Equal(SessionSaveState.Duplicate, (await db.Sessions.SaveAsync(second)).State);
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM training_sessions"));
        Assert.Equal(21L, db.Scalar("SELECT COUNT(*) FROM telemetry_samples"));
        Assert.Equal(4L, db.Scalar("SELECT COUNT(*) FROM session_events"));
        Assert.Equal(JsonSerializer.Serialize(first.Analysis), JsonSerializer.Serialize((await db.Sessions.LoadAsync(first.Session.SessionId))!.Analysis));
        Assert.Equal(750d, db.Scalar("SELECT simulator_score FROM training_sessions"));
    }

    [Fact]
    public async Task LateDatabaseFailureRollsBackEveryPartIncludingNewOperatorAndProfile()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        db.Execute("CREATE TRIGGER controlled_failure BEFORE INSERT ON replay_entity_samples BEGIN SELECT RAISE(ABORT,'controlled test failure'); END;");
        var original = PersistenceTestDatabase.Snapshot();
        var json = JsonSerializer.SerializeToNode(original.Profile)!; json["ProfileId"] = "rollback-profile";
        var profile = json.Deserialize<ExerciseProfile>()!;
        var document = ReplayTestFiles.Document(); document["profileId"] = "rollback-profile";
        var session = ReplayTestFiles.Session(document);
        var snapshot = new StoredSession(session, profile, new SessionAnalyzer().Analyze(session, profile));
        Assert.Equal(SessionSaveState.Failed, (await db.Sessions.SaveAsync(snapshot)).State);
        foreach (var table in new[] { "training_sessions", "operators", "telemetry_samples", "session_events", "evaluation_results", "recommendations", "detected_mistakes", "route_analysis", "replay_contexts", "replay_entity_tracks", "replay_entity_samples" })
            Assert.Equal(0L, db.Scalar($"SELECT COUNT(*) FROM {table}"));
        Assert.Equal(2L, db.Scalar("SELECT COUNT(*) FROM exercise_profiles"));
    }

    [Fact]
    public async Task NewProfileVersionKeepsBothConfigurationsAndReopensItsExactSnapshot()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var original = PersistenceTestDatabase.Snapshot();
        var profileJson = JsonSerializer.SerializeToNode(original.Profile)!;
        profileJson["Version"] = 2; profileJson["Name"] = "Version two snapshot"; profileJson["Criteria"]![0]!["Weight"] = 17;
        var profile = profileJson.Deserialize<ExerciseProfile>()!;
        var document = ReplayTestFiles.Document(); document["profileVersion"] = 2; document["sessionId"] = "synthetic-version-two";
        var session = ReplayTestFiles.Session(document);
        var snapshot = new StoredSession(session, profile, new SessionAnalyzer().Analyze(session, profile));
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(snapshot)).State);
        var loaded = (await db.Sessions.LoadAsync(session.SessionId))!;
        Assert.Equal(2, loaded.Profile.Version); Assert.Equal("Version two snapshot", loaded.Profile.Name);
        Assert.Equal(17, loaded.Profile.Criteria[0].Weight);
        var baseline = await new ExerciseProfileRepository(db.Connections).LoadAsync(original.Profile.ProfileId, 1);
        Assert.Equal(original.Profile.Criteria[0].Weight, baseline!.Criteria[0].Weight);
        Assert.Equal(3L, db.Scalar("SELECT COUNT(*) FROM exercise_profiles"));
    }

    [Fact]
    public async Task ProfileVersionConflictDoesNotOverwriteHistoricalConfiguration()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        db.Execute("UPDATE evaluation_criteria SET weight=17 WHERE criterion_id='CheckpointAccuracy'");
        var result = await db.Sessions.SaveAsync(PersistenceTestDatabase.Snapshot());
        Assert.Equal(SessionSaveState.Failed, result.State);
        Assert.Contains("different saved configuration", result.Message);
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM training_sessions"));
        Assert.Equal(17d, db.Scalar("SELECT weight FROM evaluation_criteria WHERE criterion_id='CheckpointAccuracy'"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("true")]
    public async Task NullableBooleansAndOpenMetadataRoundTrip(string boolean)
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var snapshot = PersistenceTestDatabase.Snapshot(edit: d =>
        {
            d["success"] = JsonNode.Parse(boolean);
            d["assistantFinalScore"] = 12;
            foreach (var sample in d["telemetry"]!.AsArray()) { sample!["failsafe"] = JsonNode.Parse(boolean); sample["armed"] = JsonNode.Parse(boolean); sample["flightMode"] = "Future flight mode"; }
            var track = d["replay"]!["entities"]![0]!; track["entityType"] = "FutureEntity"; track["archetypeId"] = "future.visual"; track["metadata"] = new JsonObject();
            track["samples"]![0]!["isActive"] = JsonNode.Parse(boolean); track["samples"]![0]!["state"] = "FutureState"; track["samples"]![0]!["metadata"] = null;
            d["events"]![0]!["eventType"] = "FutureEvent"; d["events"]![0]!["metadata"] = new JsonObject { ["CaseSensitiveKey"] = "' ; DROP TABLE operators; --" };
        });
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(snapshot)).State);
        var loaded = (await db.Sessions.LoadAsync(snapshot.Session.SessionId))!;
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(loaded));
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM operators"));
        Assert.Equal(12, loaded.Session.AssistantFinalScore);
    }

    [Fact]
    public async Task SimultaneousEventOrderAndRepeatedMistakeCodesSurviveStorage()
    {
        using var db = new PersistenceTestDatabase(); await db.Initialize();
        var snapshot = PersistenceTestDatabase.Snapshot(edit: d =>
        {
            var events = d["events"]!.AsArray(); var collision = events.First(e => e!["eventType"]!.GetValue<string>() == "Collision")!.DeepClone();
            collision["eventId"] = "another-collision"; events.Add(collision);
        });
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(snapshot)).State);
        var loaded = (await db.Sessions.LoadAsync(snapshot.Session.SessionId))!;
        Assert.Equal(snapshot.Session.Events.OrderBy(e => e.TimeSec).Select(e => e.EventId), loaded.Session.Events.Select(e => e.EventId));
        Assert.Equal(2, loaded.Analysis.Mistakes.Count(m => m.Code == "COLLISION"));
    }
}
