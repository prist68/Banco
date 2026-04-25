namespace Banco.Backup;

public interface IBackupDialogService
{
    string? SelectBackupFilePath();

    bool ConfirmRestoreImport();
}
