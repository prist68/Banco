namespace Banco.Stampa;

public sealed record FastReportDataSourceDefinition
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool IsCollection { get; init; }

    public IReadOnlyList<FastReportDataFieldDefinition> Fields { get; init; } = Array.Empty<FastReportDataFieldDefinition>();
}
