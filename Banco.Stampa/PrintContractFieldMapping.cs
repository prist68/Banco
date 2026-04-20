namespace Banco.Stampa;

public sealed record PrintContractFieldMapping
{
    public string Zone { get; init; } = string.Empty;

    public string TargetField { get; init; } = string.Empty;

    public string SourceContext { get; init; } = string.Empty;

    public string SourceField { get; init; } = string.Empty;

    public PrintContractConfidence Confidence { get; init; }

    public string? Notes { get; init; }
}
