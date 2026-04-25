using System.Windows;
using Banco.Backup;
using Microsoft.Win32;

namespace Banco.UI.Wpf.Services;

public sealed class BackupImportDialogService : IBackupDialogService
{
    public string? SelectBackupFilePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleziona backup gestionale",
            Filter = "Backup gestionale (*.zip;*.bak;*.sql)|*.zip;*.bak;*.sql|Archivio ZIP (*.zip)|*.zip|Dump SQL (*.bak;*.sql)|*.bak;*.sql",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public bool ConfirmRestoreImport()
    {
        var result = MessageBox.Show(
            "L'importazione sovrascrive il contenuto del db_diltech configurato in questa postazione.\n\nChiudi Banco e Facile Manager sulle altre postazioni prima di procedere.\n\nContinuare?",
            "Importa backup gestionale",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
