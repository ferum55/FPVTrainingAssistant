using System.Windows;

namespace FPVTrainingAssistant;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var connections = new Data.DatabaseConnectionFactory(new Data.ApplicationDataPathProvider());
        var profileRepository=new Data.ExerciseProfileRepository(connections);
        var preferences=new Data.ApplicationSettingsService(connections);
        var viewModel = new ViewModels.MainViewModel(new Services.FileDialogService(), new Services.SessionImportService(),
            new Services.ExerciseProfileProvider(profileRepository), new Services.SessionAnalyzer(),
            new Services.MockSimulatorReplayLauncher(new Views.ReplayMockWindowService(preferences)), new Data.SessionRepository(connections),
            new Data.HistoryRepository(connections), new Data.DatabaseInitializer(connections),profileRepository,preferences);
        MainWindow = new Views.MainWindow { DataContext = viewModel };
        MainWindow.Show();
        await viewModel.InitializePersistenceAsync();
    }
}
