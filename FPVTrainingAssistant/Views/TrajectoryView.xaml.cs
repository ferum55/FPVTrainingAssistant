using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Views;

public partial class TrajectoryView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1d / 30) };
    private readonly Stopwatch _clock = new();
    private TrajectoryViewModel? _viewModel;

    public TrajectoryView()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach(DataContext as TrajectoryViewModel);
        Unloaded += (_, _) => Attach(null);
        DataContextChanged += (_, _) => { if (IsLoaded) Attach(DataContext as TrajectoryViewModel); };
        _timer.Tick += (_, _) =>
        {
            var elapsed = _clock.Elapsed.TotalSeconds;
            _clock.Restart();
            _viewModel?.AdvanceBy(elapsed);
        };
    }

    private void Attach(TrajectoryViewModel? viewModel)
    {
        _timer.Stop(); _clock.Reset();
        if (_viewModel is not null) { _viewModel.PropertyChanged -= PlaybackChanged; _viewModel.Pause(); }
        _viewModel = viewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += PlaybackChanged;
        UpdateClock();
    }

    private void PlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrajectoryViewModel.IsPlaying)) UpdateClock();
    }

    private void UpdateClock()
    {
        if (_viewModel?.IsPlaying == true) { _clock.Restart(); _timer.Start(); }
        else { _timer.Stop(); _clock.Reset(); }
    }
}
