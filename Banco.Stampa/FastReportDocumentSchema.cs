namespace Banco.Stampa;

public sealed record FastReportDocumentSchema
{
    public string DocumentKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string RootObjectName { get; init; } = string.Empty;

    public IReadOnlyList<FastReportDataSourceDefinition> DataSources { get; init; } = Array.Empty<FastReportDataSourceDefinition>();
}
