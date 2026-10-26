using System.IO;

namespace FPVTrainingAssistant.Data;

public interface IApplicationDataPathProvider { string DatabasePath { get; } }

public sealed class ApplicationDataPathProvider : IApplicationDataPathProvider
{
    public string DatabasePath
    {
        get
        {
            var folder=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if(string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("The local application-data directory is unavailable.");
            return Path.Combine(folder,"FPVTrainingAssistant","assistant.db");
        }
    }
}
