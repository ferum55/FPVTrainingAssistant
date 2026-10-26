using FPVTrainingAssistant.Models;
using FPVTrainingAssistant.Services;

namespace FPVTrainingAssistant.ViewModels;

public sealed record ReplayMinimapFrame(ReplayState? Drone, IReadOnlyList<EntityReplayState> Entities);
public sealed record MinimapEntityMarker(string EntityId, Point2D Position);
public sealed record ProjectedMinimapFrame(Point2D? Drone, IReadOnlyList<MinimapEntityMarker> Entities);

public sealed class ReplayMinimapViewModel
{
    private readonly IReadOnlyList<Point2D> _bounds;
    private TrajectoryProjection? _projection;
    private double _width, _height;

    public ReplayMinimapViewModel(TrainingSession session, ExerciseProfile profile)
    {
        Trajectory = Array.AsReadOnly(session.Telemetry.Select(s => new Point2D(s.X, s.Y)).ToArray());
        Route = Array.AsReadOnly(profile.ReferencePoints.OrderBy(p => p.SequenceNo).Select(p => new Point2D(p.X, p.Y)).ToArray());
        _bounds = Route.Concat(session.Replay?.Entities.SelectMany(t => t.Samples).Select(s => new Point2D(s.X, s.Y)) ?? []).ToArray();
    }

    public IReadOnlyList<Point2D> Trajectory { get; }
    public IReadOnlyList<Point2D> Route { get; }
    public bool HasRoute => Route.Count >= 2;

    public TrajectoryProjection Projection(double width, double height)
    {
        if (_projection is null || width != _width || height != _height)
        {
            _projection = new TrajectoryProjectionService().Create(Trajectory, _bounds, width, height, 28);
            _width = width; _height = height;
        }
        return _projection;
    }

    public ProjectedMinimapFrame Project(ReplayMinimapFrame frame, double width, double height)
    {
        var projection = Projection(width, height);
        return new(frame.Drone is { } drone ? projection.Project(new(drone.X, drone.Y)) : null,
            frame.Entities.Where(e => e.IsActive).Select(e => new MinimapEntityMarker(e.EntityId,
                projection.Project(new(e.Transform!.X, e.Transform.Y)))).ToArray());
    }
}
