using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleBackupExportService : IGestionaleBackupExportService
{
    private static readonly Regex DefinerRegex = new(
        @"\sDEFINER=`[^`]+`@`[^`]+`",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;

    public GestionaleBackupExportService(
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _logService = logService;
    }

    public async Task<GestionaleBackupExportResult> ExportAsync(
        GestionaleBackupExportRequest? request = null,
        IProgress<GestionaleBackupExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        var databaseName = settings.GestionaleDatabase.Database?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("La configurazione DB non contiene un database valido.");
        }

        var includeDatabase = request?.IncludeDatabase ?? settings.BackupConfiguration.IncludeDatabase;
        var includeAttachments = request?.IncludeAttachments ?? settings.BackupConfiguration.IncludeAttachments;
        var includeImages = request?.IncludeImages ?? settings.BackupConfiguration.IncludeImages;
        var includeLayouts = request?.IncludeLayouts ?? settings.BackupConfiguration.IncludeLayouts;
        var includeReports = request?.IncludeReports ?? settings.BackupConfiguration.IncludeReports;

        if (!includeDatabase && !includeAttachments && !includeImages && !includeLayouts && !includeReports)
        {
            throw new InvalidOperationException("Selezionare almeno un contenuto da includere nel backup.");
        }

        var destinationDirectory = string.IsNullOrWhiteSpace(request?.DestinationDirectory)
            ? settings.BackupConfiguration.DefaultBackupDirectory
            : request!.DestinationDirectory!;
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            destinationDirectory = @"C:\Facile Manager";
        }

        Directory.CreateDirectory(destinationDirectory);

        var archiveStem = BuildArchiveStem(databaseName);
        var createdAt = DateTime.Now;
        var zipFilePath = BuildBackupFilePath(
            destinationDirectory,
            archiveStem,
            createdAt,
            settings.BackupConfiguration.UseDifferentialBackupNaming);
        var scriptEntryName = includeDatabase
            ? $"{Path.GetFileNameWithoutExtension(zipFilePath)}.bak"
            : null;

        var tempDirectory = Path.Combine(Path.GetTempPath(), "BancoBackupExport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var includedEntries = new List<string>();
        var skippedEntries = new List<string>();

        try
        {
            _logService.Info(
                nameof(GestionaleBackupExportService),
                $"Avvio backup. Destinazione={destinationDirectory}, File={zipFilePath}, Database={databaseName}, IncludeDb={includeDatabase}, Allegati={includeAttachments}, Immagini={includeImages}, Layout={includeLayouts}, Report={includeReports}, PuliziaStorici={settings.BackupConfiguration.DeleteOldBackupsEnabled}, GiorniRetention={settings.BackupConfiguration.DeleteOldBackupsAfterDays}.");

            var exportFolders = ResolveExportFolders(settings, includeAttachments, includeImages, includeLayouts, includeReports);
            var totalPhases = 1 + exportFolders.Count + (includeDatabase ? 1 : 0);
            var completedPhases = 0;

            string? scriptPath = null;
            if (includeDatabase)
            {
                progress?.Report(new GestionaleBackupExportProgress(
                    "Dump",
                    "Preparazione dump SQL del gestionale in corso...",
                    completedPhases,
                    totalPhases));

                scriptPath = Path.Combine(tempDirectory, scriptEntryName!);
                await WriteDatabaseDumpAsync(settings, databaseName, scriptPath, progress, cancellationToken);
                completedPhases++;
                includedEntries.Add(scriptEntryName!);
            }

            progress?.Report(new GestionaleBackupExportProgress(
                "Archivio",
                "Creazione archivio backup in corso...",
                completedPhases,
                totalPhases));

            using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                if (includeDatabase && scriptPath is not null)
                {
                    archive.CreateEntryFromFile(scriptPath, scriptEntryName!, CompressionLevel.Optimal);
                }

                foreach (var folder in exportFolders)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!Directory.Exists(folder.SourceDirectory))
                    {
                        skippedEntries.Add($"{folder.EntryRoot} (cartella non trovata: {folder.SourceDirectory})");
                        completedPhases++;
                        continue;
                    }

                    progress?.Report(new GestionaleBackupExportProgress(
                        "Archivio",
                        $"Aggiunta contenuto {folder.EntryRoot} in corso...",
                        completedPhases,
                        totalPhases));

                    var added = AddDirectoryToArchive(archive, folder.SourceDirectory, folder.EntryRoot);
                    if (added == 0)
                    {
                        skippedEntries.Add($"{folder.EntryRoot} (cartella vuota: {folder.SourceDirectory})");
                    }
                    else
                    {
                        includedEntries.Add($"{folder.EntryRoot}/ ({added} file)");
                    }

                    completedPhases++;
                }
            }

            if (settings.BackupConfiguration.DeleteOldBackupsEnabled)
            {
                progress?.Report(new GestionaleBackupExportProgress(
                    "Pulizia",
                    $"Pulizia backup piu` vecchi di {settings.BackupConfiguration.DeleteOldBackupsAfterDays} giorni...",
                    totalPhases - 1,
                    totalPhases));

                DeleteOldBackups(
                    destinationDirectory,
                    archiveStem,
                    settings.BackupConfiguration.DeleteOldBackupsAfterDays <= 0 ? 30 : settings.BackupConfiguration.DeleteOldBackupsAfterDays,
                    zipFilePath,
                    skippedEntries);
            }

            var updatedSettings = await _configurationService.LoadAsync(cancellationToken);
            updatedSettings.BackupConfiguration.LastBackupAt = createdAt;
            await _configurationService.SaveAsync(updatedSettings, cancellationToken);

            progress?.Report(new GestionaleBackupExportProgress(
                "Completato",
                $"Backup creato correttamente in {zipFilePath}.",
                totalPhases,
                totalPhases));

            _logService.Info(
                nameof(GestionaleBackupExportService),
                $"Backup creato. File={zipFilePath}, Database={databaseName}, IncludeDb={includeDatabase}, Allegati={includeAttachments}, Immagini={includeImages}, Layout={includeLayouts}, Report={includeReports}, Inclusi={FormatEntries(includedEntries)}, Note={FormatEntries(skippedEntries)}.");

            return new GestionaleBackupExportResult(
                zipFilePath,
                databaseName,
                scriptEntryName,
                includedEntries,
                skippedEntries,
                createdAt);
        }
        catch (Exception ex)
        {
            _logService.Error(
                nameof(GestionaleBackupExportService),
                $"Backup non completato. Destinazione={destinationDirectory}, FileAtteso={zipFilePath}, Database={databaseName}, InclusiParziali={FormatEntries(includedEntries)}, Note={FormatEntries(skippedEntries)}.",
                ex);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch
            {
                // La pulizia temporanea non deve compromettere l'esito del backup.
            }
        }
    }

    private static async Task WriteDatabaseDumpAsync(
        AppSettings settings,
        string databaseName,
        string outputPath,
        IProgress<GestionaleBackupExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var connection = await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
        var metadata = await LoadMetadataAsync(connection, cancellationToken);
        var serverVersion = await GetServerVersionAsync(connection, cancellationToken);
        var charset = string.IsNullOrWhiteSpace(settings.GestionaleDatabase.CharacterSet)
            ? "latin1"
            : settings.GestionaleDatabase.CharacterSet.Trim();

        await using var stream = File.Create(outputPath);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("-- MySqlBackup.NET 2.0.4");
        await writer.WriteLineAsync($"-- Dump Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await writer.WriteLineAsync("-- --------------------------------------");
        await writer.WriteLineAsync($"-- Server version {serverVersion}");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("-- ");
        await writer.WriteLineAsync($"-- Create schema {databaseName}");
        await writer.WriteLineAsync("-- ");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"CREATE DATABASE IF NOT EXISTS `{databaseName}` /*!40100 DEFAULT CHARACTER SET {charset} */;");
        await writer.WriteLineAsync($"Use `{databaseName}`;");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;");
        await writer.WriteLineAsync("/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;");
        await writer.WriteLineAsync("/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;");
        await writer.WriteLineAsync($"/*!40101 SET NAMES {charset} */;");
        await writer.WriteLineAsync("/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;");
        await writer.WriteLineAsync("/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;");
        await writer.WriteLineAsync("/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;");
        await writer.WriteLineAsync("/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync();

        var totalObjects = metadata.Tables.Count + metadata.Views.Count + metadata.Triggers.Count + metadata.Routines.Count;
        var currentObject = 0;

        foreach (var tableName in metadata.Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentObject++;
            progress?.Report(new GestionaleBackupExportProgress(
                "Dump",
                $"Esportazione tabella {tableName} ({currentObject}/{totalObjects})...",
                currentObject,
                totalObjects));

            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync($"-- Definition of {tableName}");
            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync();
            var createTableSql = await GetCreateStatementAsync(connection, "TABLE", tableName, cancellationToken);
            await writer.WriteLineAsync($"DROP TABLE IF EXISTS `{tableName}`;");
            await writer.WriteLineAsync($"{EnsureCreateIfNotExists(createTableSql, "TABLE")};");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync($"-- Dumping data for table {tableName}");
            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"/*!40000 ALTER TABLE `{tableName}` DISABLE KEYS */;");
            await WriteTableDataAsync(connection, tableName, writer, cancellationToken);
            await writer.WriteLineAsync($"/*!40000 ALTER TABLE `{tableName}` ENABLE KEYS */;");
            await writer.WriteLineAsync();
        }

        foreach (var viewName in metadata.Views)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentObject++;
            progress?.Report(new GestionaleBackupExportProgress(
                "Dump",
                $"Esportazione vista {viewName} ({currentObject}/{totalObjects})...",
                currentObject,
                totalObjects));

            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync($"-- Definition of {viewName}");
            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync();
            var createViewSql = NormalizeDefiner(await GetCreateStatementAsync(connection, "VIEW", viewName, cancellationToken));
            await writer.WriteLineAsync($"DROP VIEW IF EXISTS `{viewName}`;");
            await writer.WriteLineAsync($"{EnsureCreateIfNotExists(createViewSql, "VIEW")};");
            await writer.WriteLineAsync();
        }

        foreach (var triggerName in metadata.Triggers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentObject++;
            progress?.Report(new GestionaleBackupExportProgress(
                "Dump",
                $"Esportazione trigger {triggerName} ({currentObject}/{totalObjects})...",
                currentObject,
                totalObjects));

            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync($"-- Definition of {triggerName}");
            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync();
            var createTriggerSql = NormalizeDefiner(await GetCreateStatementAsync(connection, "TRIGGER", triggerName, cancellationToken));
            await writer.WriteLineAsync("DELIMITER $$");
            await writer.WriteLineAsync($"DROP TRIGGER IF EXISTS `{triggerName}`$$");
            await writer.WriteLineAsync($"{createTriggerSql}$$");
            await writer.WriteLineAsync("DELIMITER ;");
            await writer.WriteLineAsync();
        }

        foreach (var routine in metadata.Routines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentObject++;
            progress?.Report(new GestionaleBackupExportProgress(
                "Dump",
                $"Esportazione routine {routine.Name} ({currentObject}/{totalObjects})...",
                currentObject,
                totalObjects));

            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync($"-- Definition of {routine.Name}");
            await writer.WriteLineAsync("-- ");
            await writer.WriteLineAsync();
            var createRoutineSql = NormalizeDefiner(await GetCreateStatementAsync(connection, routine.Type, routine.Name, cancellationToken));
            await writer.WriteLineAsync("DELIMITER $$");
            await writer.WriteLineAsync($"DROP {routine.Type} IF EXISTS `{routine.Name}`$$");
            await writer.WriteLineAsync($"{createRoutineSql}$$");
            await writer.WriteLineAsync("DELIMITER ;");
            await writer.WriteLineAsync();
        }

        await writer.WriteLineAsync("/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;");
        await writer.WriteLineAsync("/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;");
        await writer.WriteLineAsync("/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;");
        await writer.WriteLineAsync("/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;");
        await writer.WriteLineAsync("/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;");
        await writer.WriteLineAsync("/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;");
        await writer.WriteLineAsync("/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;");
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task<string> GetServerVersionAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT VERSION();";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "sconosciuta";
    }

    private static async Task<BackupMetadata> LoadMetadataAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        var views = new List<string>();
        var triggers = new List<string>();
        var routines = new List<DatabaseRoutine>();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT TABLE_NAME, TABLE_TYPE
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                ORDER BY TABLE_TYPE, TABLE_NAME;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var tableName = reader.GetString(0);
                var tableType = reader.GetString(1);
                if (string.Equals(tableType, "BASE TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    tables.Add(tableName);
                }
                else if (string.Equals(tableType, "VIEW", StringComparison.OrdinalIgnoreCase))
                {
                    views.Add(tableName);
                }
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT TRIGGER_NAME
                FROM information_schema.TRIGGERS
                WHERE TRIGGER_SCHEMA = DATABASE()
                ORDER BY TRIGGER_NAME;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                triggers.Add(reader.GetString(0));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT ROUTINE_NAME, ROUTINE_TYPE
                FROM information_schema.ROUTINES
                WHERE ROUTINE_SCHEMA = DATABASE()
                ORDER BY ROUTINE_TYPE, ROUTINE_NAME;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                routines.Add(new DatabaseRoutine(reader.GetString(0), reader.GetString(1).ToUpperInvariant()));
            }
        }

        return new BackupMetadata(tables, views, triggers, routines);
    }

    private static async Task<string> GetCreateStatementAsync(
        MySqlConnection connection,
        string objectType,
        string objectName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = objectType switch
        {
            "TABLE" => $"SHOW CREATE TABLE `{objectName}`;",
            "VIEW" => $"SHOW CREATE VIEW `{objectName}`;",
            "TRIGGER" => $"SHOW CREATE TRIGGER `{objectName}`;",
            "PROCEDURE" => $"SHOW CREATE PROCEDURE `{objectName}`;",
            "FUNCTION" => $"SHOW CREATE FUNCTION `{objectName}`;",
            _ => throw new InvalidOperationException($"Tipo oggetto non supportato: {objectType}.")
        };

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Impossibile leggere la definizione SQL di {objectType} {objectName}.");
        }

        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            var columnName = reader.GetName(ordinal);
            if (columnName.StartsWith("Create ", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(columnName, "SQL Original Statement", StringComparison.OrdinalIgnoreCase))
            {
                return reader.GetString(ordinal);
            }
        }

        throw new InvalidOperationException($"Definizione SQL non trovata per {objectType} {objectName}.");
    }

    private static async Task WriteTableDataAsync(
        MySqlConnection connection,
        string tableName,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM `{tableName}`;";

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        if (reader.FieldCount == 0)
        {
            return;
        }

        var columnList = string.Join(", ", Enumerable.Range(0, reader.FieldCount).Select(index => $"`{reader.GetName(index)}`"));
        const int batchSize = 150;
        var rowIndex = 0;
        var batch = new List<string>(batchSize);

        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                values[i] = SerializeValue(reader, i);
            }

            batch.Add($"({string.Join(", ", values)})");
            rowIndex++;

            if (batch.Count >= batchSize)
            {
                await writer.WriteLineAsync($"INSERT INTO `{tableName}` ({columnList}) VALUES");
                await writer.WriteLineAsync($"{string.Join("," + Environment.NewLine, batch)};");
                await writer.WriteLineAsync();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await writer.WriteLineAsync($"INSERT INTO `{tableName}` ({columnList}) VALUES");
            await writer.WriteLineAsync($"{string.Join("," + Environment.NewLine, batch)};");
            await writer.WriteLineAsync();
        }
    }

    private static string SerializeValue(IDataRecord record, int ordinal)
    {
        if (record.IsDBNull(ordinal))
        {
            return "NULL";
        }

        var value = record.GetValue(ordinal);
        return value switch
        {
            byte[] bytes => $"0x{Convert.ToHexString(bytes)}",
            string text => $"'{EscapeSqlString(text)}'",
            char character => $"'{EscapeSqlString(character.ToString())}'",
            bool boolean => boolean ? "1" : "0",
            sbyte or byte or short or ushort or int or uint or long or ulong
                => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            float or double or decimal
                => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            DateTime dateTime => $"'{dateTime:yyyy-MM-dd HH:mm:ss.ffffff}'",
            DateOnly dateOnly => $"'{dateOnly:yyyy-MM-dd}'",
            TimeOnly timeOnly => $"'{timeOnly:HH:mm:ss.ffffff}'",
            TimeSpan timeSpan => $"'{timeSpan:hh\\:mm\\:ss}'",
            _ => $"'{EscapeSqlString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)}'"
        };
    }

    private static string EscapeSqlString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\0", "\\0", StringComparison.Ordinal);
    }

    private static string NormalizeDefiner(string sql)
    {
        return DefinerRegex.Replace(sql, string.Empty);
    }

    private static string EnsureCreateIfNotExists(string sql, string objectType)
    {
        var marker = $"CREATE {objectType}";
        var markerIfNotExists = $"CREATE {objectType} IF NOT EXISTS";

        return sql.Contains(markerIfNotExists, StringComparison.OrdinalIgnoreCase)
            ? sql
            : sql.Replace(marker, markerIfNotExists, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBackupFilePath(
        string destinationDirectory,
        string archiveStem,
        DateTime createdAt,
        bool useDifferentialNaming)
    {
        var baseFileName = useDifferentialNaming
            ? $"{archiveStem}_{createdAt:dd-MM-yyyy}"
            : archiveStem;
        var candidate = Path.Combine(destinationDirectory, $"{baseFileName}.zip");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        return Path.Combine(destinationDirectory, $"{baseFileName}_{createdAt:HH-mm-ss}.zip");
    }

    private static string BuildArchiveStem(string databaseName)
    {
        var parts = databaseName
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return databaseName;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            parts[i] = Capitalize(parts[i]);
        }

        return string.Join("_", parts);
    }

    private static string Capitalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    private static List<ExportFolder> ResolveExportFolders(
        AppSettings settings,
        bool includeAttachments,
        bool includeImages,
        bool includeLayouts,
        bool includeReports)
    {
        var folders = new List<ExportFolder>();
        var root = settings.FmContent.RootDirectory?.Trim() ?? string.Empty;
        var imageDirectory = string.IsNullOrWhiteSpace(settings.FmContent.ArticleImagesDirectory)
            ? Path.Combine(root, "Immagini")
            : settings.FmContent.ArticleImagesDirectory.Trim();

        if (includeAttachments)
        {
            folders.Add(new ExportFolder(Path.Combine(root, "Archidoc"), "Archidoc"));
        }

        if (includeImages)
        {
            var entryRoot = BuildEntryRoot(imageDirectory, root, "Immagini");
            folders.Add(new ExportFolder(imageDirectory, entryRoot));
        }

        if (includeLayouts)
        {
            folders.Add(new ExportFolder(Path.Combine(root, "Layout"), "Layout"));
        }

        if (includeReports)
        {
            folders.Add(new ExportFolder(Path.Combine(root, "Report"), "Report"));
        }

        return folders;
    }

    private static string BuildEntryRoot(string sourceDirectory, string rootDirectory, string fallbackRoot)
    {
        if (!string.IsNullOrWhiteSpace(rootDirectory) && sourceDirectory.StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(rootDirectory, sourceDirectory);
            if (!string.IsNullOrWhiteSpace(relative) && relative != ".")
            {
                return relative.Replace('\\', '/');
            }
        }

        return fallbackRoot.Replace('\\', '/');
    }

    private static int AddDirectoryToArchive(
        ZipArchive archive,
        string sourceDirectory,
        string entryRoot)
    {
        var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath).Replace('\\', '/');
            archive.CreateEntryFromFile(filePath, $"{entryRoot}/{relativePath}", CompressionLevel.Optimal);
        }

        return files.Length;
    }

    private static void DeleteOldBackups(
        string destinationDirectory,
        string archiveStem,
        int retentionDays,
        string currentBackupFilePath,
        ICollection<string> skippedEntries)
    {
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        var pattern = $"{archiveStem}*.zip";
        var files = Directory.GetFiles(destinationDirectory, pattern, SearchOption.TopDirectoryOnly);

        foreach (var filePath in files)
        {
            if (string.Equals(filePath, currentBackupFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.LastWriteTime > cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(filePath);
                skippedEntries.Add($"Eliminato backup storico: {fileInfo.Name}");
            }
            catch (Exception ex)
            {
                skippedEntries.Add($"Pulizia non riuscita per {fileInfo.Name}: {ex.Message}");
            }
        }
    }

    private static string FormatEntries(IEnumerable<string> entries)
    {
        var values = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToList();

        return values.Count == 0
            ? "nessuna"
            : string.Join(" | ", values);
    }

    private sealed record BackupMetadata(
        IReadOnlyList<string> Tables,
        IReadOnlyList<string> Views,
        IReadOnlyList<string> Triggers,
        IReadOnlyList<DatabaseRoutine> Routines);

    private sealed record DatabaseRoutine(string Name, string Type);

    private sealed record ExportFolder(string SourceDirectory, string EntryRoot);
}
