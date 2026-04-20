namespace Banco.Vendita.Abstractions;

public interface IGestionaleDocumentDeleteService
{
    Task DeleteNonFiscalizedDocumentAsync(
        int documentoGestionaleOid,
        CancellationToken cancellationToken = default);
}
