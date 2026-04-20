using Banco.Vendita.Documents;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleDocumentReadService
{
    Task<IReadOnlyList<GestionaleDocumentSummary>> GetRecentBancoDocumentsAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default);

    Task<GestionaleDocumentDetail?> GetDocumentDetailAsync(
        int documentOid,
        CancellationToken cancellationToken = default);

    Task<(int Numero, int Anno)> GetNextBancoDocumentNumberAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleDocumentSummary>> GetCustomerDocumentsAsync(
        int soggettoOid,
        DateTime? dataInizio = null,
        DateTime? dataFine = null,
        string? filtroArticolo = null,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticlePurchaseQuickInfo?> GetLatestArticlePurchaseAsync(
        int articoloOid,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticlePurchaseHistoryDetail> SearchArticlePurchaseHistoryAsync(
        GestionaleArticlePurchaseSearchRequest request,
        CancellationToken cancellationToken = default);
}
