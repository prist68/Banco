namespace Banco.Stampa;

public sealed record PrintReportAvailableFieldDefinition
{
    public string TechnicalName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string BindingPath { get; init; } = string.Empty;

    public string SourceContext { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public PrintContractConfidence Confidence { get; init; }

    public string? Notes { get; init; }
}
