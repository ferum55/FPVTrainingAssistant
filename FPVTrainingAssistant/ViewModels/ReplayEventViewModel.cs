using FPVTrainingAssistant.Models;

namespace FPVTrainingAssistant.ViewModels;

public sealed class ReplayEventViewModel(SessionEvent sessionEvent) : ViewModelBase
{
    private bool _hasOccurred, _isCurrent;
    public double TimeSec => sessionEvent.TimeSec;
    public string EventId => sessionEvent.EventId;
    public EventViewModel Display { get; } = new(sessionEvent);
    public bool HasOccurred { get => _hasOccurred; private set => SetProperty(ref _hasOccurred, value); }
    public bool IsCurrent { get => _isCurrent; private set => SetProperty(ref _isCurrent, value); }
    public string ProgressText => IsCurrent ? "Latest" : HasOccurred ? "Occurred" : "Upcoming";

    internal void Update(double time, bool latest)
    {
        var changed = HasOccurred != (TimeSec <= time) || IsCurrent != latest;
        HasOccurred = TimeSec <= time;
        IsCurrent = latest;
        if (changed) OnPropertyChanged(nameof(ProgressText));
    }
}
