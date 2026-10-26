using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace FPVTrainingAssistant.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? ChooseSessionFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import training session",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        var samples = Path.Combine(AppContext.BaseDirectory, "TestSessions");
        if (Directory.Exists(samples)) dialog.InitialDirectory = samples;
        var selected = Application.Current?.MainWindow is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return selected == true ? dialog.FileName : null;
    }
}
