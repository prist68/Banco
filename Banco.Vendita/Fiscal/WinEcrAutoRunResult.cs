namespace Banco.Vendita.Fiscal;

public sealed class WinEcrAutoRunResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? ErrorDetails { get; init; }

    public int? EcrErrorCode { get; init; }

    public string CommandFilePath { get; init; } = string.Empty;

    public string GeneratedContent { get; init; } = string.Empty;

    public string ErrorFilePath { get; init; } = string.Empty;
}
