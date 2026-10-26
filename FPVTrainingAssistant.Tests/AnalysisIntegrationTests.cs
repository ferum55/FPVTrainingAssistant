using System.Globalization;
using System.Text.Json;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class AnalysisIntegrationTests
{
    [Fact]
    public void RepeatedAnalysisIsIdenticalAcrossCulturesAndDoesNotMutateInput()
    {
        var session = Session(events: Events(SessionEventTypes.CheckpointPassed, SessionEventTypes.CheckpointMissed, SessionEventTypes.Collision));
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CheckpointAccuracy, 40), Criterion(RuleTypes.CollisionAvoidance, 20)]);
        var before = JsonSerializer.Serialize(session, SessionJson.Options);
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
            var first = new SessionAnalyzer().Analyze(session, profile);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var second = new SessionAnalyzer().Analyze(session, profile);
            Assert.Equal(JsonSerializer.Serialize(first, SessionJson.Options), JsonSerializer.Serialize(second, SessionJson.Options));
            Assert.Equal(before, JsonSerializer.Serialize(session, SessionJson.Options));
            Assert.Equal(60, first.FinalScore); // Weights 40/60 and 20/60, scores 50 and 80.
            Assert.All(first.CriterionResults, r => Assert.Equal(session.SessionId + ":" + r.CriterionId, r.ResultId));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void OnlyFinalScoreIsRounded()
    {
        var session = Session(events: Events(SessionEventTypes.CheckpointPassed, SessionEventTypes.CheckpointMissed, SessionEventTypes.CheckpointMissed));
        var result = new SessionAnalyzer().Analyze(session, Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CheckpointAccuracy)]));
        Assert.Equal(33.33, result.FinalScore);
        Assert.Equal(100d / 3, Assert.Single(result.CriterionResults).Score, 10);
    }

    [Theory]
    [InlineData("valid_obstacle_course.json")]
    [InlineData("valid_target_mission.json")]
    [InlineData("valid_unknown_event.json")]
    public async Task ExistingSyntheticFixturesCanBeImportedThenAnalyzed(string fileName)
    {
        var imported = await new SessionImportService().ImportAsync(Path.Combine(AppContext.BaseDirectory, "TestSessions", fileName));
        Assert.True(imported.IsSuccess);
        var session = imported.Session!;
        var template = session.ExerciseType == ExerciseTypes.ObstacleCourse
            ? DefaultExerciseProfiles.DefaultObstacleCourse([Point(0, 0), Point(1, 20)])
            : DefaultExerciseProfiles.DefaultTargetMission();
        var profile = Profile(session.ExerciseType, template.Criteria, template.ReferencePoints, session.ProfileId);
        var result = new SessionAnalyzer().Analyze(session, profile);
        Assert.True(result.IsSuccessful);
        Assert.InRange(result.FinalScore, 0, 100);
        Assert.All(result.CriterionResults, r => Assert.False(string.IsNullOrWhiteSpace(r.Explanation)));
        Assert.Equal(750d, session.SimulatorScore);
    }

    [Fact]
    public void DefaultFactoriesReturnIndependentMutableConfigurations()
    {
        var first = DefaultExerciseProfiles.DefaultTargetMission();
        first.Criteria.Clear();
        Assert.Equal(4, DefaultExerciseProfiles.DefaultTargetMission().Criteria.Count);
    }
}
