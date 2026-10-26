using FPVTrainingAssistant.Models;
using static FPVTrainingAssistant.Services.ReplayInterpolation;

namespace FPVTrainingAssistant.Services;

public sealed class ReplayEntityService
{
    private readonly ReplayEntityTrack _track;
    private readonly ReplayEntitySample[] _samples;
    private readonly double[] _times;

    public ReplayEntityService(ReplayEntityTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (!SessionValidator.IsValidEntityTrack(track, null))
            throw new ArgumentException("Entity replay requires a valid ordered track with finite transforms.", nameof(track));
        _track = track;
        _samples = track.Samples.ToArray();
        _times = _samples.Select(s => s.TimeSec).ToArray();
    }

    public EntityReplayState GetStateAtTime(double timeSec)
    {
        if (double.IsNaN(timeSec)) throw new ArgumentOutOfRangeException(nameof(timeSec));
        if (timeSec < _times[0]) return new(_track.EntityId, _track.EntityType, _track.ArchetypeId, null);
        var index = Array.BinarySearch(_times, timeSec);
        if (index >= 0) return State(_samples[index], _samples[index], 0, _times[index]);
        var next = ~index;
        if (next == _samples.Length) return State(_samples[^1], _samples[^1], 0, _times[^1]);
        var a = _samples[next - 1];
        var b = _samples[next];
        return State(a, b, (timeSec - a.TimeSec) / (b.TimeSec - a.TimeSec), timeSec);
    }

    private EntityReplayState State(ReplayEntitySample a, ReplayEntitySample b, double f, double time) =>
        new(_track.EntityId, _track.EntityType, _track.ArchetypeId, new ReplayEntitySample
        {
            TimeSec = time, X = Lerp(a.X, b.X, f), Y = Lerp(a.Y, b.Y, f), Z = Lerp(a.Z, b.Z, f),
            PitchDeg = Angle(a.PitchDeg, b.PitchDeg, f), RollDeg = Angle(a.RollDeg, b.RollDeg, f), YawDeg = Angle(a.YawDeg, b.YawDeg, f),
            IsActive = a.IsActive, State = a.State, Metadata = a.Metadata
        });
}
