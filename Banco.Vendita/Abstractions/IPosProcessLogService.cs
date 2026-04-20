namespace Banco.Vendita.Abstractions;

public interface IPosProcessLogService
{
    string LogFilePath { get; }

    void Info(string source, string message);

    void Warning(string source, string message);

    void Error(string source, string message, Exception? exception = null);
}
