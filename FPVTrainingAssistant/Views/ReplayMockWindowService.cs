using System.Windows;
using FPVTrainingAssistant.Services;
using FPVTrainingAssistant.ViewModels;

namespace FPVTrainingAssistant.Views;

public sealed class ReplayMockWindowService : IReplayMockWindowService
{
    private SimulatorReplayMockWindow? _window;

    public void Show(ReplayLaunchRequest request)
    {
        if (_window?.DataContext is SimulatorReplayMockViewModel current && ReferenceEquals(current.Session, request.Session))
        {
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
            return;
        }
        _window?.Close();
        var viewModel = new SimulatorReplayMockViewModel(request.Session, request.Profile, request.Analysis);
        if (!viewModel.IsAvailable) { viewModel.Dispose(); throw new InvalidOperationException(viewModel.UnavailableMessage); }
        SimulatorReplayMockWindow? window = null;
        try
        {
            window = new SimulatorReplayMockWindow(viewModel) { Owner = Application.Current.MainWindow };
            window.Closed += (_, _) => { if (ReferenceEquals(_window, window)) _window = null; };
            _window = window;
            window.Show();
        }
        catch
        {
            window?.Close();
            viewModel.Dispose();
            _window = null;
            throw;
        }
    }
}
