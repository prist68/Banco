namespace Banco.Vendita.Abstractions;

public interface IGestionaleBackupExportService
{
    Task<GestionaleBackupExportResult> ExportAsync(
        GestionaleBackupExportRequest? request = null,
        IProgress<GestionaleBackupExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record GestionaleBackupExportRequest(
    string? DestinationDirectory = null,
    bool? IncludeDatabase = null,
    bool? IncludeAttachments = null,
    bool? IncludeImages = null,
    bool? IncludeLayouts = null,
    bool? IncludeReports = null);

public sealed record GestionaleBackupExportProgress(
    string Stage,
    string Message,
    int Current,
    int Total);

public sealed record GestionaleBackupExportResult(
    string BackupFilePath,
    string DatabaseName,
    string? ScriptEntryName,
    IReadOnlyList<string> IncludedEntries,
    IReadOnlyList<string> SkippedEntries,
    DateTime CreatedAt);
