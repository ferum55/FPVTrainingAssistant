using System.IO;
using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed class ReplayCapabilityService
{
    public const string MissingEnvironment = "This session does not contain the environment information required for 3D replay.";
    public const string MissingConfiguration = "Simulator executable is not configured.";
    public const string MissingSourceFile = "The source replay file is no longer available.";
    public const string MissingExecutable = "The configured simulator executable is no longer available.";

    /// <summary>Evaluates a validated session and current local file availability; does not change the session.</summary>
    public ReplayCapability Evaluate(TrainingSession? session, string? sourceFilePath, string? simulatorExecutablePath)
    {
        var drone = session?.Telemetry?.Count >= 2;
        var environment = SessionValidator.IsNeutralId(session?.Replay?.EnvironmentId);
        var dynamicEntities = session?.Replay?.Entities?.Any(track => track is not null &&
            SessionValidator.IsValidEntityTrack(track, session.EndedAtUtc >= session.StartedAtUtc ? session.Duration.TotalSeconds : null)) == true;
        var limitations = new List<string>();
        if (!drone) limitations.Add("At least two telemetry samples are required for drone replay.");
        if (!environment) limitations.Add(MissingEnvironment);
        if (!File.Exists(sourceFilePath)) limitations.Add(MissingSourceFile);
        if (string.IsNullOrWhiteSpace(simulatorExecutablePath)) limitations.Add(MissingConfiguration);
        else if (!File.Exists(simulatorExecutablePath)) limitations.Add(MissingExecutable);
        return new(drone, environment, dynamicEntities, limitations.Count == 0, limitations.AsReadOnly());
    }
}
