using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class TargetMissionAnalysisTests
{
    [Theory]
    [InlineData(1, 0, 100)]
    [InlineData(1, 1, 50)]
    [InlineData(0, 2, 0)]
    [InlineData(0, 0, 0)]
    public void IdentificationUsesCorrectAndFalseAttempts(int correct, int falseAttempts, double expected)
    {
        var events = Events(Enumerable.Repeat(SessionEventTypes.TargetDetected, correct)
            .Concat(Enumerable.Repeat(SessionEventTypes.FalseTargetDetection, falseAttempts)).ToArray());
        var result = Analyze(events, RuleTypes.TargetIdentification);
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(correct + falseAttempts, result.Metrics.TotalDetectionAttempts);
        Assert.Contains($"{correct} correct detections", Assert.Single(result.CriterionResults).Explanation);
        Assert.Contains($"{falseAttempts} false detections", Assert.Single(result.CriterionResults).Explanation);
    }

    [Theory]
    [InlineData(true, 100)]
    [InlineData(false, 0)]
    public void HitIsConfirmedOnlyByHitEvent(bool hit, double expected)
    {
        var result = Analyze(Events(hit ? SessionEventTypes.TargetHit : SessionEventTypes.TargetDetected), RuleTypes.TargetHit);
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(hit ? 1 : 0, result.Metrics.TargetHitCount);
    }

    [Theory]
    [InlineData("critical", 100)]
    [InlineData("ENGINE", 90)]
    [InlineData("track", 70)]
    [InlineData("tracks", 70)]
    [InlineData("weapon", 80)]
    [InlineData("gun", 80)]
    [InlineData("barrel", 80)]
    [InlineData("hull", 60)]
    [InlineData("future-zone", 50)]
    public void DamageCategoriesAndAliasesUseTrainingScores(string zone, double expected)
    {
        var result = Analyze([Event(SessionEventTypes.TargetDamageZone, zone: zone)], RuleTypes.TargetDamage);
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(expected, Assert.Single(result.CriterionResults).MeasuredValue);
        Assert.Contains("'" + zone + "'", Assert.Single(result.CriterionResults).Explanation);
        Assert.Equal(0, result.Metrics.TargetHitCount); // Explicit damage evidence never fabricates a hit.
    }

    [Fact]
    public void NoDamageEventGivesZeroWithoutInferringFromHit()
    {
        var result = Analyze(Events(SessionEventTypes.TargetHit), RuleTypes.TargetDamage);
        Assert.Equal(0, result.FinalScore);
        Assert.Contains("No TargetDamageZone", Assert.Single(result.CriterionResults).Explanation);
    }

    [Fact]
    public void BestConfiguredDamageScoreWinsRatherThanAssumingCategoryOrder()
    {
        var session = Session(ExerciseTypes.TargetMission,
            [Event(SessionEventTypes.TargetDamageZone, zone: "critical", id: "critical"), Event(SessionEventTypes.TargetDamageZone, zone: "engine", id: "engine")]);
        var profile = Profile(ExerciseTypes.TargetMission,
            [Criterion(RuleTypes.TargetDamage, parameters: new() { ["DamageScore_Critical"] = 40, ["DamageScore_Engine"] = 95 })]);
        var result = new SessionAnalyzer().Analyze(session, profile);
        Assert.Equal(95, result.FinalScore);
        Assert.Contains("'engine'", Assert.Single(result.CriterionResults).Explanation);
        Assert.Equal(2, result.Metrics.DamageZones.Count);
    }

    [Fact]
    public void UnknownDamageScoreCanBeConfigured()
    {
        var session = Session(ExerciseTypes.TargetMission, [Event(SessionEventTypes.TargetDamageZone, zone: "future")]);
        var profile = Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.TargetDamage, parameters: new() { ["DamageScore_Default"] = 17 })]);
        Assert.Equal(17, new SessionAnalyzer().Analyze(session, profile).FinalScore);
    }

    [Theory]
    [InlineData(true, 100, "succeeded")]
    [InlineData(false, 0, "failed")]
    [InlineData(null, 0, "unknown")]
    public void MissionSuccessDistinguishesFailureFromUnknown(bool? success, double expected, string explanation)
    {
        var session = Session(ExerciseTypes.TargetMission, success: success);
        var result = new SessionAnalyzer().Analyze(session, Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.MissionSuccess)]));
        Assert.Equal(expected, result.FinalScore);
        Assert.Equal(expected == 100 ? 1d : 0d, Assert.Single(result.CriterionResults).MeasuredValue);
        Assert.Contains(explanation, Assert.Single(result.CriterionResults).Explanation);
        Assert.Equal(success == false, result.Mistakes.Any(m => m.Code == "MISSION_FAILED"));
        Assert.True(result.IsSuccessful); // Analysis success, independent of mission outcome.
    }

    [Fact]
    public void DefaultTargetWeightsAndRecommendationsFollowEvidence()
    {
        var profile = DefaultExerciseProfiles.DefaultTargetMission();
        var session = Session(ExerciseTypes.TargetMission,
            [Event(SessionEventTypes.TargetDetected), Event(SessionEventTypes.FalseTargetDetection),
                Event(SessionEventTypes.TargetDamageZone, zone: "engine")],
            success: false, profileId: profile.ProfileId);
        var result = new SessionAnalyzer().Analyze(session, profile);
        Assert.Equal(35.5, result.FinalScore); // .35*50 + .30*0 + .20*90 + .15*0
        Assert.Contains(result.Mistakes, m => m.Code == "FALSE_TARGET_DETECTION");
        Assert.Contains(result.Mistakes, m => m.Code == "TARGET_NOT_HIT");
        Assert.Contains(result.Mistakes, m => m.Code == "MISSION_FAILED");
        Assert.DoesNotContain(result.Mistakes, m => m.Code == "TARGET_NOT_IDENTIFIED");
        Assert.Equal(3, result.Recommendations.Count);
        Assert.All(result.Recommendations, r => Assert.False(string.IsNullOrWhiteSpace(r.Evidence)));
        Assert.Equal(750d, session.SimulatorScore);
    }

    [Fact]
    public void MissingDetectionAndHitDescribeMissingRecords()
    {
        var result = Analyze([], RuleTypes.TargetIdentification);
        Assert.Contains(result.Mistakes, m => m.Code == "TARGET_NOT_IDENTIFIED" && m.Description.Contains("recorded", StringComparison.Ordinal));
        Assert.Contains(result.Mistakes, m => m.Code == "TARGET_NOT_HIT");
        Assert.Contains(result.Recommendations, r => r.RecommendationId.EndsWith(":rec:target-search", StringComparison.Ordinal));
        Assert.Contains(result.Recommendations, r => r.RecommendationId.EndsWith(":rec:target-hit", StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatedFalseDetectionsYieldOneRecommendationWithBothEventIds()
    {
        var result = Analyze(Events(SessionEventTypes.FalseTargetDetection, SessionEventTypes.FalseTargetDetection), RuleTypes.TargetIdentification);
        var recommendation = Assert.Single(result.Recommendations, r => r.RecommendationId.EndsWith(":rec:false-target-detection", StringComparison.Ordinal));
        Assert.Contains("synthetic-event-0", recommendation.Evidence);
        Assert.Contains("synthetic-event-1", recommendation.Evidence);
    }

    private static SessionAnalysisResult Analyze(IEnumerable<SessionEvent> events, string rule) =>
        new SessionAnalyzer().Analyze(Session(ExerciseTypes.TargetMission, events), Profile(ExerciseTypes.TargetMission, [Criterion(rule)]));
}
