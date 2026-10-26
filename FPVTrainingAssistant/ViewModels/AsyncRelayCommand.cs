using System.Windows.Input;

namespace FPVTrainingAssistant.ViewModels;

public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onError = null) : ViewModelBase, ICommand
{
    private bool _isRunning;
    private Exception? _lastError;
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public Exception? LastError { get => _lastError; private set => SetProperty(ref _lastError, value); }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !IsRunning && (canExecute?.Invoke() ?? true);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    // async void is confined to the ICommand event boundary; execution catches failures.
    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        IsRunning = true;
        LastError = null;
        RaiseCanExecuteChanged();
        try { await execute(); }
        catch (Exception exception)
        {
            LastError = exception;
            onError?.Invoke(exception);
        }
        finally
        {
            IsRunning = false;
            RaiseCanExecuteChanged();
        }
    }
}
