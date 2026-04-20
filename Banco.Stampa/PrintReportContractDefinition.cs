namespace Banco.Stampa;

public sealed record PrintReportContractDefinition
{
    public string DocumentKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Family { get; init; } = string.Empty;

    public string DomainContext { get; init; } = string.Empty;

    public string RuntimeParametersSummary { get; init; } = string.Empty;

    public IReadOnlyList<PrintContractFieldMapping> FieldMappings { get; init; } = Array.Empty<PrintContractFieldMapping>();
}
