namespace Banco.Stampa;

public sealed class FastReportRuntimeActionResult
{
    public bool Succeeded { get; init; }

    public bool IsSupported { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? OutputPath { get; init; }

    public string? AssignedPrinterName { get; init; }
}
