using System.Windows;

namespace FPVTrainingAssistant;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var connections = new Data.DatabaseConnectionFactory(new Data.ApplicationDataPathProvider());
        var viewModel = new ViewModels.MainViewModel(new Services.FileDialogService(), new Services.SessionImportService(),
            new Services.ExerciseProfileProvider(), new Services.SessionAnalyzer(),
            new Services.MockSimulatorReplayLauncher(new Views.ReplayMockWindowService()), new Data.SessionRepository(connections),
            new Data.HistoryRepository(connections), new Data.DatabaseInitializer(connections));
        MainWindow = new Views.MainWindow { DataContext = viewModel };
        MainWindow.Show();
        await viewModel.InitializePersistenceAsync();
    }
}
