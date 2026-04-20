using System.IO.Compression;
using System.Text;
using Banco.Vendita.Abstractions;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleBackupImportService : IGestionaleBackupImportService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleBackupImportService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<GestionaleBackupImportResult> ImportAsync(
        string backupFilePath,
        IProgress<GestionaleBackupImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            throw new InvalidOperationException("Selezionare un file di backup prima di avviare l'importazione.");
        }

        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Il file di backup selezionato non esiste.", backupFilePath);
        }

        var settings = await _configurationService.LoadAsync(cancellationToken);
        var databaseName = settings.GestionaleDatabase.Database?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("La configurazione DB non contiene un database valido.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "BancoBackupImport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            progress?.Report(new GestionaleBackupImportProgress("Preparazione", "Analisi del backup in corso...", 0, 1));
            var scriptPath = await ResolveScriptPathAsync(backupFilePath, databaseName, tempDirectory, cancellationToken);
            var scriptText = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8, cancellationToken);
            if (string.IsNullOrWhiteSpace(scriptText))
            {
                throw new InvalidOperationException("Il backup selezionato non contiene istruzioni SQL importabili.");
            }

            progress?.Report(new GestionaleBackupImportProgress("Parsing", "Lettura dello script SQL e preparazione statement...", 0, 1));
            var statements = EnumerateStatements(scriptText).ToList();
            if (statements.Count == 0)
            {
                throw new InvalidOperationException("Il dump SQL non contiene statement eseguibili.");
            }

            var builder = GestionaleConnectionFactory.CreateConnectionStringBuilder(settings.GestionaleDatabase);
            builder.Database = string.Empty;
            builder.AllowUserVariables = true;
            builder.AllowLoadLocalInfile = true;
            builder.DefaultCommandTimeout = 600;

            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var executedStatements = 0;
            progress?.Report(new GestionaleBackupImportProgress("Import", $"Importazione avviata: 0 / {statements.Count:N0} statement.", 0, statements.Count));
            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var command = connection.CreateCommand();
                command.CommandTimeout = 600;
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync(cancellationToken);
                executedStatements++;
                if (executedStatements == 1 || executedStatements == statements.Count || executedStatements % 25 == 0)
                {
                    progress?.Report(new GestionaleBackupImportProgress(
                        "Import",
                        $"Importazione in corso: {executedStatements:N0} / {statements.Count:N0} statement.",
                        executedStatements,
                        statements.Count));
                }
            }

            progress?.Report(new GestionaleBackupImportProgress(
                "Completato",
                $"Ripristino completato: {executedStatements:N0} statement eseguiti.",
                executedStatements,
                statements.Count));

            return new GestionaleBackupImportResult(
                backupFilePath,
                scriptPath,
                databaseName,
                executedStatements);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Se la pulizia temporanea fallisce non deve compromettere l'esito del restore.
            }
        }
    }

    private static async Task<string> ResolveScriptPathAsync(
        string backupFilePath,
        string databaseName,
        string tempDirectory,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(backupFilePath);
        if (extension.Equals(".sql", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bak", StringComparison.OrdinalIgnoreCase))
        {
            return backupFilePath;
        }

        if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sono supportati solo backup .zip, .bak o .sql.");
        }

        using var archive = ZipFile.OpenRead(backupFilePath);
        var scriptEntry = archive.Entries
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Name) &&
                (entry.FullName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                 entry.FullName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(entry => Path.GetFileNameWithoutExtension(entry.Name).Contains(databaseName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(entry => entry.Length)
            .FirstOrDefault();

        if (scriptEntry is null)
        {
            throw new InvalidOperationException("Lo zip selezionato non contiene un dump SQL utilizzabile.");
        }

        var extractedPath = Path.Combine(tempDirectory, scriptEntry.Name);
        await using var source = scriptEntry.Open();
        await using var target = File.Create(extractedPath);
        await source.CopyToAsync(target, cancellationToken);
        return extractedPath;
    }

    private static IEnumerable<string> EnumerateStatements(string scriptText)
    {
        using var reader = new StringReader(scriptText);
        var builder = new StringBuilder();
        var delimiter = ";";
        var inBlockComment = false;

        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (inBlockComment)
            {
                if (trimmed.Contains("*/", StringComparison.Ordinal))
                {
                    inBlockComment = false;
                }

                continue;
            }

            if (trimmed.StartsWith("/*!", StringComparison.Ordinal))
            {
                builder.AppendLine(line);
                if (!trimmed.EndsWith(delimiter, StringComparison.Ordinal))
                {
                    continue;
                }

                var conditionalStatement = builder.ToString().Trim();
                builder.Clear();
                conditionalStatement = TrimTerminalDelimiter(conditionalStatement, delimiter);

                if (!string.IsNullOrWhiteSpace(conditionalStatement))
                {
                    yield return conditionalStatement;
                }

                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                if (!trimmed.Contains("*/", StringComparison.Ordinal))
                {
                    inBlockComment = true;
                }

                continue;
            }

            if (trimmed.StartsWith("-- ", StringComparison.Ordinal) || trimmed == "--")
            {
                continue;
            }

            if (trimmed.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
            {
                delimiter = trimmed["DELIMITER ".Length..].Trim();
                continue;
            }

            builder.AppendLine(line);
            if (!trimmed.EndsWith(delimiter, StringComparison.Ordinal))
            {
                continue;
            }

            var statement = builder.ToString().Trim();
            builder.Clear();
            statement = TrimTerminalDelimiter(statement, delimiter);

            if (!string.IsNullOrWhiteSpace(statement))
            {
                yield return statement;
            }
        }

        var tail = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
        {
            yield return tail;
        }
    }

    private static string TrimTerminalDelimiter(string statement, string delimiter)
    {
        if (string.IsNullOrWhiteSpace(delimiter))
        {
            return statement;
        }

        return statement.EndsWith(delimiter, StringComparison.Ordinal)
            ? statement[..^delimiter.Length].TrimEnd()
            : statement;
    }
}
