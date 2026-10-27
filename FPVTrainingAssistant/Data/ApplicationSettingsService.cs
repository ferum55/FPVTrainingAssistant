using System.Diagnostics;
using System.Globalization;
using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;
using static FPVTrainingAssistant.Data.SqliteData;

namespace FPVTrainingAssistant.Data;

public sealed class ApplicationSettingsService(DatabaseConnectionFactory connections) : IApplicationSettingsService
{
    public ApplicationSettings Current { get; private set; } = new();

    public Task<ApplicationSettingsResult> LoadAsync() => Task.Run(() =>
    {
        try
        {
            using var c = connections.Open();
            var values = Query(c, null, "SELECT key,value FROM app_settings", r => new KeyValuePair<string,string>(r.GetString(0),r.GetString(1))).ToDictionary();
            var speed = double.Parse(values["default_replay_speed"], CultureInfo.InvariantCulture);
            var settings = new ApplicationSettings(speed, bool.Parse(values["show_minimap"]), bool.Parse(values["show_event_log"]));
            if (speed is not (0.5 or 1 or 2)) throw new InvalidOperationException("Unsupported replay speed in settings.");
            Current = settings;
            return new ApplicationSettingsResult(true, "Application settings loaded.");
        }
        catch (Exception e)
        {
            Trace.TraceError("Application settings load: {0}", e);
            return new ApplicationSettingsResult(false, "Application settings could not be loaded. Current replay defaults remain available.");
        }
    });

    public Task<ApplicationSettingsResult> SaveAsync(ApplicationSettings settings) => Task.Run(() =>
    {
        if (settings.DefaultReplaySpeed is not (0.5 or 1 or 2)) return new ApplicationSettingsResult(false, "Default replay speed must be 0.5x, 1x or 2x.");
        try
        {
            using var c = connections.Open(); using var t = c.BeginTransaction();
            foreach (var pair in new[] { ("default_replay_speed", settings.DefaultReplaySpeed.ToString(CultureInfo.InvariantCulture)),
                ("show_minimap", settings.ShowMinimapByDefault.ToString()), ("show_event_log", settings.ShowEventLogByDefault.ToString()) })
                Execute(c,t,"INSERT INTO app_settings(key,value) VALUES($p0,$p1) ON CONFLICT(key) DO UPDATE SET value=excluded.value", pair.Item1,pair.Item2);
            t.Commit(); Current = settings;
            return new ApplicationSettingsResult(true,"Application settings saved.");
        }
        catch (Exception e)
        {
            Trace.TraceError("Application settings save: {0}",e);
            return new ApplicationSettingsResult(false,"Application settings could not be saved.");
        }
    });
}
