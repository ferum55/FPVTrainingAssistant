using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public sealed record SessionImportResult(
    TrainingSession? Session,
    ValidationResult Validation,
    string? TechnicalError = null,
    string? SourceFilePath = null)
{
    public bool IsSuccess => Session is not null && Validation.IsValid && TechnicalError is null;
}
