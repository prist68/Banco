namespace Banco.Stampa;

public sealed record PrintReportFieldGroupDefinition
{
    public string GroupKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ReportArea { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<PrintReportAvailableFieldDefinition> Fields { get; init; } = Array.Empty<PrintReportAvailableFieldDefinition>();
}
