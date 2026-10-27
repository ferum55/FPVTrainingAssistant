using System.Diagnostics;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;
using Xunit.Abstractions;

namespace FPVTrainingAssistant.Tests;

public sealed class FinalReviewTests(ITestOutputHelper output)
{
    [Fact]
    public void ReplayDoesNotNotifyEventRowsWhenTimeMovesWithinTheSameEventInterval()
    {
        using var vm = MockReplayTestData.ViewModel();
        vm.CurrentTimeSec = 4.1;
        var changes = 0;
        foreach (var row in vm.Events) row.PropertyChanged += (_, _) => changes++;
        vm.CurrentTimeSec = 4.2;
        Assert.Equal(0, changes);
        vm.CurrentTimeSec = 6;
        Assert.True(changes > 0);
        Assert.Equal(2, vm.Events.Count(e => e.HasOccurred));
        Assert.Equal(6, Assert.Single(vm.Events, e => e.IsCurrent).TimeSec);
        changes = 0;
        vm.CurrentTimeSec = 6.1;
        Assert.Equal(0, changes);
    }

    [Fact]
    public void EventBoundaryOptimizationPreservesSimultaneousEventsAndBackwardSeeks()
    {
        using var vm = MockReplayTestData.ViewModel(edit: document =>
        {
            var events = document["events"]!.AsArray();
            events[0]!["timeSec"] = 4;
            events[1]!["timeSec"] = 4;
        });
        vm.CurrentTimeSec = 4;
        Assert.True(vm.Events[0].HasOccurred);
        Assert.True(vm.Events[1].HasOccurred);
        Assert.False(vm.Events[0].IsCurrent);
        Assert.True(vm.Events[1].IsCurrent);
        vm.CurrentTimeSec = 10;
        Assert.All(vm.Events, e => Assert.True(e.HasOccurred));
        vm.CurrentTimeSec = 4;
        Assert.True(vm.Events[1].IsCurrent);
        Assert.All(vm.Events.Skip(2), e => Assert.False(e.HasOccurred || e.IsCurrent));
        vm.CurrentTimeSec = 0;
        Assert.DoesNotContain(vm.Events, e => e.HasOccurred || e.IsCurrent);
    }

    [Fact]
    public async Task LargeSessionImportsPersistsReconstructsAndReplaysWithoutReplacingStaticPoints()
    {
        using var files = new ReplayTestFiles();
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var document = ReplayTestFiles.Document(Path.Combine("Demo", "09_replay_obstacle_course_v1_1.json"));
        var template = document["telemetry"]!.AsArray();
        var samples = new JsonArray();
        for (var i = 0; i <= 6000; i++)
        {
            var sample = template[i % template.Count]!.DeepClone();
            sample["timeSec"] = i * 0.1;
            samples.Add(sample);
        }
        document["sessionId"] = "synthetic-final-performance";
        document["sourceSimulator"] = "SyntheticPerformanceReview";
        document["endedAtUtc"] = "2026-10-24T10:10:00Z";
        document["telemetry"] = samples;
        var events = new JsonArray();
        for (var i = 0; i < 600; i++)
            events.Add(new JsonObject
            {
                ["eventId"] = "performance-event-" + i, ["timeSec"] = i + 0.5,
                ["eventType"] = i % 10 == 0 ? "Collision" : "CheckpointPassed", ["entityId"] = "synthetic-entity-" + i
            });
        document["events"] = events;
        var path = files.Create(document.ToJsonString());
        var watch = Stopwatch.StartNew();
        var import = await new SessionImportService().ImportAsync(path);
        Record("Import 6001 samples / 600 events", watch);
        Assert.True(import.IsSuccess);
        var session = import.Session!;
        var profile = (await new ExerciseProfileProvider(new ExerciseProfileRepository(db.Connections)).ResolveAsync(session, true))!;
        watch.Restart();
        var analysis = new SessionAnalyzer().AnalyzeRequestedVersion(session, profile);
        Record("Analysis", watch);
        Assert.True(analysis.IsSuccessful);
        watch.Restart();
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(new(session, profile, analysis))).State);
        Record("SQLite save", watch);
        watch.Restart();
        var stored = (await new SessionRepository(new DatabaseConnectionFactory(db)).LoadAsync(session.SessionId))!;
        Record("SQLite reconstruction", watch);
        Assert.Equal(6001, stored.Session.Telemetry.Count);
        Assert.Equal(600, stored.Session.Events.Count);
        Assert.Equal(analysis.FinalScore, stored.Analysis.FinalScore);
        watch.Restart();
        using var replay = new SimulatorReplayMockViewModel(stored.Session, stored.Profile, stored.Analysis);
        var staticPoints = replay.Playback.TrajectoryPoints;
        for (var i = 0; i < 3000; i++) replay.CurrentTimeSec = i * 0.2;
        replay.CurrentTimeSec = 600;
        Record("Mock construction + 3000 seeks", watch);
        Assert.Same(staticPoints, replay.Playback.TrajectoryPoints);
        Assert.All(replay.Events, e => Assert.True(e.HasOccurred));
        Assert.Single(replay.Events, e => e.IsCurrent);
        replay.CurrentTimeSec = 0;
        Assert.DoesNotContain(replay.Events, e => e.HasOccurred || e.IsCurrent);
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    private void Record(string phase, Stopwatch watch) =>
        output.WriteLine(FormattableString.Invariant($"{phase}: {watch.Elapsed.TotalMilliseconds:F1} ms (observed, no timing assertion)"));
}
