using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed record ApplicationSettingsResult(bool IsSuccess, string Message);
public interface IApplicationSettingsService
{
    ApplicationSettings Current { get; }
    Task<ApplicationSettingsResult> LoadAsync();
    Task<ApplicationSettingsResult> SaveAsync(ApplicationSettings settings);
}
