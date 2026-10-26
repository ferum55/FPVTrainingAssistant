using System.Text.Json;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

public sealed class ReplayContractTests : IDisposable
{
    private readonly ReplayTestFiles _files = new();
    public void Dispose() => _files.Dispose();

    [Theory]
    [InlineData("valid_obstacle_course.json", "1.0", 82.67)]
    [InlineData("valid_target_mission.json", "1.0", 90)]
    [InlineData("replay_obstacle_course_v1_1.json", "1.1", 82.67)]
    [InlineData("replay_target_mission_v1_1.json", "1.1", 90)]
    public async Task VersionsFixturesAndScoresRemainCompatible(string file, string version, double score)
    {
        var path = ReplayTestFiles.FixturePath(file);
        var before = await File.ReadAllBytesAsync(path);
        var imported = await new SessionImportService().ImportAsync(path);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Validation.Errors));
        Assert.Equal(version, imported.Session!.ContractVersion);
        Assert.Equal(Path.GetFullPath(path), imported.SourceFilePath);
        var result = new SessionAnalyzer().Analyze(imported.Session, new ExerciseProfileProvider().Resolve(imported.Session)!);
        Assert.Equal(score, result.FinalScore);
        Assert.Equal(750, imported.Session.SimulatorScore);
        Assert.Null(imported.Session.AssistantFinalScore);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.DoesNotContain("sourceFilePath", JsonSerializer.Serialize(imported.Session, SessionJson.Options));
        Assert.DoesNotContain(Path.GetDirectoryName(path)!, JsonSerializer.Serialize(imported.Session, SessionJson.Options));
    }

    [Theory]
    [InlineData("1.0", true)]
    [InlineData("1.1", true)]
    [InlineData("2.0", false)]
    [InlineData("1.2", false)]
    public async Task VersionIsExplicitAndReplayIsOptional(string version, bool valid)
    {
        var document = ReplayTestFiles.Document("valid_target_mission.json");
        document["contractVersion"] = version;
        var imported = await _files.Import(document);
        Assert.Equal(valid, imported.IsSuccess);
        if (valid) Assert.Null(imported.Session!.Replay);
        else Error(imported, "UnsupportedContractVersion", "$.contractVersion");
    }

    [Fact]
    public async Task ExtendedValuesSurviveImportAndNormalization()
    {
        var document = ReplayTestFiles.Document();
        var sample = document["telemetry"]![0]!;
        sample["cameraTiltDeg"] = 375;
        sample["verticalSpeedMps"] = -3;
        sample["currentA"] = -0.5;
        sample["failsafe"] = true;
        sample["armed"] = false;
        sample["flightMode"] = "FutureFlightMode";
        var imported = await _files.Import(document);
        Assert.True(imported.IsSuccess);
        var actual = imported.Session!.Telemetry[0];
        Assert.Equal(15, actual.CameraTiltDeg);
        Assert.Equal(100, actual.CameraFovDeg);
        Assert.Equal(-3, actual.VerticalSpeedMps);
        Assert.Equal(-0.5, actual.CurrentA);
        Assert.Equal(16.8, actual.BatteryVoltageV);
        Assert.Equal(4.2, actual.CellVoltageV);
        Assert.Equal(0, actual.ConsumedMah);
        Assert.Equal(-65, actual.ControlRssiDbm);
        Assert.True(actual.Failsafe);
        Assert.False(actual.Armed);
        Assert.Equal("FutureFlightMode", actual.FlightMode);
    }

    [Fact]
    public async Task AllExtensionsCanBeNullOrAbsent()
    {
        var document = ReplayTestFiles.Document("valid_obstacle_course.json");
        foreach (var field in ExtendedFields) document["telemetry"]![0]![field] = null;
        document["replay"] = null;
        var imported = await _files.Import(document);
        Assert.True(imported.IsSuccess);
        Assert.Null(imported.Session!.Replay);
        Assert.Null(imported.Session.Telemetry[0].CameraFovDeg);
        Assert.Null(imported.Session.Telemetry[0].Armed);
    }

    private static readonly string[] ExtendedFields = ["verticalSpeedMps", "cameraTiltDeg", "cameraFovDeg", "batteryVoltageV", "cellVoltageV", "currentA", "consumedMah", "controlRssiDbm", "failsafe", "flightMode", "armed"];

    [Theory]
    [InlineData("cameraFovDeg", 0, "InvalidCameraFov")]
    [InlineData("cameraFovDeg", -1, "InvalidCameraFov")]
    [InlineData("cameraFovDeg", 180, "InvalidCameraFov")]
    [InlineData("cameraFovDeg", 181, "InvalidCameraFov")]
    [InlineData("batteryVoltageV", -0.1, "NegativeValue")]
    [InlineData("cellVoltageV", -0.1, "NegativeValue")]
    [InlineData("consumedMah", -0.1, "NegativeValue")]
    public async Task InvalidExtendedRangesAreRejected(string field, double value, string code)
    {
        var document = ReplayTestFiles.Document();
        document["telemetry"]![0]![field] = value;
        Error(await _files.Import(document), code, "$.telemetry[0]." + field);
    }

    [Theory]
    [InlineData("verticalSpeedMps")]
    [InlineData("cameraTiltDeg")]
    [InlineData("cameraFovDeg")]
    [InlineData("batteryVoltageV")]
    [InlineData("cellVoltageV")]
    [InlineData("currentA")]
    [InlineData("consumedMah")]
    [InlineData("controlRssiDbm")]
    public async Task EveryExtendedNumberMustBeFinite(string field)
    {
        var document = ReplayTestFiles.Document();
        document["telemetry"]![0]![field] = "overflow-sentinel";
        var text = document.ToJsonString().Replace("\"overflow-sentinel\"", "1e999", StringComparison.Ordinal);
        Error(await _files.ImportText(text), "NonFiniteNumber", "$.telemetry[0]." + field);
    }

    [Theory]
    [InlineData("failsafe", "true")]
    [InlineData("armed", "false")]
    [InlineData("cameraFovDeg", "100")]
    public async Task ExtendedFieldTypesAreStrict(string field, string value)
    {
        var document = ReplayTestFiles.Document();
        document["telemetry"]![0]![field] = value;
        Error(await _files.Import(document), "InvalidType", "$.telemetry[0]." + field);
    }

    [Fact]
    public async Task EntityIdentifiersAndStatesAreOpenAndRetained()
    {
        var document = ReplayTestFiles.Document();
        var track = document["replay"]!["entities"]![0]!;
        track["entityType"] = "FutureEntity";
        track["archetypeId"] = "future.visual.v2";
        track["samples"]![0]!["state"] = "FutureState";
        track["samples"]![0]!["isActive"] = false;
        var imported = await _files.Import(document);
        Assert.True(imported.IsSuccess);
        var actual = Assert.Single(imported.Session!.Replay!.Entities);
        Assert.Equal("FutureEntity", actual.EntityType);
        Assert.Equal("future.visual.v2", actual.ArchetypeId);
        Assert.Equal("FutureState", actual.Samples[0].State);
        Assert.False(actual.Samples[0].IsActive);
        Assert.Equal("true", actual.Metadata!["synthetic"]);
    }

    [Theory]
    [InlineData("entityId")]
    [InlineData("entityType")]
    public async Task BlankTrackIdentityIsRejected(string field)
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"]![0]![field] = " ";
        Error(await _files.Import(document), "RequiredValue", "$.replay.entities[0]." + field);
    }

    [Fact]
    public async Task DuplicateEntityIdsAreRejected()
    {
        var document = ReplayTestFiles.Document();
        var tracks = document["replay"]!["entities"]!.AsArray();
        tracks.Add(tracks[0]!.DeepClone());
        Error(await _files.Import(document), "DuplicateEntityId", "$.replay.entities[1].entityId");
    }

    [Theory]
    [InlineData(-1, "NegativeTime")]
    [InlineData(0, "EntityTimeOrder")]
    [InlineData(11, "TimeOutsideSession")]
    public async Task EntitySampleTimePolicyIsEnforced(double time, string code)
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"]![0]!["samples"]![1]!["timeSec"] = time;
        Error(await _files.Import(document), code, "$.replay.entities[0].samples[1].timeSec");
    }

    [Fact]
    public async Task EntityDurationToleranceMatchesTelemetry()
    {
        var document = ReplayTestFiles.Document();
        var sample = document["replay"]!["entities"]![0]!["samples"]![5]!;
        sample["timeSec"] = 10 + SessionValidator.TimeToleranceSeconds / 2;
        Assert.True((await _files.Import(document)).IsSuccess);
        sample["timeSec"] = 10 + SessionValidator.TimeToleranceSeconds * 2;
        Error(await _files.Import(document), "TimeOutsideSession", "$.replay.entities[0].samples[5].timeSec");
    }

    [Theory]
    [InlineData("timeSec")]
    [InlineData("x")]
    [InlineData("y")]
    [InlineData("z")]
    [InlineData("pitchDeg")]
    [InlineData("rollDeg")]
    [InlineData("yawDeg")]
    public async Task EveryEntityTransformComponentMustBeFinite(string field)
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"]![0]!["samples"]![0]![field] = "overflow-sentinel";
        Error(await _files.ImportText(document.ToJsonString().Replace("\"overflow-sentinel\"", "1e999", StringComparison.Ordinal)),
            "NonFiniteNumber", "$.replay.entities[0].samples[0]." + field);
    }

    [Theory]
    [InlineData("[]", "EntitySamplesRequired")]
    [InlineData("null", "InvalidType")]
    public async Task TrackRequiresNonemptyNonnullSamples(string json, string code)
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"]![0]!["samples"] = JsonNode.Parse(json);
        Error(await _files.Import(document), code, "$.replay.entities[0].samples");
    }

    [Fact]
    public async Task ReplayContextRequiresEnvironmentAndEntitiesArray()
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!.AsObject().Remove("environmentId");
        Error(await _files.Import(document), "RequiredField", "$.replay.environmentId");
        document = ReplayTestFiles.Document();
        document["replay"]!["entities"] = null;
        Error(await _files.Import(document), "InvalidType", "$.replay.entities");
    }

    [Theory]
    [InlineData("environmentId", "/Game/Maps/Training")]
    [InlineData("environmentId", "C:\\Maps\\Training")]
    [InlineData("scenarioId", "/Game/Missions/Scenario")]
    [InlineData("archetypeId", "Blueprint'/Game/Actors/Vehicle.Vehicle_C'")]
    public async Task EngineAndFilesystemPathsCannotBeNeutralIds(string field, string value)
    {
        var document = ReplayTestFiles.Document();
        var owner = field == "archetypeId" ? document["replay"]!["entities"]![0]! : document["replay"]!;
        owner[field] = value;
        var path = field == "archetypeId" ? "$.replay.entities[0].archetypeId" : "$.replay." + field;
        Error(await _files.Import(document), "NeutralIdRequired", path);
    }

    [Fact]
    public async Task ReplayMetadataCannotSilentlyLoseInvalidValues()
    {
        var document = ReplayTestFiles.Document();
        document["replay"]!["entities"]![0]!["metadata"]!["bad"] = true;
        document["replay"]!["entities"]![0]!["samples"]![0]!["metadata"]!["bad"] = null;
        var imported = await _files.Import(document);
        Error(imported, "InvalidType", "$.replay.entities[0].metadata");
        Error(imported, "InvalidType", "$.replay.entities[0].samples[0].metadata");
    }

    [Fact]
    public async Task ReplayAndExtendedTelemetryDoNotChangeCriterionEvidenceOrRecommendations()
    {
        foreach (var file in new[] { "replay_obstacle_course_v1_1.json", "replay_target_mission_v1_1.json" })
        {
            var document = ReplayTestFiles.Document(file);
            var rich = (await _files.Import(document)).Session!;
            document.Remove("replay");
            foreach (var sample in document["telemetry"]!.AsArray())
                foreach (var field in ExtendedFields) sample!.AsObject().Remove(field);
            var basic = (await _files.Import(document)).Session!;
            var profile = new ExerciseProfileProvider().Resolve(rich)!;
            var analyzer = new SessionAnalyzer();
            var richResult = analyzer.Analyze(rich, profile);
            var basicResult = analyzer.Analyze(basic, profile);
            Assert.Equal(basicResult.FinalScore, richResult.FinalScore);
            Assert.Equal(JsonSerializer.Serialize(basicResult), JsonSerializer.Serialize(richResult));
        }
    }

    private static void Error(SessionImportResult result, string code, string path)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Null(result.TechnicalError);
        Assert.Contains(result.Validation.Errors, issue => issue.Code == code && issue.Path == path);
    }
}
