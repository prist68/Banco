namespace Banco.Stampa;

public interface IFastReportStoreProfileService
{
    Task<FastReportStoreProfile> GetStoreProfileAsync(CancellationToken cancellationToken = default);
}
