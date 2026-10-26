using System.Text.Json.Nodes;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Tests;

internal static class MockReplayTestData
{
    public static ReplayLaunchRequest Request(string file = "replay_obstacle_course_v1_1.json", Action<JsonObject>? edit = null)
    {
        var document = ReplayTestFiles.Document(file);
        edit?.Invoke(document);
        var session = ReplayTestFiles.Session(document);
        var profile = new ExerciseProfileProvider().Resolve(session)!;
        return new(session, profile, new SessionAnalyzer().Analyze(session, profile), ReplayTestFiles.FixturePath(file));
    }

    public static SimulatorReplayMockViewModel ViewModel(string file = "replay_obstacle_course_v1_1.json", Action<JsonObject>? edit = null)
    {
        var request = Request(file, edit);
        return new(request.Session, request.Profile, request.Analysis);
    }
}
