namespace Banco.Stampa;

public sealed record FastReportDataFieldDefinition
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DataPath { get; init; } = string.Empty;

    public string SourceTable { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public string? Notes { get; init; }
}
