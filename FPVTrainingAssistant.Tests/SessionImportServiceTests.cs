using System.Text.Json;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class SessionImportServiceTests
{
    private readonly SessionImportService _importer = new();

    [Theory]
    [InlineData("valid_obstacle_course.json", ExerciseTypes.ObstacleCourse)]
    [InlineData("valid_target_mission.json", ExerciseTypes.TargetMission)]
    [InlineData("example_contract_v1.json", ExerciseTypes.ObstacleCourse)]
    public async Task ValidSessionImports(string fileName, string exerciseType)
    {
        var result = await _importer.ImportAsync(FixturePath(fileName));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Validation.Errors);
        Assert.Null(result.TechnicalError);
        Assert.Equal(exerciseType, result.Session!.ExerciseType);
        Assert.Null(result.Session.AssistantFinalScore);
        Assert.Equal(750d, result.Session.SimulatorScore);
    }

    [Theory]
    [InlineData("invalid_missing_session_id.json", "RequiredField", "$.sessionId")]
    [InlineData("invalid_bad_telemetry_time.json", "TelemetryTimeOrder", "$.telemetry[1].timeSec")]
    [InlineData("invalid_out_of_range_percentage.json", "PercentageOutOfRange", "$.telemetry[1].batteryPercent")]
    public async Task InvalidFixtureReturnsIssues(string fileName, string code, string path)
    {
        AssertError(await _importer.ImportAsync(FixturePath(fileName)), code, path);
    }

    [Fact]
    public async Task UnknownEventIsAcceptedAndRetained()
    {
        var result = await _importer.ImportAsync(FixturePath("valid_unknown_event.json"));
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Session!.Events, e => e.EventType == "FutureWindSensorAlert");
        Assert.Equal(result.Session.Events.OrderBy(e => e.TimeSec).Select(e => e.EventId), result.Session.Events.Select(e => e.EventId));
    }

    [Fact]
    public async Task UnsupportedVersionIsRejected()
    {
        var document = Fixture();
        document["contractVersion"] = "2.0";
        AssertError(await Import(document), "UnsupportedContractVersion", "$.contractVersion");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{ /* comment */ }")]
    [InlineData("{\"contractVersion\":\"1.0\",}")]
    [InlineData("{\"x\": NaN}")]
    public async Task MalformedJsonFailsGracefully(string json)
    {
        var result = await ImportText(json);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Null(result.TechnicalError);
        Assert.Contains(result.Validation.Errors, issue => issue.Code == "InvalidJson");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    public async Task NonObjectRootIsRejected(string json) =>
        AssertError(await ImportText(json), "InvalidType", "$");

    [Fact]
    public async Task DamageZoneMetadataIsRequired()
    {
        var document = Fixture("valid_target_mission.json");
        document["events"]![2]!.AsObject().Remove("metadata");
        AssertError(await Import(document), "DamageZoneRequired", "$.events[2].metadata.damageZone");
    }

    [Fact]
    public async Task NegativeTelemetryTimeIsRejected()
    {
        var document = Fixture();
        document["telemetry"]![0]!["timeSec"] = -1;
        AssertError(await Import(document), "NegativeTime", "$.telemetry[0].timeSec");
    }

    [Fact]
    public async Task EventAfterSessionIsRejected()
    {
        var document = Fixture();
        document["events"]![0]!["timeSec"] = 11;
        AssertError(await Import(document), "TimeOutsideSession", "$.events[0].timeSec");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public async Task AssistantScoreOutsideRangeIsAnOrdinaryValidationError(double score)
    {
        var document = Fixture();
        document["assistantFinalScore"] = score;
        AssertError(await Import(document), "PercentageOutOfRange", "$.assistantFinalScore");
    }

    [Fact]
    public async Task UnknownExerciseProducesOnlyWarning()
    {
        var document = Fixture();
        document["exerciseType"] = "FutureExercise";
        var result = await Import(document);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Validation.Errors);
        Assert.Contains(result.Validation.Warnings, issue => issue.Code == "UnknownExerciseType");
    }

    [Fact]
    public async Task CasingAndUnknownPropertiesAreCompatible()
    {
        var document = Fixture();
        document["SESSIONID"] = document["sessionId"]!.DeepClone();
        document.Remove("sessionId");
        document["futureExtension"] = new JsonObject { ["extra"] = true };
        document["telemetry"]![0]!["futureSensor"] = 123;
        Assert.True((await Import(document)).IsSuccess);
    }

    [Theory]
    [InlineData("contractVersion")]
    [InlineData("telemetry")]
    [InlineData("events")]
    [InlineData("profileVersion")]
    [InlineData("startedAtUtc")]
    public async Task MissingRequiredRootFieldIsNotHiddenByModelDefaults(string field)
    {
        var document = Fixture();
        document.Remove(field);
        AssertError(await Import(document), "RequiredField", "$." + field);
    }

    [Fact]
    public async Task MissingRequiredTelemetryNumberIsNotDefaultedToZero()
    {
        var document = Fixture();
        document["telemetry"]![0]!.AsObject().Remove("x");
        AssertError(await Import(document), "RequiredField", "$.telemetry[0].x");
    }

    [Theory]
    [InlineData("sessionId")]
    [InlineData("operatorId")]
    [InlineData("profileId")]
    public async Task BlankGuardedIdentifiersProduceValidationIssues(string field)
    {
        var document = Fixture();
        document[field] = " ";
        AssertError(await Import(document), "RequiredValue", "$." + field);
    }

    [Theory]
    [InlineData("2026-10-26T10:00:00")]
    [InlineData("2026-10-26T12:00:00+02:00")]
    public async Task NonExplicitOrNonUtcTimestampIsRejected(string timestamp)
    {
        var document = Fixture();
        document["startedAtUtc"] = timestamp;
        AssertError(await Import(document), "UtcRequired", "$.startedAtUtc");
    }

    [Fact]
    public async Task ExplicitZeroOffsetIsAccepted()
    {
        var document = Fixture();
        document["startedAtUtc"] = "2026-10-26T10:00:00+00:00";
        Assert.True((await Import(document)).IsSuccess);
    }

    [Fact]
    public async Task ReversedSessionTimesAreRejectedBeforeDurationAccess()
    {
        var document = Fixture();
        document["endedAtUtc"] = "2026-10-26T09:59:59Z";
        AssertError(await Import(document), "InvalidTimeRange", "$.endedAtUtc");
    }

    [Fact]
    public async Task ErrorsAreAccumulatedForIndependentTelemetryProblems()
    {
        var document = Fixture();
        document["telemetry"]![0]!["speedMps"] = -2;
        document["telemetry"]![1]!["batteryPercent"] = 110;
        document["telemetry"]![2]!["timeSec"] = 11;
        var result = await Import(document);
        AssertError(result, "NegativeSpeed", "$.telemetry[0].speedMps");
        AssertError(result, "PercentageOutOfRange", "$.telemetry[1].batteryPercent");
        AssertError(result, "TimeOutsideSession", "$.telemetry[2].timeSec");
    }

    [Theory]
    [InlineData("CheckpointPassed")]
    [InlineData("CheckpointMissed")]
    [InlineData("TargetDetected")]
    [InlineData("TargetHit")]
    [InlineData("TargetDamageZone")]
    public async Task KnownEntityEventsRequireEntityId(string eventType)
    {
        var document = Fixture();
        document["events"]![0]!["eventType"] = eventType;
        document["events"]![0]!.AsObject().Remove("entityId");
        AssertError(await Import(document), "RequiredValue", "$.events[0].entityId");
    }

    [Theory]
    [InlineData("Collision")]
    [InlineData("FalseTargetDetection")]
    [InlineData("ObjectiveCompleted")]
    [InlineData("Crash")]
    [InlineData("MissionCompleted")]
    public async Task OtherKnownEventsDoNotRequireEntityId(string eventType)
    {
        var document = Fixture();
        document["events"]![0]!["eventType"] = eventType;
        document["events"]![0]!.AsObject().Remove("entityId");
        Assert.True((await Import(document)).IsSuccess);
    }

    [Fact]
    public async Task DuplicateEventIdsAreRejected()
    {
        var document = Fixture();
        document["events"]![1]!["eventId"] = document["events"]![0]!["eventId"]!.DeepClone();
        AssertError(await Import(document), "DuplicateEventId", "$.events[1].eventId");
    }

    [Fact]
    public async Task EqualTimeEventsKeepTheirInputOrder()
    {
        var result = await _importer.ImportAsync(FixturePath("valid_target_mission.json"));
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "TargetHit", "TargetDamageZone" }, result.Session!.Events.Where(e => e.TimeSec == 8).Select(e => e.EventType));
    }

    [Fact]
    public async Task AnglesAreWrappedAfterValidationWithoutChangingPositionOrScore()
    {
        var document = Fixture();
        var sample = document["telemetry"]![0]!;
        sample["pitchDeg"] = 540;
        sample["rollDeg"] = -541;
        sample["yawDeg"] = 721;
        document["assistantFinalScore"] = 25;
        var result = await Import(document);
        Assert.True(result.IsSuccess);
        Assert.Equal(-180, result.Session!.Telemetry[0].PitchDeg);
        Assert.Equal(179, result.Session.Telemetry[0].RollDeg);
        Assert.Equal(1, result.Session.Telemetry[0].YawDeg);
        Assert.Equal(2, result.Session.Telemetry[0].Z);
        Assert.Equal(750d, result.Session.SimulatorScore);
        Assert.Equal(25d, result.Session.AssistantFinalScore);
        Assert.Contains(result.Validation.Warnings, issue => issue.Code == "ImportedAssistantScore");
    }

    [Fact]
    public async Task MissingCheckpointAndTargetEvidenceAreWarnings()
    {
        foreach (var exerciseType in new[] { ExerciseTypes.ObstacleCourse, ExerciseTypes.TargetMission })
        {
            var document = Fixture();
            document["exerciseType"] = exerciseType;
            document["events"] = new JsonArray();
            var result = await Import(document);
            Assert.True(result.IsSuccess);
            Assert.Single(result.Validation.Warnings);
        }
    }

    [Fact]
    public async Task ObstacleCourseNeedsTwoSamples()
    {
        var document = Fixture();
        document["telemetry"] = new JsonArray(document["telemetry"]![0]!.DeepClone());
        AssertError(await Import(document), "InsufficientTelemetry", "$.telemetry");
    }

    [Fact]
    public async Task TolerancePermitsOnlySmallDurationOvershoot()
    {
        var document = Fixture();
        document["events"]![0]!["timeSec"] = 10 + SessionValidator.TimeToleranceSeconds / 2;
        Assert.True((await Import(document)).IsSuccess);
        document["events"]![0]!["timeSec"] = 10 + SessionValidator.TimeToleranceSeconds * 2;
        AssertError(await Import(document), "TimeOutsideSession", "$.events[0].timeSec");
    }

    [Theory]
    [InlineData("telemetry")]
    [InlineData("events")]
    public async Task NullCollectionsAreOrdinaryValidationErrors(string name)
    {
        var document = Fixture();
        document[name] = null;
        AssertError(await Import(document), "InvalidType", "$." + name);
    }

    [Fact]
    public async Task NumericStringsAndNullSamplesAreRejected()
    {
        var document = Fixture();
        document["telemetry"]![0]!["speedMps"] = "2.5";
        document["telemetry"]![1] = null;
        var result = await Import(document);
        AssertError(result, "InvalidType", "$.telemetry[0].speedMps");
        AssertError(result, "InvalidType", "$.telemetry[1]");
    }

    [Fact]
    public async Task MetadataMustHaveStringValues()
    {
        var document = Fixture();
        document["events"]![0]!["metadata"] = new JsonObject { ["value"] = 12 };
        AssertError(await Import(document), "InvalidType", "$.events[0].metadata");
    }

    [Fact]
    public async Task DuplicatePropertiesAreRejectedIgnoringCase()
    {
        var text = Fixture().ToJsonString().Replace("\"sessionId\":", "\"SESSIONID\":\"another\",\"sessionId\":", StringComparison.Ordinal);
        var result = await ImportText(text);
        Assert.Contains(result.Validation.Errors, issue => issue.Code == "DuplicateProperty");
        Assert.Null(result.TechnicalError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyPathReturnsIssue(string? path) =>
        AssertError(await _importer.ImportAsync(path!), "InvalidFilePath", "$");

    [Fact]
    public async Task MissingFileReturnsIssue() =>
        AssertError(await _importer.ImportAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")), "FileNotFound", "$");

    [Fact]
    public async Task FileReadFailureReturnsTechnicalError()
    {
        var path = Path.Combine(Path.GetTempPath(), "FPVTrainingAssistant-locked-" + Guid.NewGuid() + ".json");
        try
        {
            using var lockedFile = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            var result = await _importer.ImportAsync(path);
            Assert.False(result.IsSuccess);
            Assert.Null(result.Session);
            Assert.NotNull(result.TechnicalError);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _importer.ImportAsync(FixturePath("valid_obstacle_course.json"), source.Token));
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "TestSessions", name);
    private static JsonObject Fixture(string name = "valid_obstacle_course.json") => JsonNode.Parse(File.ReadAllText(FixturePath(name)))!.AsObject();
    private Task<SessionImportResult> Import(JsonObject document) => ImportText(document.ToJsonString());

    private async Task<SessionImportResult> ImportText(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "FPVTrainingAssistant-test-" + Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(path, text);
            return await _importer.ImportAsync(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertError(SessionImportResult result, string code, string path)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Null(result.TechnicalError);
        Assert.Contains(result.Validation.Errors, issue => issue.Code == code && issue.Path == path);
    }
}
