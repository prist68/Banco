namespace Banco.Stampa;

public interface ILegacyRepxReportCatalogService
{
    Task<IReadOnlyList<LegacyRepxReportReference>> GetReportsAsync(CancellationToken cancellationToken = default);
}
