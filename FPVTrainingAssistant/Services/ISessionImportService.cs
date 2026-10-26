namespace FPVTrainingAssistant.Services;

public interface ISessionImportService
{
    Task<SessionImportResult> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
