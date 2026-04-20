using Banco.Vendita.Articles;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleArticleReadService
{
    Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchArticlesAsync(
        string searchText,
        int? selectedPriceListOid = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(
        int articleOid,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);
}

public interface IGestionaleArticleWriteService
{
    Task SaveArticleAsync(
        GestionaleArticleLegacyUpdate update,
        CancellationToken cancellationToken = default);
}
