using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class ObstacleCourseAnalysisTests
{
    [Theory]
    [InlineData(10, 0, 100)]
    [InlineData(8, 2, 80)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, 0)]
    public void CheckpointScoresUseRecordedOutcomes(int passed, int missed, double expected)
    {
        var events = Events(Enumerable.Repeat(SessionEventTypes.CheckpointPassed, passed).Concat(Enumerable.Repeat(SessionEventTypes.CheckpointMissed, missed)).ToArray());
        var result = Analyze(Session(events: events), Criterion(RuleTypes.CheckpointAccuracy));
        Assert.Equal(expected, result.FinalScore);
        var criterion = Assert.Single(result.CriterionResults);
        Assert.Equal(expected, criterion.MeasuredValue);
        Assert.Contains($"{missed} missed", criterion.Explanation);
        Assert.Equal(passed + missed, result.Metrics.TotalCheckpointCount);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 80)]
    [InlineData(2, 60)]
    [InlineData(5, 0)]
    [InlineData(8, 0)]
    public void CollisionPenaltyAndClamping(int collisions, double expected)
    {
        var result = Analyze(Session(events: Events(Enumerable.Repeat(SessionEventTypes.Collision, collisions).ToArray())), Criterion(RuleTypes.CollisionAvoidance));
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(collisions, Assert.Single(result.CriterionResults).MeasuredValue);
        Assert.Contains("20.00 points per collision", Assert.Single(result.CriterionResults).Explanation);
    }

    [Fact]
    public void CollisionPenaltyCanBeConfigured()
    {
        var result = Analyze(Session(events: Events(SessionEventTypes.Collision)), Criterion(RuleTypes.CollisionAvoidance, parameters: new() { ["PenaltyPerCollision"] = 7 }));
        Assert.Equal(93, result.FinalScore);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(60, 100)]
    [InlineData(120, 50)]
    [InlineData(180, 0)]
    [InlineData(240, 0)]
    public void CompletionTimeIsBoundedAndLinear(double duration, double expected)
    {
        var result = Analyze(Session(duration: duration), Criterion(RuleTypes.CompletionTime, parameters: new() { ["TargetTimeSec"] = 60, ["MaximumTimeSec"] = 180 }));
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(duration, Assert.Single(result.CriterionResults).MeasuredValue);
    }

    [Theory]
    [InlineData(0.5, 25)]
    [InlineData(1, 40)]
    [InlineData(0, 10)]
    public void RouteScoreUsesTimeWeightedZonesAndYellowFactor(double factor, double expected)
    {
        var session = Session(telemetry: [Sample(0, 0, 0), Sample(1, 0, 0), Sample(4, 0, 4), Sample(10, 0, 6)]);
        var profile = Profile(ExerciseTypes.ObstacleCourse,
            [Criterion(RuleTypes.RouteAdherence, parameters: new() { ["YellowZoneFactor"] = factor })], [Point(0, 0), Point(1, 10)]);
        var result = new SessionAnalyzer().Analyze(session, profile);
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(10d, Assert.Single(result.CriterionResults).MeasuredValue);
        Assert.Contains("60.00% red", Assert.Single(result.CriterionResults).Explanation);
    }

    [Fact]
    public void DefaultWeightsPrioritizeCorrectnessAndKeepInputScoresUnchanged()
    {
        var profile = DefaultExerciseProfiles.DefaultObstacleCourse([Point(0, 0), Point(1, 10)]);
        var events = Events(Enumerable.Repeat(SessionEventTypes.CheckpointPassed, 8)
            .Concat(Enumerable.Repeat(SessionEventTypes.CheckpointMissed, 2)).Concat(Enumerable.Repeat(SessionEventTypes.Collision, 2)).ToArray());
        var session = Session(events: events, profileId: profile.ProfileId, assistantScore: 3);
        var result = new SessionAnalyzer().Analyze(session, profile);
        Assert.Equal(84, result.FinalScore); // .4*80 + .3*100 + .2*60 + .1*100
        Assert.Equal(750d, session.SimulatorScore);
        Assert.Equal(3d, session.AssistantFinalScore);
        Assert.True(result.IsSuccessful);
        Assert.Equal(profile.Version, result.ProfileVersion);
        Assert.All(result.CriterionResults, c => Assert.InRange(c.Score, 0, 100));
    }

    [Fact]
    public void MistakesRetainEvidenceAndRecommendationsAreDeduplicated()
    {
        var events = Events(SessionEventTypes.CheckpointMissed, SessionEventTypes.CheckpointMissed, SessionEventTypes.Collision, SessionEventTypes.Collision);
        var session = Session(events: events, telemetry: [Sample(0, 0, 5), Sample(10, 10, 5)]);
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CheckpointAccuracy), Criterion(RuleTypes.RouteAdherence)], [Point(0, 0), Point(1, 10)]);
        var result = new SessionAnalyzer().Analyze(session, profile);
        Assert.Equal(2, result.Mistakes.Count(m => m.Code == "MISSED_CHECKPOINT"));
        Assert.Equal(2, result.Mistakes.Count(m => m.Code == "COLLISION"));
        Assert.Contains(result.Mistakes, m => m.Code == "LOW_CHECKPOINT_ACCURACY");
        Assert.Contains(result.Mistakes, m => m.Code == "ROUTE_DEVIATION");
        Assert.Contains(result.Mistakes, m => m.TimeSec == 1 && m.RelatedEntityId == "synthetic-entity" && m.Evidence.Contains("synthetic-event-0", StringComparison.Ordinal));
        Assert.Equal(3, result.Recommendations.Count);
        Assert.Single(result.Recommendations, r => r.Category == "CheckpointAccuracy");
        Assert.Equal(result.Recommendations.Count, result.Recommendations.Select(r => r.RecommendationId).Distinct().Count());
        Assert.All(result.Recommendations, r => { Assert.False(string.IsNullOrWhiteSpace(r.Evidence)); Assert.InRange(r.Priority, 1, 3); });
    }

    [Fact]
    public void RedExposureThresholdIsConfigurableAndUsesStrictComparison()
    {
        var session = Session(telemetry: [Sample(0, 0, 0), Sample(9, 0, 0), Sample(10, 0, 10)]);
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.RouteAdherence)], [Point(0, 0), Point(1, 10)]);
        Assert.DoesNotContain(new SessionAnalyzer().Analyze(session, profile).Mistakes, m => m.Code == "ROUTE_DEVIATION");
        Assert.Contains(new SessionAnalyzer(9).Analyze(session, profile).Mistakes, m => m.Code == "ROUTE_DEVIATION");
    }

    [Fact]
    public void RouteMetricsAreAvailableWithoutRouteCriterionWhenRouteIsSupplied()
    {
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CollisionAvoidance)], [Point(0, 0), Point(1, 10)]);
        Assert.NotNull(new SessionAnalyzer().Analyze(Session(), profile).RouteAnalysis);
    }

    private static SessionAnalysisResult Analyze(TrainingSession session, params EvaluationCriterion[] criteria) =>
        new SessionAnalyzer().Analyze(session, Profile(ExerciseTypes.ObstacleCourse, criteria));
}
