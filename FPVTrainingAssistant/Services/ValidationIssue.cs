namespace FPVTrainingAssistant.Services;

public sealed record ValidationIssue(
    string Code,
    string Path,
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Error);
