namespace Banco.Vendita.Diagnostics;

public sealed class ConnectionTestResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }
}
