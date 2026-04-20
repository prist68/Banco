namespace Banco.Stampa;

public sealed class PrintLayoutCatalogSettings
{
    public IReadOnlyList<PrintLayoutDefinition> Layouts { get; init; } = Array.Empty<PrintLayoutDefinition>();
}
