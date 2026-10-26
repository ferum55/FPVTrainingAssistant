using System.Text.Json;

namespace FPVTrainingAssistant.Services;

internal static partial class SessionJsonStructureValidator
{
    private static void ValidateReplayStructure(Dictionary<string, JsonElement> fields, List<ValidationIssue> issues)
    {
        Optional(fields, "replay", JsonValueKind.Object, "$", issues);
        if (!fields.TryGetValue("replay", out var replay) || replay.ValueKind != JsonValueKind.Object) return;
        var context = ObjectFields(replay, "$.replay", issues)!;
        Required(context, "environmentId", JsonValueKind.String, "$.replay", issues);
        Optional(context, "scenarioId", JsonValueKind.String, "$.replay", issues);
        if (Required(context, "entities", JsonValueKind.Array, "$.replay", issues) is not { } entities) return;
        var index = 0;
        foreach (var track in entities.EnumerateArray())
        {
            var path = $"$.replay.entities[{index++}]";
            var values = ObjectFields(track, path, issues);
            if (values is null) continue;
            Required(values, "entityId", JsonValueKind.String, path, issues);
            Required(values, "entityType", JsonValueKind.String, path, issues);
            Optional(values, "archetypeId", JsonValueKind.String, path, issues);
            ReplayMetadata(values, path, issues);
            if (Required(values, "samples", JsonValueKind.Array, path, issues) is not { } samples) continue;
            var sampleIndex = 0;
            foreach (var sample in samples.EnumerateArray())
            {
                var samplePath = $"{path}.samples[{sampleIndex++}]";
                var transform = ObjectFields(sample, samplePath, issues);
                if (transform is null) continue;
                foreach (var name in new[] { "timeSec", "x", "y", "z", "pitchDeg", "rollDeg", "yawDeg" })
                    Required(transform, name, JsonValueKind.Number, samplePath, issues);
                Optional(transform, "isActive", JsonValueKind.True, samplePath, issues, allowBoolean: true);
                Optional(transform, "state", JsonValueKind.String, samplePath, issues);
                ReplayMetadata(transform, samplePath, issues);
            }
        }
    }

    private static void ReplayMetadata(Dictionary<string, JsonElement> fields, string path, List<ValidationIssue> issues)
    {
        Optional(fields, "metadata", JsonValueKind.Object, path, issues);
        if (!fields.TryGetValue("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object) return;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in metadata.EnumerateObject())
        {
            if (!keys.Add(entry.Name)) issues.Add(new("DuplicateProperty", path + ".metadata", "Metadata keys must be unique."));
            if (entry.Value.ValueKind != JsonValueKind.String)
                issues.Add(new("InvalidType", path + ".metadata", "Metadata values must be strings."));
        }
    }
}
