namespace Banco.Stampa;

public interface IPrintLayoutCatalogService
{
    Task<IReadOnlyList<PrintLayoutDefinition>> GetLayoutsAsync(CancellationToken cancellationToken = default);

    Task SaveLayoutsAsync(IReadOnlyList<PrintLayoutDefinition> layouts, CancellationToken cancellationToken = default);
}
