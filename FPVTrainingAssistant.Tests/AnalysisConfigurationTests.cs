using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Tests.AnalysisTestData;

namespace FPVTrainingAssistant.Tests;

public sealed class AnalysisConfigurationTests
{
    [Fact]
    public void EmptyCriteriaAreRejected() => Reject([]);

    [Fact]
    public void AllDisabledCriteriaAreRejected() => Reject([Criterion(RuleTypes.CheckpointAccuracy, enabled: false)]);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonpositiveOrNonfiniteEnabledWeightsAreRejected(double weight) =>
        Reject([Criterion(RuleTypes.CheckpointAccuracy, weight)]);

    [Fact]
    public void ZeroWeightIsRejectedEvenWithAnotherPositiveCriterion() =>
        Reject([Criterion(RuleTypes.CheckpointAccuracy, 1), Criterion(RuleTypes.CollisionAvoidance, 0)]);

    [Fact]
    public void UnsupportedEnabledRuleIsRejected() => Reject([Criterion("FutureRule")]);

    [Fact]
    public void DisabledRuleDoesNotParticipate()
    {
        var profile = Profile(ExerciseTypes.ObstacleCourse,
            [Criterion(RuleTypes.CollisionAvoidance), Criterion("FutureRule", 0, enabled: false)]);
        var result = new SessionAnalyzer().Analyze(Session(), profile);
        Assert.Single(result.CriterionResults);
        Assert.Equal(100, result.FinalScore);
    }

    [Fact]
    public void ProfileIdMismatchIsRejected() =>
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(), Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CheckpointAccuracy)], id: "different")));

    [Fact]
    public void ProfileVersionMismatchIsRejected() =>
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(), Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.CheckpointAccuracy)], version: 2)));

    [Fact]
    public void ExerciseTypeMismatchIsRejected() =>
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(), Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.TargetIdentification)])));

    [Fact]
    public void UnknownExerciseIsImportableButNotAnalyzable() =>
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session("FutureExercise"), Profile("FutureExercise", [Criterion(RuleTypes.MissionSuccess)])));

    [Fact]
    public void IncompatibleKnownCriterionIsRejected() => Reject([Criterion(RuleTypes.TargetIdentification)]);

    [Fact]
    public void RouteCriterionRequiresUsableRoute() => Reject([Criterion(RuleTypes.RouteAdherence)]);

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(3, 2)]
    [InlineData(1, double.NaN)]
    public void ModelRejectsInvalidRouteRadiiBeforeAnalysis(double green, double yellow) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Point(0, 0, green: green, yellow: yellow));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void InvalidYellowContributionIsRejected(double value)
    {
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.RouteAdherence, parameters: new() { ["YellowZoneFactor"] = value })], [Point(0, 0), Point(1, 10)]);
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(), profile));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(20, 20)]
    [InlineData(20, 10)]
    [InlineData(double.NaN, 20)]
    public void InvalidCompletionParametersAreRejected(double target, double maximum) =>
        Reject([Criterion(RuleTypes.CompletionTime, parameters: new() { ["TargetTimeSec"] = target, ["MaximumTimeSec"] = maximum })]);

    [Fact]
    public void MissingCompletionParametersAreRejected() => Reject([Criterion(RuleTypes.CompletionTime)]);

    [Fact]
    public void NegativeCollisionPenaltyIsRejected() =>
        Reject([Criterion(RuleTypes.CollisionAvoidance, parameters: new() { ["PenaltyPerCollision"] = -1 })]);

    [Fact]
    public void DuplicateCriterionIdsAreRejected() =>
        Reject([Criterion(RuleTypes.CheckpointAccuracy, id: "same"), Criterion(RuleTypes.CollisionAvoidance, id: "same")]);

    [Fact]
    public void DamageScoresMustBeWithinRange()
    {
        var profile = Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.TargetDamage, parameters: new() { ["DamageScore_Critical"] = 101 })]);
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(ExerciseTypes.TargetMission), profile));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    public void InvalidMistakeThresholdIsRejected(double threshold) =>
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer(threshold));

    [Fact]
    public void DuplicateRouteSequenceNumbersAreRejected()
    {
        var profile = Profile(ExerciseTypes.ObstacleCourse, [Criterion(RuleTypes.RouteAdherence)], [Point(0, 0), Point(0, 10)]);
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(), profile));
    }

    [Fact]
    public void GenericDefaultObstacleTemplateNeedsCallerSuppliedCourse()
    {
        var profile = DefaultExerciseProfiles.DefaultObstacleCourse();
        Assert.Empty(profile.ReferencePoints);
        Assert.Equal(new[] { 40d, 30d, 20d, 10d }, profile.Criteria.Select(c => c.Weight));
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(profileId: profile.ProfileId), profile));
    }

    [Fact]
    public void LargeWeightsNormalizeWithoutOverflow()
    {
        var profile = Profile(ExerciseTypes.TargetMission, [Criterion(RuleTypes.MissionSuccess, double.MaxValue), Criterion(RuleTypes.TargetHit, double.MaxValue)]);
        Assert.Equal(50, new SessionAnalyzer().Analyze(Session(ExerciseTypes.TargetMission), profile).FinalScore);
    }

    private static void Reject(IEnumerable<EvaluationCriterion> criteria) =>
        Assert.Throws<AnalysisConfigurationException>(() => new SessionAnalyzer().Analyze(Session(), Profile(ExerciseTypes.ObstacleCourse, criteria)));
}
