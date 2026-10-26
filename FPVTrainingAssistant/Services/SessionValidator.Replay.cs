using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed partial class SessionValidator
{
    internal static bool IsValidEntityTrack(ReplayEntityTrack track, double? duration)
    {
        var issues = new List<ValidationIssue>();
        ValidateReplay(new ReplayContext { EnvironmentId = "capability-check", Entities = [track] }, duration, issues);
        return issues.Count == 0;
    }

    private static void ValidateExtendedTelemetry(TelemetrySample sample, string path, List<ValidationIssue> issues)
    {
        if (sample.VerticalSpeedMps is { } vertical) Finite(vertical, path + ".verticalSpeedMps", issues);
        if (sample.CameraTiltDeg is { } tilt) Finite(tilt, path + ".cameraTiltDeg", issues);
        if (sample.ControlRssiDbm is { } rssi) Finite(rssi, path + ".controlRssiDbm", issues);
        if (sample.CurrentA is { } current) Finite(current, path + ".currentA", issues);
        Nonnegative(sample.BatteryVoltageV, path + ".batteryVoltageV", issues);
        Nonnegative(sample.CellVoltageV, path + ".cellVoltageV", issues);
        Nonnegative(sample.ConsumedMah, path + ".consumedMah", issues);
        if (sample.CameraFovDeg is { } fov && Finite(fov, path + ".cameraFovDeg", issues) && (fov <= 0 || fov >= 180))
        {
            issues.Add(new("InvalidCameraFov", path + ".cameraFovDeg", "Horizontal camera FOV must be greater than 0 and less than 180 degrees."));
        }
    }

    private static void Nonnegative(double? value, string path, List<ValidationIssue> issues)
    {
        if (value is { } number && Finite(number, path, issues) && number < 0)
            issues.Add(new("NegativeValue", path, "Value must be nonnegative."));
    }

    private static void ValidateReplay(ReplayContext? replay, double? duration, List<ValidationIssue> issues)
    {
        if (replay is null) return;
        NeutralId(replay.EnvironmentId, "$.replay.environmentId", issues);
        if (replay.ScenarioId is not null) NeutralId(replay.ScenarioId, "$.replay.scenarioId", issues);
        if (replay.Entities is null)
        {
            issues.Add(new("CollectionRequired", "$.replay.entities", "Replay entities must be a nonnull array."));
            return;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < replay.Entities.Count; i++)
        {
            var path = $"$.replay.entities[{i}]";
            var track = replay.Entities[i];
            if (track is null)
            {
                issues.Add(new("TrackRequired", path, "Replay entity track must be an object."));
                continue;
            }
            RequiredText(track.EntityId, path + ".entityId", issues);
            RequiredText(track.EntityType, path + ".entityType", issues);
            if (!string.IsNullOrWhiteSpace(track.EntityId) && !ids.Add(track.EntityId))
                issues.Add(new("DuplicateEntityId", path + ".entityId", "Entity IDs must be unique within replay context."));
            if (track.ArchetypeId is not null) NeutralId(track.ArchetypeId, path + ".archetypeId", issues);
            ValidateMetadata(track.Metadata, path + ".metadata", issues);
            ValidateEntitySamples(track.Samples, duration, path + ".samples", issues);
        }
    }

    private static void ValidateEntitySamples(List<ReplayEntitySample>? samples, double? duration, string path, List<ValidationIssue> issues)
    {
        if (samples is null || samples.Count == 0)
        {
            issues.Add(new("EntitySamplesRequired", path, "An entity track requires at least one sample."));
            return;
        }
        double? previousTime = null;
        for (var i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            var samplePath = $"{path}[{i}]";
            if (sample is null)
            {
                issues.Add(new("SampleRequired", samplePath, "Replay entity sample must be an object."));
                continue;
            }
            CheckTime(sample.TimeSec, duration, samplePath + ".timeSec", issues);
            if (double.IsFinite(sample.TimeSec))
            {
                if (previousTime is { } previous && sample.TimeSec <= previous)
                    issues.Add(new("EntityTimeOrder", samplePath + ".timeSec", "Entity sample times must be strictly increasing."));
                previousTime = sample.TimeSec;
            }
            Finite(sample.X, samplePath + ".x", issues);
            Finite(sample.Y, samplePath + ".y", issues);
            Finite(sample.Z, samplePath + ".z", issues);
            Finite(sample.PitchDeg, samplePath + ".pitchDeg", issues);
            Finite(sample.RollDeg, samplePath + ".rollDeg", issues);
            Finite(sample.YawDeg, samplePath + ".yawDeg", issues);
            ValidateMetadata(sample.Metadata, samplePath + ".metadata", issues);
        }
    }

    private static void ValidateMetadata(Dictionary<string, string>? metadata, string path, List<ValidationIssue> issues)
    {
        if (metadata is not null && metadata.Values.Any(value => value is null))
            issues.Add(new("InvalidMetadata", path, "Metadata values must be strings, not null."));
    }

    // IDs are open logical names, never filesystem, engine package or object paths.
    internal static bool IsNeutralId(string? value) => !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(['/', '\\', ':', '\'', '"']) < 0;

    private static void NeutralId(string? value, string path, List<ValidationIssue> issues)
    {
        RequiredText(value, path, issues);
        if (!string.IsNullOrWhiteSpace(value) && !IsNeutralId(value))
            issues.Add(new("NeutralIdRequired", path, "Use a neutral logical ID without path separators, colons or quotes; engine asset paths are not supported."));
    }
}
