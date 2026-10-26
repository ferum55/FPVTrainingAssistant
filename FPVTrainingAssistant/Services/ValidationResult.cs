namespace FPVTrainingAssistant.Services;

public sealed class ValidationResult
{
    public ValidationResult(IEnumerable<ValidationIssue> issues)
    {
        Issues = Array.AsReadOnly(issues.ToArray());
        Errors = Array.AsReadOnly(Issues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray());
        Warnings = Array.AsReadOnly(Issues.Where(issue => issue.Severity == ValidationSeverity.Warning).ToArray());
    }

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public IReadOnlyList<ValidationIssue> Errors { get; }
    public IReadOnlyList<ValidationIssue> Warnings { get; }
}
