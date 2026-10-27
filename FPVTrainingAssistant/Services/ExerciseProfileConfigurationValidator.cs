using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.Services;

public static class ExerciseProfileConfigurationValidator
{
    public static IReadOnlyList<string> Validate(ExerciseProfile profile)
    {
        var errors = new List<string>();
        try { AnalysisProfileValidator.ValidateConfiguration(profile, requireActive:false); }
        catch (AnalysisConfigurationException e) { errors.Add(e.Message); }
        var sequences = new HashSet<int>();
        foreach (var point in profile.ReferencePoints)
        {
            if (point is null) { errors.Add("Reference points cannot be empty rows."); continue; }
            if (!sequences.Add(point.SequenceNo)) errors.Add("Reference-point sequence numbers must be unique.");
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z)) errors.Add("Reference-point coordinates must be finite.");
            if (string.IsNullOrWhiteSpace(point.PointType)) errors.Add("Each reference point requires a point type.");
        }
        // The analyzer also computes optional route metrics when two or more points are present.
        if(profile.ReferencePoints.Count>=2 && !profile.Criteria.Any(c=>c is not null && c.IsEnabled && c.RuleType==RuleTypes.RouteAdherence))
        {
            try { RouteAnalyzer.ValidateRoute(profile.ReferencePoints); }
            catch(AnalysisConfigurationException e) { errors.Add(e.Message); }
        }
        return errors.Distinct().ToArray();
    }
}
