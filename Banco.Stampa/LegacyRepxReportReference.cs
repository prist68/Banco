namespace Banco.Stampa;

public sealed record LegacyRepxReportReference
{
    public string Id { get; init; } = string.Empty;

    public string DocumentKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string SourceFilePath { get; init; } = string.Empty;

    public string? LegacyPrinterName { get; init; }

    public int PageWidth { get; init; }

    public bool RollPaper { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<string> Sections { get; init; } = Array.Empty<string>();

    public IReadOnlyList<LegacyRepxParameterReference> Parameters { get; init; } = Array.Empty<LegacyRepxParameterReference>();

    public IReadOnlyList<LegacyRepxBindingReference> Bindings { get; init; } = Array.Empty<LegacyRepxBindingReference>();

    public IReadOnlyList<LegacyRepxRuleReference> Rules { get; init; } = Array.Empty<LegacyRepxRuleReference>();
}
