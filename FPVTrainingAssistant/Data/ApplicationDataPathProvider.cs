using System.IO;

namespace FPVTrainingAssistant.Data;

public interface IApplicationDataPathProvider { string DatabasePath { get; } }

public sealed class ApplicationDataPathProvider : IApplicationDataPathProvider
{
    private readonly string? _directory;

    public ApplicationDataPathProvider(string? directory = null)
    {
        if (directory is not null && (!Path.IsPathFullyQualified(directory) || string.IsNullOrWhiteSpace(directory)))
            throw new ArgumentException("The data directory must be an absolute path.", nameof(directory));
        _directory = directory is null ? null : Path.GetFullPath(directory);
    }

    // Explicit developer/test isolation; never resets or copies an existing database.
    public static ApplicationDataPathProvider FromArguments(IReadOnlyList<string> args) => args.Count switch
    {
        0 => new(),
        2 when args[0] == "--data-directory" => new(args[1]),
        _ => throw new ArgumentException("Usage: FPVTrainingAssistant [--data-directory <absolute directory>]")
    };

    public string DatabasePath
    {
        get
        {
            if (_directory is not null) return Path.Combine(_directory, "assistant.db");
            var folder=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if(string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("The local application-data directory is unavailable.");
            return Path.Combine(folder,"FPVTrainingAssistant","assistant.db");
        }
    }
}
