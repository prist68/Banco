using Banco.Vendita.Fiscal;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleDocumentWriter
{
    Task<FiscalizationResult> UpsertFiscalDocumentAsync(
        FiscalizationRequest request,
        int? documentoGestionaleOid = null,
        CancellationToken cancellationToken = default);
}
