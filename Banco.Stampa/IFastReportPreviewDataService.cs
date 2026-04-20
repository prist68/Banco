namespace Banco.Stampa;

public interface IFastReportPreviewDataService
{
    Task<FastReportPreviewDocument> GetPreviewDocumentAsync(CancellationToken cancellationToken = default);
}
