using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed partial class SessionValidator
{
    public const double TimeToleranceSeconds = 0.000001;

    public ValidationResult Validate(TrainingSession? session)
    {
        var issues = new List<ValidationIssue>();
        if (session is null)
        {
            issues.Add(new("SessionRequired", "$", "A training session is required."));
            return new ValidationResult(issues);
        }

        if (string.IsNullOrWhiteSpace(session.ContractVersion))
        {
            issues.Add(new("RequiredValue", "$.contractVersion", "Contract version is required."));
        }
        else if (!SessionJson.IsSupportedVersion(session.ContractVersion))
        {
            issues.Add(new("UnsupportedContractVersion", "$.contractVersion", "Supported contract versions are 1.0 and 1.1."));
        }

        RequiredText(session.SessionId, "$.sessionId", issues);
        RequiredText(session.OperatorId, "$.operatorId", issues);
        RequiredText(session.ProfileId, "$.profileId", issues);
        RequiredText(session.ExerciseType, "$.exerciseType", issues);
        RequiredText(session.SourceSimulator, "$.sourceSimulator", issues);
        if (session.ProfileVersion <= 0) issues.Add(new("InvalidProfileVersion", "$.profileVersion", "Profile version must be positive."));
        if (session.StartedAtUtc.Offset != TimeSpan.Zero) issues.Add(new("UtcRequired", "$.startedAtUtc", "Start time must be UTC."));
        if (session.EndedAtUtc.Offset != TimeSpan.Zero) issues.Add(new("UtcRequired", "$.endedAtUtc", "End time must be UTC."));

        double? duration = null;
        if (session.EndedAtUtc < session.StartedAtUtc)
        {
            issues.Add(new("InvalidTimeRange", "$.endedAtUtc", "End time must not precede start time."));
        }
        else
        {
            duration = session.Duration.TotalSeconds;
        }

        if (session.SimulatorScore is { } simulatorScore) Finite(simulatorScore, "$.simulatorScore", issues);
        if (session.AssistantFinalScore is { } assistantScore)
        {
            CheckPercentage(assistantScore, "$.assistantFinalScore", issues);
            issues.Add(new("ImportedAssistantScore", "$.assistantFinalScore", "An imported assistant score is retained as supplied, not verified or calculated by this importer.", ValidationSeverity.Warning));
        }

        ValidateTelemetry(session.Telemetry, duration, issues);
        ValidateEvents(session.Events, duration, issues);
        ValidateReplay(session.Replay, duration, issues);
        ValidateExercise(session, issues);
        return new ValidationResult(issues);
    }

    private static void ValidateTelemetry(List<TelemetrySample>? telemetry, double? duration, List<ValidationIssue> issues)
    {
        if (telemetry is null)
        {
            issues.Add(new("CollectionRequired", "$.telemetry", "Telemetry must be a nonnull array."));
            return;
        }

        double? previousTime = null;
        for (var i = 0; i < telemetry.Count; i++)
        {
            var sample = telemetry[i];
            var path = $"$.telemetry[{i}]";
            if (sample is null)
            {
                issues.Add(new("SampleRequired", path, "Telemetry sample must be an object."));
                continue;
            }

            CheckTime(sample.TimeSec, duration, path + ".timeSec", issues);
            if (double.IsFinite(sample.TimeSec))
            {
                if (previousTime is { } previous && sample.TimeSec <= previous)
                {
                    issues.Add(new("TelemetryTimeOrder", path + ".timeSec", "Telemetry times must be strictly increasing; duplicates are invalid."));
                }
                previousTime = sample.TimeSec;
            }

            Finite(sample.X, path + ".x", issues);
            Finite(sample.Y, path + ".y", issues);
            Finite(sample.Z, path + ".z", issues);
            if (Finite(sample.SpeedMps, path + ".speedMps", issues) && sample.SpeedMps < 0)
            {
                issues.Add(new("NegativeSpeed", path + ".speedMps", "Speed must be nonnegative."));
            }
            Finite(sample.AltitudeM, path + ".altitudeM", issues);
            Finite(sample.PitchDeg, path + ".pitchDeg", issues);
            Finite(sample.RollDeg, path + ".rollDeg", issues);
            Finite(sample.YawDeg, path + ".yawDeg", issues);
            if (sample.BatteryPercent is { } battery) CheckPercentage(battery, path + ".batteryPercent", issues);
            if (sample.ControlLinkQualityPercent is { } control) CheckPercentage(control, path + ".controlLinkQualityPercent", issues);
            if (sample.VideoLinkQualityPercent is { } video) CheckPercentage(video, path + ".videoLinkQualityPercent", issues);
            ValidateExtendedTelemetry(sample, path, issues);
        }
    }

    private static void ValidateEvents(List<SessionEvent>? events, double? duration, List<ValidationIssue> issues)
    {
        if (events is null)
        {
            issues.Add(new("CollectionRequired", "$.events", "Events must be a nonnull array."));
            return;
        }

        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < events.Count; i++)
        {
            var sessionEvent = events[i];
            var path = $"$.events[{i}]";
            if (sessionEvent is null)
            {
                issues.Add(new("EventRequired", path, "Session event must be an object."));
                continue;
            }
            RequiredText(sessionEvent.EventId, path + ".eventId", issues);
            RequiredText(sessionEvent.EventType, path + ".eventType", issues);
            CheckTime(sessionEvent.TimeSec, duration, path + ".timeSec", issues);
            if (!string.IsNullOrWhiteSpace(sessionEvent.EventId) && !eventIds.Add(sessionEvent.EventId))
            {
                issues.Add(new("DuplicateEventId", path + ".eventId", "Event IDs must be unique within a session."));
            }

            switch (sessionEvent.EventType)
            {
                case SessionEventTypes.CheckpointPassed:
                case SessionEventTypes.CheckpointMissed:
                case SessionEventTypes.TargetDetected:
                case SessionEventTypes.TargetHit:
                case SessionEventTypes.TargetDamageZone:
                    RequiredText(sessionEvent.EntityId, path + ".entityId", issues);
                    break;
            }
            if (sessionEvent.EventType == SessionEventTypes.TargetDamageZone &&
                (sessionEvent.Metadata is null || !sessionEvent.Metadata.TryGetValue("damageZone", out var zone) || string.IsNullOrWhiteSpace(zone)))
            {
                issues.Add(new("DamageZoneRequired", path + ".metadata.damageZone", "TargetDamageZone requires nonblank metadata.damageZone."));
            }
            if (sessionEvent.Metadata is { } metadata && metadata.Values.Any(value => value is null))
            {
                issues.Add(new("InvalidMetadata", path + ".metadata", "Metadata values must be strings, not null."));
            }
        }
    }

    private static void ValidateExercise(TrainingSession session, List<ValidationIssue> issues)
    {
        var events = session.Events;
        switch (session.ExerciseType)
        {
            case ExerciseTypes.ObstacleCourse:
                if (session.Telemetry is null || session.Telemetry.Count < 2)
                {
                    issues.Add(new("InsufficientTelemetry", "$.telemetry", "ObstacleCourse requires at least two telemetry samples."));
                }
                if (events is not null && !events.Any(e => e?.EventType is SessionEventTypes.CheckpointPassed or SessionEventTypes.CheckpointMissed))
                {
                    issues.Add(new("NoCheckpointEvents", "$.events", "ObstacleCourse contains no checkpoint passed/missed events.", ValidationSeverity.Warning));
                }
                break;
            case ExerciseTypes.TargetMission:
                if (events is not null && !events.Any(e => e?.EventType is SessionEventTypes.TargetDetected or SessionEventTypes.FalseTargetDetection or SessionEventTypes.TargetHit or SessionEventTypes.TargetDamageZone or SessionEventTypes.MissionCompleted))
                {
                    issues.Add(new("NoTargetEvents", "$.events", "TargetMission contains no target-related events; it may have failed or been aborted.", ValidationSeverity.Warning));
                }
                break;
            default:
                if (!string.IsNullOrWhiteSpace(session.ExerciseType))
                {
                    issues.Add(new("UnknownExerciseType", "$.exerciseType", "This exercise type has no prototype-specific validation rules.", ValidationSeverity.Warning));
                }
                break;
        }
    }

    private static void CheckTime(double time, double? duration, string path, List<ValidationIssue> issues)
    {
        if (!Finite(time, path, issues)) return;
        if (time < 0) issues.Add(new("NegativeTime", path, "Elapsed time must be nonnegative."));
        if (duration is { } seconds && time - seconds > TimeToleranceSeconds)
        {
            issues.Add(new("TimeOutsideSession", path, "Elapsed time exceeds the session duration."));
        }
    }

    private static void RequiredText(string? value, string path, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)) issues.Add(new("RequiredValue", path, "Value must not be empty or whitespace."));
    }

    private static bool Finite(double value, string path, List<ValidationIssue> issues)
    {
        if (double.IsFinite(value)) return true;
        issues.Add(new("NonFiniteNumber", path, "Value must be finite."));
        return false;
    }

    internal static void CheckPercentage(double value, string path, List<ValidationIssue> issues)
    {
        if (Finite(value, path, issues) && value is < 0 or > 100)
        {
            issues.Add(new("PercentageOutOfRange", path, "Value must be between 0 and 100."));
        }
    }
}
