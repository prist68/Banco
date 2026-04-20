namespace Banco.Stampa;

public sealed record PrintLayoutDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DocumentKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public PrintEngineKind Engine { get; init; }

    public string? TemplateFileName { get; init; }

    public string? AssignedPrinterName { get; init; }

    public bool IsDefault { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string? Notes { get; init; }
}
