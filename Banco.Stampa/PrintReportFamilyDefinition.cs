namespace Banco.Stampa;

public sealed record PrintReportFamilyDefinition
{
    public string FamilyKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DomainContext { get; init; } = string.Empty;

    public string RuntimeModelSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedDocumentKeys { get; init; } = Array.Empty<string>();

    public IReadOnlyList<PrintReportFieldGroupDefinition> FieldGroups { get; init; } = Array.Empty<PrintReportFieldGroupDefinition>();
}
