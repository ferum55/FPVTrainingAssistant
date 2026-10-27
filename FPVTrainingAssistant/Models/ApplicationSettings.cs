namespace FPVTrainingAssistant.Models;

public sealed record ApplicationSettings(double DefaultReplaySpeed = 1,
    bool ShowMinimapByDefault = true, bool ShowEventLogByDefault = true);
