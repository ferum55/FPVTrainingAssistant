using System.Text.Json;

namespace FPVTrainingAssistant.Services;

// Checks presence/types before deserialization so model defaults cannot hide missing
// JSON fields and existing model guards cannot turn ordinary bad input into exceptions.
internal static class SessionJsonStructureValidator
{
    public static ValidationResult Validate(JsonElement root)
    {
        var issues = new List<ValidationIssue>();
        var fields = ObjectFields(root, "$", issues);
        if (fields is null)
        {
            return new ValidationResult(issues);
        }

        foreach (var name in new[] { "contractVersion", "sessionId", "operatorId", "profileId", "exerciseType", "sourceSimulator" })
        {
            Required(fields, name, JsonValueKind.String, "$", issues);
        }

        if (Required(fields, "profileVersion", JsonValueKind.Number, "$", issues) is { } version && !version.TryGetInt32(out _))
        {
            issues.Add(new("InvalidType", "$.profileVersion", "Profile version must be a 32-bit integer."));
        }

        foreach (var name in new[] { "startedAtUtc", "endedAtUtc" })
        {
            if (Required(fields, name, JsonValueKind.String, "$", issues) is not { } timestamp)
            {
                continue;
            }

            var text = timestamp.GetString()!;
            if (!timestamp.TryGetDateTimeOffset(out var parsed))
            {
                issues.Add(new("InvalidTimestamp", $"$.{name}", "Use an explicit UTC ISO 8601 timestamp."));
            }
            else if (parsed.Offset != TimeSpan.Zero || !(text.EndsWith('Z') || text.EndsWith("+00:00", StringComparison.Ordinal)))
            {
                issues.Add(new("UtcRequired", $"$.{name}", "Timestamp must explicitly use UTC (Z or +00:00)."));
            }
        }

        Optional(fields, "success", JsonValueKind.True, "$", issues, allowBoolean: true);
        Optional(fields, "simulatorScore", JsonValueKind.Number, "$", issues);
        Optional(fields, "assistantFinalScore", JsonValueKind.Number, "$", issues);
        if (fields.TryGetValue("assistantFinalScore", out var score) && score.ValueKind == JsonValueKind.Number && score.TryGetDouble(out var value))
        {
            // The existing TrainingSession setter also enforces this invariant.
            SessionValidator.CheckPercentage(value, "$.assistantFinalScore", issues);
        }

        if (Required(fields, "telemetry", JsonValueKind.Array, "$", issues) is { } telemetry)
        {
            var index = 0;
            foreach (var sample in telemetry.EnumerateArray())
            {
                var path = $"$.telemetry[{index++}]";
                var values = ObjectFields(sample, path, issues);
                if (values is null) continue;
                foreach (var name in new[] { "timeSec", "x", "y", "z", "speedMps", "altitudeM", "pitchDeg", "rollDeg", "yawDeg" })
                {
                    Required(values, name, JsonValueKind.Number, path, issues);
                }
                foreach (var name in new[] { "batteryPercent", "controlLinkQualityPercent", "videoLinkQualityPercent" })
                {
                    Optional(values, name, JsonValueKind.Number, path, issues);
                }
            }
        }

        if (Required(fields, "events", JsonValueKind.Array, "$", issues) is { } events)
        {
            var index = 0;
            foreach (var sessionEvent in events.EnumerateArray())
            {
                var path = $"$.events[{index++}]";
                var values = ObjectFields(sessionEvent, path, issues);
                if (values is null) continue;
                Required(values, "eventId", JsonValueKind.String, path, issues);
                Required(values, "eventType", JsonValueKind.String, path, issues);
                Required(values, "timeSec", JsonValueKind.Number, path, issues);
                Optional(values, "entityId", JsonValueKind.String, path, issues);
                Optional(values, "value", JsonValueKind.String, path, issues);
                Optional(values, "metadata", JsonValueKind.Object, path, issues);
                if (values.TryGetValue("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
                {
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in metadata.EnumerateObject())
                    {
                        if (!keys.Add(entry.Name)) issues.Add(new("DuplicateProperty", path + ".metadata", "Metadata keys must be unique."));
                        if (entry.Value.ValueKind != JsonValueKind.String)
                        {
                            issues.Add(new("InvalidType", path + ".metadata", "Metadata values must be strings."));
                        }
                    }
                }
            }
        }

        return new ValidationResult(issues);
    }

    private static Dictionary<string, JsonElement>? ObjectFields(JsonElement element, string path, List<ValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("InvalidType", path, "Expected a JSON object."));
            return null;
        }

        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (!fields.TryAdd(property.Name, property.Value))
            {
                issues.Add(new("DuplicateProperty", path + "." + property.Name, "Property names must be unique, ignoring casing."));
            }
        }
        return fields;
    }

    private static JsonElement? Required(Dictionary<string, JsonElement> fields, string name, JsonValueKind kind, string path, List<ValidationIssue> issues)
    {
        if (!fields.TryGetValue(name, out var value))
        {
            issues.Add(new("RequiredField", path + "." + name, "Required field is missing."));
            return null;
        }
        if (value.ValueKind != kind)
        {
            issues.Add(new("InvalidType", path + "." + name, $"Expected {kind}."));
            return null;
        }
        if (kind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
        {
            issues.Add(new("RequiredValue", path + "." + name, "Value must not be empty or whitespace."));
            return null;
        }
        return value;
    }

    private static void Optional(Dictionary<string, JsonElement> fields, string name, JsonValueKind kind, string path, List<ValidationIssue> issues, bool allowBoolean = false)
    {
        if (fields.TryGetValue(name, out var value) && value.ValueKind != JsonValueKind.Null &&
            value.ValueKind != kind && !(allowBoolean && value.ValueKind == JsonValueKind.False))
        {
            issues.Add(new("InvalidType", path + "." + name, $"Expected {kind} or null."));
        }
    }
}
