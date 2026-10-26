using System.Windows;

namespace FPVTrainingAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var viewModel = new ViewModels.MainViewModel(new Services.FileDialogService(), new Services.SessionImportService(),
            new Services.ExerciseProfileProvider(), new Services.SessionAnalyzer());
        MainWindow = new Views.MainWindow { DataContext = viewModel };
        MainWindow.Show();
    }
}
