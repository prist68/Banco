namespace Banco.Stampa;

public interface IPrintReportContractCatalogService
{
    Task<IReadOnlyList<PrintReportContractDefinition>> GetContractsAsync(CancellationToken cancellationToken = default);

    Task<PrintReportContractDefinition?> GetContractAsync(string documentKey, CancellationToken cancellationToken = default);
}
