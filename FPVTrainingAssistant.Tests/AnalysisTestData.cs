using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

// All data in this builder is synthetic, independent of any simulator/course export.
internal static class AnalysisTestData
{
    public static TelemetrySample Sample(double time, double x = 0, double y = 0, double z = 0) => new()
    {
        TimeSec = time, X = x, Y = y, Z = z, SpeedMps = 0, AltitudeM = z, PitchDeg = 0, RollDeg = 0, YawDeg = 0
    };

    public static ReferencePoint Point(int sequence, double x, double y = 0, double green = 1, double yellow = 3) =>
        new(green, yellow) { SequenceNo = sequence, X = x, Y = y, Z = 0, PointType = "SyntheticRoute" };

    public static EvaluationCriterion Criterion(string rule, double weight = 1, Dictionary<string, double>? parameters = null,
        bool enabled = true, string? id = null) => new()
    {
        CriterionId = id ?? rule, RuleType = rule, Weight = weight, Parameters = parameters, IsEnabled = enabled,
        Name = rule, Description = "Synthetic assessment criterion."
    };

    public static ExerciseProfile Profile(string type, IEnumerable<EvaluationCriterion> criteria,
        IEnumerable<ReferencePoint>? points = null, string id = "synthetic-analysis-profile", int version = 1) => new()
    {
        ProfileId = id, Version = version, ExerciseType = type, Name = "Synthetic analysis profile",
        Criteria = criteria.ToList(), ReferencePoints = points?.ToList() ?? []
    };

    public static SessionEvent Event(string type, double time = 1, string? entityId = "synthetic-entity", string? zone = null, string? id = null) => new()
    {
        EventId = id ?? type, EventType = type, TimeSec = time, EntityId = entityId,
        Metadata = zone is null ? null : new() { ["damageZone"] = zone }
    };

    public static List<SessionEvent> Events(params string[] types) => types.Select((type, i) => Event(type, i + 1, id: "synthetic-event-" + i)).ToList();

    public static TrainingSession Session(string type = ExerciseTypes.ObstacleCourse, IEnumerable<SessionEvent>? events = null,
        IEnumerable<TelemetrySample>? telemetry = null, double duration = 60, bool? success = true,
        string profileId = "synthetic-analysis-profile", int version = 1, double? assistantScore = null)
    {
        var start = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);
        var session = new TrainingSession
        {
            SessionId = "synthetic-analysis-session", OperatorId = "synthetic-operator", ProfileId = profileId, ProfileVersion = version,
            ExerciseType = type, StartedAtUtc = start, EndedAtUtc = start.AddSeconds(duration), SourceSimulator = "SyntheticAnalysisTest",
            Success = success, SimulatorScore = 750, AssistantFinalScore = assistantScore,
            Telemetry = telemetry?.ToList() ?? [Sample(0), Sample(duration, 10)], Events = events?.ToList() ?? []
        };
        var validation = new SessionValidator().Validate(session);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors.Select(e => e.Path + ": " + e.Message)));
        return session;
    }

    public static double Score(SessionAnalysisResult result, string criterionId) =>
        Assert.Single(result.CriterionResults, r => r.CriterionId == criterionId).Score;
}
