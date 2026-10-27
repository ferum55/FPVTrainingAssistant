using System.Text.Json;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

// Final laboratory data travels through the real importer, persisted profiles, analyzer and SQLite.
public sealed class LaboratoryScenarioTests
{
    public static TheoryData<string, bool> DemoFixtures => new()
    {
        { "01_valid_obstacle_course.json", true },
        { "02_invalid_session.json", false },
        { "03_obstacle_course_with_errors.json", true },
        { "04_target_mission.json", true },
        { "05_target_mission_with_errors.json", true },
        { "06_history_progress_01.json", true },
        { "07_history_progress_02.json", true },
        { "08_history_progress_03.json", true },
        { "09_replay_obstacle_course_v1_1.json", true },
        { "10_replay_target_mission_v1_1.json", true }
    };

    private static string DemoPath(string name) => ReplayTestFiles.FixturePath(Path.Combine("Demo", name));

    [Theory]
    [MemberData(nameof(DemoFixtures))]
    public async Task EveryFinalFixtureHasItsDocumentedImportOutcomeOnFreshStorage(string file, bool valid)
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var imported = await new SessionImportService().ImportAsync(DemoPath(file));
        Assert.Null(imported.TechnicalError);
        Assert.Equal(valid, imported.IsSuccess);
        if (!valid)
        {
            Assert.Null(imported.Session);
            var issue = Assert.Single(imported.Validation.Issues);
            Assert.Equal("RequiredField", issue.Code);
            Assert.Equal("$.sessionId", issue.Path);
            Assert.Empty(await db.History.ListOperatorsAsync());
            return;
        }

