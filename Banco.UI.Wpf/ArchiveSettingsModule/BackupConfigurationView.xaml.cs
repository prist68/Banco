using Banco.Backup.ViewModels;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Banco.UI.Wpf.ArchiveSettingsModule;

public partial class BackupConfigurationView : System.Windows.Controls.UserControl
{
    public BackupConfigurationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void SelectBackupDirectoryButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not BackupConfigurationViewModel viewModel)
        {
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Seleziona cartella destinazione backup",
            UseDescriptionForTitle = true,
            InitialDirectory = string.IsNullOrWhiteSpace(viewModel.DefaultBackupDirectory)
                ? @"C:\Facile Manager"
                : viewModel.DefaultBackupDirectory,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        viewModel.DefaultBackupDirectory = dialog.SelectedPath;
        viewModel.StatusMessage = $"Cartella backup selezionata: {dialog.SelectedPath}.";
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is BackupConfigurationViewModel previousViewModel)
        {
            previousViewModel.BackupCompleted -= OnBackupCompleted;
            previousViewModel.BackupFailed -= OnBackupFailed;
        }

        if (e.NewValue is BackupConfigurationViewModel currentViewModel)
        {
            currentViewModel.BackupCompleted += OnBackupCompleted;
            currentViewModel.BackupFailed += OnBackupFailed;
        }
    }

    private static void OnBackupCompleted(object? sender, string backupFilePath)
    {
        MessageBox.Show(
            $"Backup completato correttamente.\n\nFile creato:\n{backupFilePath}",
            "Backup completato",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void OnBackupFailed(object? sender, string errorMessage)
    {
        MessageBox.Show(
            $"La creazione del backup non e` andata a buon fine.\n\nDettaglio:\n{errorMessage}",
            "Errore backup",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
