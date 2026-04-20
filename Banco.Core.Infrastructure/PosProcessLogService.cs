using System.Globalization;
using System.Text;
using Banco.Vendita.Abstractions;

namespace Banco.Core.Infrastructure;

public sealed class PosProcessLogService : IPosProcessLogService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly string _logDirectoryPath;

    public string LogFilePath { get; }

    public PosProcessLogService()
    {
        _logDirectoryPath = Path.Combine(AppContext.BaseDirectory, "Log");
        LogFilePath = Path.Combine(_logDirectoryPath, "Avvio.log");
    }

    public void Info(string source, string message) => Write("INFO", source, message);

    public void Warning(string source, string message) => Write("WARN", source, message);

    public void Error(string source, string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";

        Write("ERROR", source, fullMessage);
    }

    private void Write(string level, string source, string message)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "POS" : source.Trim();
        var logFilePath = ResolveLogFilePath(normalizedSource);
        var lines = SplitLines(message);

        Gate.Wait();
        try
        {
            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                builder.Append('[')
                    .Append(timestamp)
                    .Append("] [")
                    .Append(level)
                    .Append("] [")
                    .Append(normalizedSource)
                    .Append("] ")
                    .AppendLine(line);
            }

            File.AppendAllText(logFilePath, builder.ToString(), Encoding.UTF8);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static IReadOnlyList<string> SplitLines(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return [string.Empty];
        }

        return message
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .ToList();
    }

    private string ResolveLogFilePath(string source)
    {
        var fileName = ResolveLogFileName(source);
        return Path.Combine(_logDirectoryPath, fileName);
    }

    private static string ResolveLogFileName(string source)
    {
        return source switch
        {
            "APP" => "Avvio.log",
            "BancoViewModel" => "Banco.log",
            "DocumentListViewModel" => "Documenti.log",
            "SettingsViewModel" => "Amministrazione.log",
            "BackupImportViewModel" => "Amministrazione.log",
            "PosConfigurationViewModel" => "Amministrazione.log",
            "WinEcrConfigurationViewModel" => "Amministrazione.log",
            "DiagnosticsViewModel" => "Amministrazione.log",
            "ThemeManagementViewModel" => "Amministrazione.log",
            "FastReportStudioViewModel" => "Stampa.log",
            "BancoPosPrintService" => "Stampa.log",
            nameof(NexiPosPaymentService) => "Pos.log",
            nameof(WinEcrAutoRunService) => "Fiscale.log",
            "FiscalizationService" => "Fiscale.log",
            _ => "Generale.log"
        };
    }
}
