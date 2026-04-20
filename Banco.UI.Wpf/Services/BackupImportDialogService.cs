using Microsoft.Win32;

namespace Banco.UI.Wpf.Services;

public sealed class BackupImportDialogService
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
}
