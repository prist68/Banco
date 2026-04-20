namespace Banco.Vendita.Abstractions;

public interface IGestionaleBackupImportService
{
    Task<GestionaleBackupImportResult> ImportAsync(
        string backupFilePath,
        IProgress<GestionaleBackupImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record GestionaleBackupImportProgress(
    string Stage,
    string Message,
    int Current,
    int Total);

public sealed record GestionaleBackupImportResult(
    string BackupFilePath,
    string ScriptFilePath,
    string DatabaseName,
    int ExecutedStatements);
