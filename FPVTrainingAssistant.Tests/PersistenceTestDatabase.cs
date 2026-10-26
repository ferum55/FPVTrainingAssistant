using System.Text.Json.Nodes;
using FPVTrainingAssistant.Data;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.Tests;

internal sealed class PersistenceTestDatabase : IDisposable, IApplicationDataPathProvider
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FPVTrainingAssistant-db-" + Guid.NewGuid());
    public string DatabasePath => Path.Combine(_directory, "assistant.db");
    public DatabaseConnectionFactory Connections { get; }
    public SessionRepository Sessions { get; }
    public HistoryRepository History { get; }
    public DatabaseInitializer Initializer { get; }
    public PersistenceTestDatabase()
    {
        Connections = new(this); Sessions = new(Connections); History = new(Connections); Initializer = new(Connections);
    }
    public async Task Initialize() => Assert.True((await Initializer.InitializeAsync()).IsSuccess);
    public void Execute(string sql)
    {
        using var c = Connections.Open(); using var command = c.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    public object? Scalar(string sql)
    {
        using var c = Connections.Open(); using var command = c.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar();
    }
    public static StoredSession Snapshot(string file = "replay_obstacle_course_v1_1.json", Action<JsonObject>? edit = null)
    {
        var request = MockReplayTestData.Request(file, edit);
        return new(request.Session, request.Profile, request.Analysis);
    }
    public void Dispose()
    {
        if (!Directory.Exists(_directory)) return;
        foreach (var file in Directory.EnumerateFiles(_directory)) File.Delete(file);
        Directory.Delete(_directory);
    }
}
