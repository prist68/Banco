using Banco.Vendita.Articles;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleArticleReadService
{
    Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchArticlesAsync(
        string searchText,
        int? selectedPriceListOid = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchArticleMastersAsync(
        string searchText,
        int? selectedPriceListOid = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticleSearchResult?> FindArticleMasterByCodeOrBarcodeAsync(
        string codeOrBarcode,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(
        int articleOid,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticleLookupDetail?> GetArticleLookupDetailAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleArticleSearchResult>> GetArticleVariantsAsync(
        int parentArticleOid,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<GestionaleArticleSearchResult?> GetArticleMasterAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleArticleCatalogRow>> BrowseArticlesAsync(
        GestionaleArticleCatalogFilter filter,
        int? selectedPriceListOid = null,
        int maxResults = 250,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetArticleCategoryPathsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetArticleCategoryOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetArticleSecondaryCategoryOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetArticleSecondaryCategoryOidsAsync(
        int articoloOid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetVatOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetTaxOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetUnitOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetAccountOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetMarkupCategoryOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetArticleTypeOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetTraceabilityOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetCostTypeOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetConditionOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetLoyaltyOperationOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetVariantOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<GestionaleArticleCodeValidationResult?> ValidateArticleCodeAsync(
        string articleCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleArticleLegacyListinoRow>> GetArticleLegacyListinoRowsAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArticleImageRecord>> GetArticleImagesAsync(
        int articoloOid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleLookupOption>> GetArticleVariantedettaglioOptionsAsync(
        int articoloOid,
        CancellationToken cancellationToken = default);
}

public interface IGestionaleArticleWriteService
{
    Task SaveArticleAsync(
        GestionaleArticleLegacyUpdate update,
        CancellationToken cancellationToken = default);

    Task SaveQuantityPriceTiersAsync(
        GestionaleArticleLegacyOffersUpdate update,
        CancellationToken cancellationToken = default);

    Task PropagateQuantityPriceTierAsync(
        GestionaleArticleLegacyOfferPropagationRequest request,
        CancellationToken cancellationToken = default);

    Task<ArticleImageRecord> AddArticleImageAsync(
        ArticleImageAddRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteArticleImageAsync(
        int imageOid,
        CancellationToken cancellationToken = default);

    Task SetArticleImageAsPredefinitaAsync(
        int imageOid,
        int articoloOid,
        CancellationToken cancellationToken = default);

    Task UpdateArticleImageVariantAsync(
        int imageOid,
        int? variantedettaglioOid,
        CancellationToken cancellationToken = default);

    Task UpdateArticleImagePositionsAsync(
        IReadOnlyList<(int Oid, int Posizione)> updates,
        CancellationToken cancellationToken = default);

    Task SaveArticleSecondaryCategoriesAsync(
        int articoloOid,
        IReadOnlyList<int> categoryOids,
        CancellationToken cancellationToken = default);

    Task SaveArticleTagsAsync(
        int articoloOid,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default);
}
