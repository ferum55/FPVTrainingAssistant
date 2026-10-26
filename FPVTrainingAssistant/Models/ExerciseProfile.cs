namespace FPVTrainingAssistant.Models;

public sealed class ExerciseProfile
{
    public required string ProfileId
    {
        get;
        init => field = ModelValue.NonEmpty(value, nameof(ProfileId));
    }

    public required string Name { get; init; }
    public required string ExerciseType { get; init; }
    public int Version { get; init; } = 1;
    public bool IsActive { get; init; } = true;

    public List<ReferencePoint> ReferencePoints
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(ReferencePoints));
    } = [];

    public List<EvaluationCriterion> Criteria
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(Criteria));
    } = [];
}
