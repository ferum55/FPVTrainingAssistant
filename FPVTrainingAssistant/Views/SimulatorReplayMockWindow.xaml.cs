using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Views;

public partial class SimulatorReplayMockWindow : Window
{
    private readonly SimulatorReplayMockViewModel _viewModel;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1d / 30) };
    private readonly Stopwatch _clock = new();

    public SimulatorReplayMockWindow(SimulatorReplayMockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _timer.Tick += Tick;
        _viewModel.PropertyChanged += PlaybackChanged;
    }

    private void Tick(object? sender, EventArgs e)
    {
        var seconds = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        _viewModel.AdvanceBy(seconds);
    }

    private void PlaybackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SimulatorReplayMockViewModel.IsPlaying)) return;
        if (_viewModel.IsPlaying) { _clock.Restart(); _timer.Start(); }
        else { _timer.Stop(); _clock.Reset(); }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= Tick;
        _clock.Reset();
        _viewModel.PropertyChanged -= PlaybackChanged;
        _viewModel.Dispose();
        DataContext = null;
        base.OnClosed(e);
    }
}
