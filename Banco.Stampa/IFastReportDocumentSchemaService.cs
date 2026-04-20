namespace Banco.Stampa;

public interface IFastReportDocumentSchemaService
{
    Task<IReadOnlyList<FastReportDocumentSchema>> GetSchemasAsync(CancellationToken cancellationToken = default);

    Task<FastReportDocumentSchema?> GetSchemaAsync(string documentKey, CancellationToken cancellationToken = default);
}
