using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Customers;
using Banco.Vendita.PriceLists;

namespace Banco.UI.Avalonia.Banco.Services;

public sealed class BancoSaleDataFacade
{
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IGestionaleConnectionService _connectionService;
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IGestionalePriceListReadService _priceListReadService;

    public BancoSaleDataFacade(
        IApplicationConfigurationService configurationService,
        IGestionaleConnectionService connectionService,
        IGestionaleArticleReadService articleReadService,
        IGestionaleCustomerReadService customerReadService,
        IGestionalePriceListReadService priceListReadService)
    {
        _configurationService = configurationService;
        _connectionService = connectionService;
        _articleReadService = articleReadService;
        _customerReadService = customerReadService;
        _priceListReadService = priceListReadService;
    }

    public async Task<BancoLegacyStatus> ProbeLegacyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _configurationService.LoadAsync(cancellationToken);
            var result = await _connectionService.TestConnectionAsync(settings.GestionaleDatabase, cancellationToken);
            return result.Success
                ? new BancoLegacyStatus(true, "Legacy collegato", settings.GestionaleDatabase.Database)
                : new BancoLegacyStatus(false, "Legacy non disponibile", result.Message);
        }
        catch (Exception ex)
        {
            return new BancoLegacyStatus(false, "Legacy non disponibile", ex.Message);
        }
    }

    public async Task<BancoPriceListLookupResult> GetSalesPriceListsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var priceLists = await _priceListReadService.GetSalesPriceListsAsync(cancellationToken);
            return BancoPriceListLookupResult.Online(priceLists);
        }
        catch (Exception ex)
        {
            return BancoPriceListLookupResult.Offline(
                [new GestionalePriceListSummary { Oid = 0, Nome = "Listino banco", IsDefault = true }],
                $"Listini legacy non disponibili: {ex.Message}");
        }
    }

    public async Task<BancoArticleLookupResult> SearchArticlesAsync(string text, int? selectedPriceListOid = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BancoArticleLookupResult.Offline(CreateDemoArticles(), "Ricerca demo: inserisci codice o descrizione.");
        }

        try
        {
            var articles = await _articleReadService.SearchArticlesAsync(text, selectedPriceListOid, maxResults: 12, cancellationToken: cancellationToken);
            return BancoArticleLookupResult.Online(articles);
        }
        catch (Exception ex)
        {
            return BancoArticleLookupResult.Offline(CreateDemoArticles(text), $"DB legacy non raggiungibile: {ex.Message}");
        }
    }

    public async Task<BancoDirectArticleResult> FindArticleByCodeOrBarcodeAsync(string text, int? selectedPriceListOid = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BancoDirectArticleResult.NotFound("Codice articolo vuoto.");
        }

        try
        {
            var article = await _articleReadService.FindArticleMasterByCodeOrBarcodeAsync(text.Trim(), selectedPriceListOid, cancellationToken);
            return article is null
                ? BancoDirectArticleResult.NotFound("Nessun articolo trovato per codice/barcode.")
                : BancoDirectArticleResult.Found(article);
        }
        catch (Exception ex)
        {
            var searchText = text.Trim();
            var demo = CreateDemoArticles(text)
                .FirstOrDefault(article =>
                    article.CodiceArticolo.Equals(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (article.BarcodeAlternativo?.Equals(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                ?? CreateDemoArticles(text).FirstOrDefault();
            return demo is null
                ? BancoDirectArticleResult.NotFound($"DB legacy non raggiungibile: {ex.Message}")
                : BancoDirectArticleResult.Offline(demo, $"DB legacy non raggiungibile: inserimento demo per {text}.");
        }
    }

    public async Task<BancoArticlePricingResult> GetArticlePricingDetailAsync(
        GestionaleArticleSearchResult article,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var detail = await _articleReadService.GetArticlePricingDetailAsync(article, selectedPriceListOid, cancellationToken);
            return BancoArticlePricingResult.Online(detail);
        }
        catch (Exception ex)
        {
            return BancoArticlePricingResult.Offline(CreateDemoPricingDetail(article, selectedPriceListOid), $"Prezzi legacy non disponibili: {ex.Message}");
        }
    }

    public async Task<BancoArticleLookupResult> GetArticleVariantsAsync(
        GestionaleArticleSearchResult article,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        if (!article.HasVariantChildren)
        {
            return BancoArticleLookupResult.Online([]);
        }

        try
        {
            var variants = await _articleReadService.GetArticleVariantsAsync(article.Oid, selectedPriceListOid, cancellationToken);
            return BancoArticleLookupResult.Online(variants);
        }
        catch (Exception ex)
        {
            return BancoArticleLookupResult.Offline([], $"Varianti legacy non disponibili: {ex.Message}");
        }
    }

    public async Task<BancoCustomerLookupResult> SearchCustomersAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BancoCustomerLookupResult.Offline([], "Nessun cliente selezionato.");
        }

        try
        {
            var customers = await SearchCustomersFlexibleAsync(text, cancellationToken);
            return BancoCustomerLookupResult.Online(customers);
        }
        catch (Exception ex)
        {
            return BancoCustomerLookupResult.Offline(
                CreateDemoCustomers(text),
                $"Clienti legacy non disponibili: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<GestionaleCustomerSummary>> SearchCustomersFlexibleAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<int, GestionaleCustomerSummary>();
        foreach (var query in BuildCustomerQueries(text))
        {
            var results = await _customerReadService.SearchCustomersAsync(query, maxResults: 12, cancellationToken: cancellationToken);
            foreach (var customer in results)
            {
                candidates.TryAdd(customer.Oid, customer);
            }
        }

        var terms = SplitTerms(text);
        var filtered = candidates.Values
            .Where(customer => terms.Count == 0 || terms.All(term => ContainsTerm(customer, term)))
            .OrderBy(customer => customer.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        return filtered.Count > 0 ? filtered : candidates.Values.Take(12).ToList();
    }

    private static IReadOnlyList<string> BuildCustomerQueries(string text)
    {
        var terms = SplitTerms(text);
        if (terms.Count <= 1)
        {
            return [text.Trim()];
        }

        var reversed = string.Join(' ', terms.AsEnumerable().Reverse());
        return [text.Trim(), reversed, .. terms];
    }

    private static List<string> SplitTerms(string text)
    {
        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ContainsTerm(GestionaleCustomerSummary customer, string term)
    {
        return customer.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
            || customer.DisplayLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (customer.Nome?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || customer.RagioneSociale.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (customer.CodiceCartaFedelta?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static IReadOnlyList<GestionaleArticleSearchResult> CreateDemoArticles(string? text = null)
    {
        var suffix = string.IsNullOrWhiteSpace(text) ? "demo" : text.Trim();
        return
        [
            new GestionaleArticleSearchResult
            {
                Oid = 1001,
                CodiceArticolo = "ART-001",
                Descrizione = $"Articolo {suffix}",
                PrezzoVendita = 12.50m,
                Giacenza = 18,
                IvaOid = 1,
                AliquotaIva = 22
            },
            new GestionaleArticleSearchResult
            {
                Oid = 1002,
                CodiceArticolo = "ART-002",
                Descrizione = "Prodotto banco con disponibilita bassa",
                PrezzoVendita = 7.90m,
                Giacenza = 2,
                IvaOid = 1,
                AliquotaIva = 10
            },
            new GestionaleArticleSearchResult
            {
                Oid = 1003,
                CodiceArticolo = "ART-003",
                Descrizione = "Servizio confezione",
                PrezzoVendita = 3.00m,
                Giacenza = 0,
                IvaOid = 1,
                AliquotaIva = 22
            }
        ];
    }

    private static IReadOnlyList<GestionaleCustomerSummary> CreateDemoCustomers(string text)
    {
        var terms = SplitTerms(text);
        var customers = new List<GestionaleCustomerSummary>
        {
            new()
            {
                Oid = 1,
                RagioneSociale = "Cliente generico demo",
                PuntiAssegnati = 0
            },
            new()
            {
                Oid = 101,
                RagioneSociale = "Rossi",
                Nome = "Mario",
                CodiceCartaFedelta = "FID-101",
                PuntiAssegnati = 128
            },
            new()
            {
                Oid = 102,
                RagioneSociale = "Bianchi",
                Nome = "Laura",
                CodiceCartaFedelta = "FID-102",
                PuntiAssegnati = 42
            }
        };

        return customers
            .Where(customer => terms.Count == 0 || terms.All(term => ContainsTerm(customer, term)))
            .DefaultIfEmpty(customers[0])
            .Take(8)
            .ToList();
    }

    private static GestionaleArticlePricingDetail CreateDemoPricingDetail(
        GestionaleArticleSearchResult article,
        int? selectedPriceListOid)
    {
        return new GestionaleArticlePricingDetail
        {
            ArticoloOid = article.Oid,
            ListinoOid = selectedPriceListOid,
            ListinoNome = selectedPriceListOid.HasValue ? $"Listino {selectedPriceListOid}" : "Listino banco",
            UnitaMisuraPrincipale = "PZ",
            QuantitaMinimaVendita = 1,
            QuantitaMultiplaVendita = 1,
            FascePrezzoQuantita =
            [
                new GestionaleArticleQuantityPriceTier
                {
                    QuantitaMinima = 1,
                    PrezzoUnitario = article.PrezzoVendita
                }
            ]
        };
    }
}

public sealed record BancoLegacyStatus(bool IsOnline, string Title, string Detail);

public sealed record BancoArticleLookupResult(
    bool IsOnline,
    IReadOnlyList<GestionaleArticleSearchResult> Articles,
    string Message)
{
    public static BancoArticleLookupResult Online(IReadOnlyList<GestionaleArticleSearchResult> articles) =>
        new(true, articles, articles.Count == 0 ? "Nessun articolo trovato." : "Articoli letti dal legacy.");

    public static BancoArticleLookupResult Offline(IReadOnlyList<GestionaleArticleSearchResult> articles, string message) =>
        new(false, articles, message);
}

public sealed record BancoCustomerLookupResult(
    bool IsOnline,
    IReadOnlyList<GestionaleCustomerSummary> Customers,
    string Message)
{
    public static BancoCustomerLookupResult Online(IReadOnlyList<GestionaleCustomerSummary> customers) =>
        new(true, customers, customers.Count == 0 ? "Nessun cliente trovato." : "Clienti letti dal legacy.");

    public static BancoCustomerLookupResult Offline(IReadOnlyList<GestionaleCustomerSummary> customers, string message) =>
        new(false, customers, message);
}

public sealed record BancoPriceListLookupResult(
    bool IsOnline,
    IReadOnlyList<GestionalePriceListSummary> PriceLists,
    string Message)
{
    public static BancoPriceListLookupResult Online(IReadOnlyList<GestionalePriceListSummary> priceLists) =>
        new(true, priceLists, priceLists.Count == 0 ? "Nessun listino trovato." : "Listini letti dal legacy.");

    public static BancoPriceListLookupResult Offline(IReadOnlyList<GestionalePriceListSummary> priceLists, string message) =>
        new(false, priceLists, message);
}

public sealed record BancoArticlePricingResult(
    bool IsOnline,
    GestionaleArticlePricingDetail? Detail,
    string Message)
{
    public static BancoArticlePricingResult Online(GestionaleArticlePricingDetail? detail) =>
        new(true, detail, detail is null ? "Dettaglio prezzo non disponibile." : "Prezzo letto dal legacy.");

    public static BancoArticlePricingResult Offline(GestionaleArticlePricingDetail detail, string message) =>
        new(false, detail, message);
}

public sealed record BancoDirectArticleResult(
    bool IsFound,
    bool IsOnline,
    GestionaleArticleSearchResult? Article,
    string Message)
{
    public static BancoDirectArticleResult Found(GestionaleArticleSearchResult article) =>
        new(true, true, article, "Articolo letto dal legacy.");

    public static BancoDirectArticleResult Offline(GestionaleArticleSearchResult article, string message) =>
        new(true, false, article, message);

    public static BancoDirectArticleResult NotFound(string message) =>
        new(false, false, null, message);
}
