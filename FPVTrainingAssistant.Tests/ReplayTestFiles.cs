using System.Text.Json;
using System.Text.Json.Nodes;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

internal sealed class ReplayTestFiles : IDisposable
{
    private readonly List<string> _paths = [];
    public static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "TestSessions", name);
    public static JsonObject Document(string name = "replay_obstacle_course_v1_1.json") =>
        JsonNode.Parse(File.ReadAllText(FixturePath(name)))!.AsObject();
    public static TrainingSession Session(JsonObject document) => document.Deserialize<TrainingSession>(SessionJson.Options)!;

    public string Create(string text = "synthetic", string suffix = ".json")
    {
        var path = Path.Combine(Path.GetTempPath(), "FPV replay test " + Guid.NewGuid() + suffix);
        File.WriteAllText(path, text);
        _paths.Add(path);
        return path;
    }

    public Task<SessionImportResult> Import(JsonObject document) => ImportText(document.ToJsonString());
    public Task<SessionImportResult> ImportText(string text) => new SessionImportService().ImportAsync(Create(text));

    public void Dispose()
    {
        foreach (var path in _paths) File.Delete(path);
    }
}