        Assert.Empty(imported.Validation.Issues);
        var session = imported.Session!;
        Assert.Null(session.AssistantFinalScore);
        Assert.Equal("SyntheticLaboratoryDemo", session.SourceSimulator);
        Assert.Equal(1, session.ProfileVersion);
        var profile = await new ExerciseProfileProvider(new ExerciseProfileRepository(db.Connections)).ResolveAsync(session, true);
        Assert.NotNull(profile);
        var analysis = new SessionAnalyzer().AnalyzeRequestedVersion(session, profile);
        Assert.True(analysis.IsSuccessful);
        Assert.InRange(analysis.FinalScore, 0, 100);
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(new(session, profile, analysis))).State);
        Assert.NotNull(await db.Sessions.LoadAsync(session.SessionId));
    }

    [Fact]
    public async Task DatasetHasExactlyTheManifestFilesUniqueIdsAndUnchangedReplayBaselines()
    {
        var directory = Path.GetDirectoryName(DemoPath("01_valid_obstacle_course.json"))!;
        var files = Directory.GetFiles(directory, "*.json").OrderBy(Path.GetFileName).ToArray();
        Assert.Equal(DemoFixtures.Select(row => (string)row[0]).Order(), files.Select(p => Path.GetFileName(p)!).Order());
        var ids = new HashSet<string>();
        var versions = new HashSet<string>();
        foreach (var path in files.Where(p => Path.GetFileName(p) != "02_invalid_session.json"))
        {
            var import = await new SessionImportService().ImportAsync(path);
            Assert.True(import.IsSuccess);
            Assert.True(ids.Add(import.Session!.SessionId));
            versions.Add(import.Session.ContractVersion);
        }
        Assert.Equal(new[] { "1.0", "1.1" }, versions.Order());
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var obstacle = await ImportAnalyzeSave(db, "09_replay_obstacle_course_v1_1.json");
        var target = await ImportAnalyzeSave(db, "10_replay_target_mission_v1_1.json");
        Assert.Equal(82.67, obstacle.Analysis.FinalScore, 2);
        Assert.Equal(90, target.Analysis.FinalScore, 2);
    }

    [Fact]
    public async Task Lab1ValidImportDisplaysTheResultAndSavesItToHistory()
    {
        using var db = new PersistenceTestDatabase();
        var vm = CreateMain(db, new SessionAnalyzer());
        await vm.InitializePersistenceAsync();
        Assert.True(vm.PersistenceAvailable);
        await vm.ImportFileAsync(DemoPath("01_valid_obstacle_course.json"));
        Assert.True(vm.IsResultScreen);
        Assert.False(vm.ImportView.HasFailure);
        Assert.Empty(vm.ValidationWarnings);
        Assert.Equal(1, vm.Profile!.Version);
        Assert.True(vm.AnalysisResult!.IsSuccessful);
        Assert.Equal(100, vm.ResultView!.AssistantScore);
        Assert.Equal(SessionSaveState.Saved, vm.LastSaveResult!.State);
        var history = Assert.Single(await db.History.ListSessionsAsync(vm.ImportedSession!.OperatorId, null));
        Assert.Equal(vm.ImportedSession.SessionId, history.SessionId);
        Assert.Equal(vm.ResultView.AssistantScore, history.AssistantScore);
    }

    [Fact]
    public async Task Lab2InvalidImportNeverCallsAnalysisOrPersistsAnythingAndCanRecover()
    {
        using var db = new PersistenceTestDatabase();
        var analyzer = new CountingAnalyzer();
        var vm = CreateMain(db, analyzer);
        await vm.InitializePersistenceAsync();
        await vm.ImportFileAsync(DemoPath("02_invalid_session.json"));
        Assert.True(vm.ImportView.HasFailure);
        Assert.True(vm.IsImportScreen);
        Assert.Null(vm.ImportedSession);
        Assert.Null(vm.AnalysisResult);
        Assert.Null(vm.LastSaveResult);
        Assert.False(vm.HasResult);
        Assert.False(vm.HasTrajectory);
        Assert.Equal(0, analyzer.Calls);
        Assert.Empty(await db.History.ListOperatorsAsync());
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM training_sessions"));
        await vm.ImportFileAsync(DemoPath("01_valid_obstacle_course.json"));
        Assert.Equal(1, analyzer.Calls);
        Assert.True(vm.IsResultScreen);
    }

    [Fact]
    public async Task Lab3ObstacleErrorsHaveCheckpointsCollisionsAllRouteZonesAndReplay()
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var saved = await ImportAnalyzeSave(db, "03_obstacle_course_with_errors.json");
        var result = saved.Analysis;
        Assert.Equal(ExerciseTypes.ObstacleCourse, saved.Session.ExerciseType);
        Assert.Equal(new[] { RuleTypes.CheckpointAccuracy, RuleTypes.CollisionAvoidance, RuleTypes.CompletionTime, RuleTypes.RouteAdherence },
            result.CriterionResults.Select(r => r.CriterionId).Order());
        Assert.Equal(2, result.Metrics.PassedCheckpointCount);
        Assert.Equal(1, result.Metrics.MissedCheckpointCount);
        Assert.Equal(1, result.Metrics.CollisionCount);
        Assert.Equal(200d / 3, result.Metrics.CheckpointAccuracyPercent, 8);
        Assert.Equal(80, Assert.Single(result.CriterionResults, r => r.CriterionId == RuleTypes.CollisionAvoidance).Score);
        var route = Assert.IsType<RouteAnalysisResult>(result.RouteAnalysis);
        Assert.True(route.GreenTimePercent > 0 && route.YellowTimePercent > 0 && route.RedTimePercent > 0);
        Assert.Equal(80, route.GreenTimePercent, 8);
        Assert.Equal(5, route.YellowTimePercent, 8);
        Assert.Equal(15, route.RedTimePercent, 8);
        Assert.Equal(100, route.GreenTimePercent + route.YellowTimePercent + route.RedTimePercent, 8);
        Assert.True(route.AverageDeviationM > 0);
        Assert.True(route.MaximumDeviationM > 3);
        Assert.Equal(4.5, route.MaximumDeviationM, 8);
        Assert.Equal(77.42, result.FinalScore, 2);
        Assert.Equal(saved.Session.Duration.TotalSeconds, route.TotalEvaluatedTimeSec);
        Assert.Contains(result.Mistakes, m => m.Code == "MISSED_CHECKPOINT");
        Assert.Contains(result.Mistakes, m => m.Code == "COLLISION");
        Assert.Contains(result.Mistakes, m => m.Code == "ROUTE_DEVIATION");
        Assert.Contains(result.Recommendations, r => r.Category == "RouteAdherence");
        var trajectory = new TrajectoryViewModel(saved.Session, saved.Profile, result);
        Assert.True(trajectory.CanReplay);
        Assert.Equal(21, saved.Session.Telemetry.Count);
        Assert.True(saved.Profile.ReferencePoints.Count >= 2);
        Assert.False(new ReplayCapabilityService().Evaluate(saved.Session, null, null).CanPreviewReplay);
    }

    [Theory]
    [InlineData("04_target_mission.json", false, 90)]
    [InlineData("05_target_mission_with_errors.json", true, 17.5)]
    public async Task Lab4TargetResultsInterpretDetectionHitDamageAndFailure(string file, bool weak, double score)
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var saved = await ImportAnalyzeSave(db, file);
        var result = saved.Analysis;
        Assert.Equal(ExerciseTypes.TargetMission, saved.Session.ExerciseType);
        Assert.Equal(new[] { RuleTypes.MissionSuccess, RuleTypes.TargetDamage, RuleTypes.TargetHit, RuleTypes.TargetIdentification },
            result.CriterionResults.Select(r => r.CriterionId).Order());
        Assert.Equal(1, result.Metrics.CorrectDetectionCount);
        Assert.Equal(weak ? 1 : 0, result.Metrics.FalseDetectionCount);
        Assert.Equal(weak ? 0 : 1, result.Metrics.TargetHitCount);
        Assert.Equal(weak ? 50 : 100, result.Metrics.DetectionAccuracyPercent);
        Assert.Equal(!weak, result.Metrics.MissionSuccess);
        Assert.Equal(weak ? Array.Empty<string>() : new[] { "central" }, result.Metrics.DamageZones);
        Assert.Equal(score, result.FinalScore, 2);
        Assert.All(result.CriterionResults, r => Assert.False(string.IsNullOrWhiteSpace(r.Explanation)));
        if (weak)
        {
            Assert.Contains(result.Mistakes, m => m.Code == "FALSE_TARGET_DETECTION");
            Assert.Contains(result.Mistakes, m => m.Code == "TARGET_NOT_HIT");
            Assert.Contains(result.Mistakes, m => m.Code == "MISSION_FAILED");
            Assert.NotEmpty(result.Recommendations);
        }
        else Assert.Empty(result.Mistakes);
    }

    [Theory]
    [InlineData("03_obstacle_course_with_errors.json")]
    [InlineData("05_target_mission_with_errors.json")]
    public async Task Lab5WeightedScoresAndGuidanceAreExplainedByRecordedEvidence(string file)
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var saved = await ImportAnalyzeSave(db, file);
        var enabled = saved.Profile.Criteria.Where(c => c.IsEnabled).ToArray();
        Assert.Equal(enabled.Length, saved.Analysis.CriterionResults.Count);
        var totalWeight = enabled.Sum(c => c.Weight);
        var weighted = saved.Analysis.CriterionResults.Sum(r =>
            r.Score * enabled.Single(c => c.CriterionId == r.CriterionId).Weight / totalWeight);
        Assert.Equal(Math.Round(weighted, 2, MidpointRounding.AwayFromZero), saved.Analysis.FinalScore);
        Assert.All(saved.Analysis.CriterionResults, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.CriterionId));
            Assert.False(string.IsNullOrWhiteSpace(r.Explanation));
            Assert.InRange(r.Score, 0, 100);
            Assert.NotNull(r.MeasuredValue);
        });
        Assert.NotEmpty(saved.Analysis.Mistakes);
        Assert.All(saved.Analysis.Mistakes, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Evidence));
            if (m.TimeSec is { } time)
                Assert.Contains(saved.Session.Events, e => e.TimeSec == time && e.EntityId == m.RelatedEntityId && m.Evidence.Contains(e.EventId));
        });
        Assert.NotEmpty(saved.Analysis.Recommendations);
        Assert.All(saved.Analysis.Recommendations, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Text));
            Assert.InRange(r.Priority, 1, 3);
            Assert.Contains(saved.Analysis.Mistakes, m => r.Evidence.Contains(m.Evidence, StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task Lab6HistorySurvivesRecreationAndReopensPersistedResultsWithoutAnalysis()
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var sessions = new List<StoredSession>();
        foreach (var file in new[] { "06_history_progress_01.json", "07_history_progress_02.json", "08_history_progress_03.json" })
            sessions.Add(await ImportAnalyzeSave(db, file));
        Assert.Equal(new[] { 56d, 82.67, 100 }, sessions.Select(s => s.Analysis.FinalScore));
        var op = Assert.Single(await db.History.ListOperatorsAsync());
        Assert.Equal("demo-operator-01", op.Profile.OperatorId);
        Assert.Equal(3, op.SessionCount);
        Assert.Equal((56 + 82.67 + 100) / 3, op.AverageScore!.Value, 8);

        // Recreate all workflow objects on the same file, as on application restart.
        var vm = CreateMain(db, new ForbiddenAnalyzer());
        await vm.InitializePersistenceAsync();
        await vm.NavigateHistoryCommand.ExecuteAsync();
        var history = vm.HistoryView!;
        Assert.Equal(3, history.Sessions.Count);
        Assert.Equal("79.56", history.AverageScoreText);
        Assert.Equal("100.00", history.BestScoreText);
        Assert.Equal("100.00", history.LatestScoreText);
        Assert.Equal(new[] { 56d, 82.67, 100 }, history.ProgressPoints.Select(p => p.Score));
        Assert.Equal(sessions.Select(s => s.Session.StartedAtUtc), history.ProgressPoints.Select(p => p.StartedAtUtc));
        history.SelectedSession = history.Sessions.Single(s => s.SessionId == "demo-progress-01");
        await history.OpenResultCommand.ExecuteAsync();
        Assert.True(vm.IsResultScreen);
        Assert.Equal("Historical session", vm.ResultView!.Origin);
        Assert.Null(vm.SourceFilePath);
        Assert.Equal(JsonSerializer.Serialize(sessions[0].Analysis), JsonSerializer.Serialize(vm.AnalysisResult));
        Assert.True(vm.HasTrajectory);
        vm.NavigateTrajectoryCommand.Execute(null);
        Assert.True(vm.IsTrajectoryScreen);
        Assert.True(vm.ReplayCapability!.CanPreviewReplay);
        Assert.Equal("synthetic-training-ground-01", vm.ImportedSession!.Replay!.EnvironmentId);
        Assert.NotEmpty(vm.ImportedSession.Replay.Entities);
        using var mock = new SimulatorReplayMockViewModel(vm.ImportedSession, vm.Profile!, vm.AnalysisResult!);
        Assert.True(mock.IsAvailable);
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task DemoVersionOneHistoryAndBothBaselinesSurviveSettingsVersionTwoActivation()
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var obstacle = await ImportAnalyzeSave(db, "09_replay_obstacle_course_v1_1.json");
        var target = await ImportAnalyzeSave(db, "10_replay_target_mission_v1_1.json");
        var before = JsonSerializer.Serialize(obstacle.Analysis);
        using var editor = new ProfileEditorViewModel(obstacle.Profile);
        editor.Criteria.Single(c => c.RuleType == RuleTypes.CollisionAvoidance).Parameters[0].ValueText = "40";
        var profiles = new ExerciseProfileRepository(db.Connections);
        var saved = await profiles.CreateNewVersionAsync(editor.Build(out _)!, 1, true);
        Assert.Equal(ProfileSaveState.Saved, saved.State);
        Assert.Equal(2, (await profiles.GetActiveVersionAsync(obstacle.Profile.ProfileId))!.Version);
        var v1 = (await profiles.LoadAsync(obstacle.Profile.ProfileId, 1))!;
        Assert.False(v1.IsActive);
        Assert.Equal(JsonSerializer.Serialize(obstacle.Profile.Criteria), JsonSerializer.Serialize(v1.Criteria));
        Assert.Equal(JsonSerializer.Serialize(obstacle.Profile.ReferencePoints), JsonSerializer.Serialize(v1.ReferencePoints));
        Assert.Equal(before, JsonSerializer.Serialize((await db.Sessions.LoadAsync(obstacle.Session.SessionId))!.Analysis));
        var provider = new ExerciseProfileProvider(profiles);
        Assert.Equal(1, (await provider.ResolveAsync(obstacle.Session, true))!.Version);
        Assert.Equal(82.67, new SessionAnalyzer().AnalyzeRequestedVersion(obstacle.Session, v1).FinalScore, 2);
        Assert.Equal(90, target.Analysis.FinalScore, 2);
    }

    [Theory]
    [InlineData("09_replay_obstacle_course_v1_1.json")]
    [InlineData("10_replay_target_mission_v1_1.json")]
    public async Task FinalReplayFixturesDriveBothCameraModesMinimapEventsAndEntities(string file)
    {
        using var db = new PersistenceTestDatabase();
        await db.Initialize();
        var saved = await ImportAnalyzeSave(db, file);
        var stored = (await new SessionRepository(db.Connections).LoadAsync(saved.Session.SessionId))!;
        using var vm = new SimulatorReplayMockViewModel(stored.Session, stored.Profile, stored.Analysis);
        Assert.True(vm.IsAvailable);
        Assert.True(vm.IsFpVMode);
        Assert.True(vm.IsMinimapVisible && vm.IsEventLogVisible);
        Assert.NotNull(vm.CurrentReplayState!.BatteryVoltageV);
        Assert.NotNull(vm.CurrentReplayState.CameraTiltDeg);
        Assert.Equal(21, vm.Minimap.Trajectory.Count);
        vm.CurrentTimeSec = 5;
        var entity = Assert.Single(vm.DynamicEntities);
        Assert.True(entity.IsActive);
        Assert.Equal(6, entity.Transform!.X, 8);
        var projected = vm.Minimap.Project(vm.MinimapFrame, 400, 250);
        Assert.NotNull(projected.Drone);
        Assert.Single(projected.Entities);
        Assert.Contains(vm.Events, e => e.HasOccurred);
        Assert.Contains(vm.Events, e => !e.HasOccurred);
        vm.SetThirdPersonModeCommand.Execute(null);
        Assert.True(vm.IsThirdPersonMode);
        Assert.Equal(5, vm.CurrentTimeSec);
        vm.SetFpVModeCommand.Execute(null);
        Assert.True(vm.IsFpVMode);
        Assert.Equal(5, vm.CurrentTimeSec);
        vm.CurrentTimeSec = 10;
        Assert.All(vm.Events, e => Assert.True(e.HasOccurred));
        Assert.Single(vm.Events, e => e.IsCurrent);
    }

    private static async Task<StoredSession> ImportAnalyzeSave(PersistenceTestDatabase db, string file)
    {
        var import = await new SessionImportService().ImportAsync(DemoPath(file));
        Assert.True(import.IsSuccess);
        var session = import.Session!;
        var profile = await new ExerciseProfileProvider(new ExerciseProfileRepository(db.Connections)).ResolveAsync(session, true);
        Assert.NotNull(profile);
        var snapshot = new StoredSession(session, profile, new SessionAnalyzer().AnalyzeRequestedVersion(session, profile));
        Assert.Equal(SessionSaveState.Saved, (await db.Sessions.SaveAsync(snapshot)).State);
        return snapshot;
    }

    private static MainViewModel CreateMain(PersistenceTestDatabase db, ISessionAnalyzer analyzer)
    {
        var connections = new DatabaseConnectionFactory(db);
        var profiles = new ExerciseProfileRepository(connections);
        return new(new NoDialog(), new SessionImportService(), new ExerciseProfileProvider(profiles), analyzer, null,
            new SessionRepository(connections), new HistoryRepository(connections), new DatabaseInitializer(connections),
            profiles, new ApplicationSettingsService(connections));
    }

    private sealed class NoDialog : IFileDialogService { public string? ChooseSessionFile() => null; }
    private sealed class CountingAnalyzer : ISessionAnalyzer
    {
        public int Calls { get; private set; }
        public SessionAnalysisResult Analyze(TrainingSession session, ExerciseProfile profile)
        { Calls++; return new SessionAnalyzer().AnalyzeRequestedVersion(session, profile); }
    }
    private sealed class ForbiddenAnalyzer : ISessionAnalyzer
    {
        public SessionAnalysisResult Analyze(TrainingSession session, ExerciseProfile profile) =>
            throw new InvalidOperationException("Reopening history must not run analysis.");
    }
}
