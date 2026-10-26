using System.Globalization;
using System.Text.RegularExpressions;
using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.ViewModels;

internal static partial class DisplayText
{
    public static string Number(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    public static string Name(string value) => WordBoundary().Replace(value, " ");
    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}

public sealed class CriterionResultViewModel(EvaluationResult result, EvaluationCriterion? criterion)
{
    public string Name { get; } = DisplayText.Name(criterion?.Name ?? result.CriterionId);
    public double Score { get; } = result.Score;
    public string ScoreText { get; } = DisplayText.Number(result.Score) + " / 100";
    public string MeasuredValueText { get; } = result.MeasuredValue is { } value ? "Measured value: " + DisplayText.Number(value) : "Measured value: Not provided";
    public string Explanation { get; } = result.Explanation;
}

public sealed class MistakeViewModel(DetectedMistake mistake)
{
    public string Category { get; } = DisplayText.Name(mistake.Category);
    public string Description { get; } = mistake.Description;
    public string Evidence { get; } = mistake.Evidence;
    public string Context { get; } = string.Join("  ·  ", new[]
    {
        mistake.TimeSec is { } time ? DisplayText.Number(time) + " s" : null,
        mistake.RelatedEntityId
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class RecommendationViewModel(Recommendation recommendation)
{
    public int Priority { get; } = recommendation.Priority;
    public string PriorityLabel { get; } = recommendation.Priority switch { 1 => "High", 2 => "Medium", 3 => "Low", _ => "Unspecified" };
    public string Category { get; } = DisplayText.Name(recommendation.Category);
    public string Text { get; } = recommendation.Text;
    public string Evidence { get; } = recommendation.Evidence;
}

public sealed class EventViewModel(SessionEvent sessionEvent)
{
    public string TimeText { get; } = DisplayText.Number(sessionEvent.TimeSec) + " s";
    public string EventType { get; } = sessionEvent.EventType;
    public string EntityId { get; } = sessionEvent.EntityId ?? "Not specified";
    public string Details { get; } = string.Join(Environment.NewLine,
        (string.IsNullOrWhiteSpace(sessionEvent.Value) ? Enumerable.Empty<string>() : ["Value: " + sessionEvent.Value])
        .Concat(sessionEvent.Metadata?.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ": " + p.Value) ?? []));
}
