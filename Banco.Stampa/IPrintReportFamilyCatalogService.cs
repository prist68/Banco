namespace Banco.Stampa;

public interface IPrintReportFamilyCatalogService
{
    Task<IReadOnlyList<PrintReportFamilyDefinition>> GetFamiliesAsync(CancellationToken cancellationToken = default);

    Task<PrintReportFamilyDefinition?> GetFamilyAsync(string familyKey, CancellationToken cancellationToken = default);
}
