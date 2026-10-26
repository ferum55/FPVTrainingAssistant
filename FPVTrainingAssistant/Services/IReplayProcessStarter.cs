using System.Diagnostics;

namespace FPVTrainingAssistant.Services;

public interface IReplayProcessStarter
{
    bool Start(ProcessStartInfo startInfo);
}

public sealed class ReplayProcessStarter : IReplayProcessStarter
{
    public bool Start(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        return process is not null;
    }
}
