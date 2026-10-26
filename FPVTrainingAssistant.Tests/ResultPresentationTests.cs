using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class ResultPresentationTests
{
    [Fact]
    public void RecommendationsUsePriorityThenStableOriginalOrder()
    {
        var session = Session();
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CollisionAvoidance)]);
        var result = Result(session, profile,
        [
            Recommendation("first-medium", 2), Recommendation("first-high", 1),
            Recommendation("second-high", 1), Recommendation("low", 3)
        ]);
        var vm = new SessionResultViewModel(session, profile, result, []);
        Assert.Equal(new[] { "first-high", "second-high", "first-medium", "low" }, vm.Recommendations.Select(r => r.Text));
        Assert.Equal(new[] { "High", "High", "Medium", "Low" }, vm.Recommendations.Select(r => r.PriorityLabel));
    }

    [Fact]
    public void EventsAreChronologicalAndEqualTimesRemainStable()
    {
        var session = Session(events: [Event("Later", 8), Event("First", 2), Event("Second", 2)]);
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CollisionAvoidance)]);
        var vm = new SessionResultViewModel(session, profile, Result(session, profile), []);
        Assert.Equal(new[] { "First", "Second", "Later" }, vm.Events.Select(e => e.EventType));
        Assert.Equal("2.00 s", vm.Events[0].TimeText);
    }

    [Fact]
    public void MetadataAndValueAreReadablePairs()
    {
        var vm = new EventViewModel(new SessionEvent
        {
            EventId = "synthetic", EventType = "FutureEvent", TimeSec = 2, Value = "confirmed",
            Metadata = new() { ["result"] = "synthetic", ["damageZone"] = "central" }
        });
        Assert.Contains("Value: confirmed", vm.Details);
        Assert.Contains("damageZone: central", vm.Details);
        Assert.Contains("result: synthetic", vm.Details);
        Assert.DoesNotContain("Dictionary", vm.Details);
    }

    [Fact]
    public void CriterionNamesAreMappedAgainstProfile()
    {
        var session = Session();
        var profile = Profile(ExerciseTypes.ObstacleCourse,
            [new EvaluationCriterion { CriterionId = "clearance", Name = "Obstacle clearance", Description = "Synthetic", Weight = 1, RuleType = RuleTypes.CollisionAvoidance }]);
        var vm = new SessionResultViewModel(session, profile, Result(session, profile), []);
        Assert.Equal("Obstacle clearance", Assert.Single(vm.Criteria).Name);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(vm.Criteria).Explanation));
    }

    [Fact]
    public void NullSimulatorScoreAndEmptySectionsHaveExplicitPresentationState()
    {
        var start = DateTimeOffset.Parse("2026-10-26T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var session = new TrainingSession
        {
            SessionId = "s", OperatorId = "o", ProfileId = "p", ProfileVersion = 1, ExerciseType = ExerciseTypes.TargetMission,
            StartedAtUtc = start, EndedAtUtc = start.AddSeconds(60), SourceSimulator = "Synthetic", Success = true,
            Events = Events(SessionEventTypes.TargetDetected, SessionEventTypes.TargetHit)
        };
        var profile = Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.MissionSuccess)], id: "p");
        var vm = new SessionResultViewModel(session, profile, Result(session, profile), []);
        Assert.Equal("Not provided", vm.SimulatorScoreText);
        Assert.True(vm.HasNoMistakes);
        Assert.True(vm.HasNoRecommendations);
        Assert.False(vm.HasWarnings);
    }

    [Theory]
    [InlineData("synthetic-obstacle-course", 2, ExerciseTypes.ObstacleCourse)]
    [InlineData("unavailable", 1, ExerciseTypes.ObstacleCourse)]
    [InlineData("synthetic-target-mission", 1, ExerciseTypes.ObstacleCourse)]
    public void ProviderRequiresExactProfileVersionAndExercise(string id, int version, string type) =>
        Assert.Null(new ExerciseProfileProvider().Resolve(Session(type, profileId: id, version: version)));

    private static SessionAnalysisResult Result(TrainingSession session, ExerciseProfile profile, IReadOnlyList<Recommendation>? recommendations = null)
    {
        var analyzed = new SessionAnalyzer().Analyze(session, profile);
        return new SessionAnalysisResult
        {
            SessionId = analyzed.SessionId, ProfileId = analyzed.ProfileId, ProfileVersion = analyzed.ProfileVersion,
            FinalScore = analyzed.FinalScore, Metrics = analyzed.Metrics, CriterionResults = analyzed.CriterionResults,
            Mistakes = analyzed.Mistakes, Recommendations = recommendations ?? analyzed.Recommendations, IsSuccessful = true
        };
    }

    private static Recommendation Recommendation(string text, int priority) => new()
    {
        RecommendationId = text, SessionId = "synthetic", Text = text, Category = "Synthetic", Priority = priority, Evidence = "Synthetic evidence"
    };
}
