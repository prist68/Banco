namespace Banco.Stampa;

public interface IPrinterCatalogService
{
    Task<IReadOnlyList<SystemPrinterInfo>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default);
}
