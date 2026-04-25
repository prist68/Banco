using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using MySqlConnector;
using System.Text;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleArticleReadService : IGestionaleArticleReadService
{
    private readonly IApplicationConfigurationService _configurationService;
    private const string PreferredSalesPriceExpression =
        """
        COALESCE(
            (
                SELECT COALESCE(NULLIF(al1.Valoreivato, 0), NULLIF(al1.Valore, 0), 0)
                FROM articololistino al1
                INNER JOIN listino l1 ON l1.OID = al1.Listino
                WHERE al1.Articolo = a.OID
                  AND (@selectedListinoOid IS NULL OR al1.Listino = @selectedListinoOid)
                ORDER BY
                    CASE WHEN @selectedListinoOid IS NULL AND UPPER(COALESCE(NULLIF(TRIM(l1.Listino), ''), '')) = 'WEB' THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(l1.Tipolistino, 0) = 1 THEN 1 ELSE 0 END,
                    CASE WHEN COALESCE(l1.Predefinito, 0) = 1 THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(l1.Mostrainricerca, 0) = 1 THEN 0 ELSE 1 END,
                    COALESCE(NULLIF(al1.Quantitaminima, 0), 1),
                    al1.OID
                LIMIT 1
            ),
            aml.Listinoivato1,
            aml.Listino1,
            0
        )
        """;

    private const string PreferredVariantSalesPriceExpression =
        """
        COALESCE(
            (
                SELECT COALESCE(NULLIF(al1.Valoreivato, 0), NULLIF(al1.Valore, 0), 0)
                FROM articololistino al1
                INNER JOIN listino l1 ON l1.OID = al1.Listino
                WHERE al1.Articolo = a.OID
                  AND (@selectedListinoOid IS NULL OR al1.Listino = @selectedListinoOid)
                  AND COALESCE(al1.Variantedettaglio1, 0) = COALESCE(amc.Variantedettaglio1, 0)
                  AND COALESCE(al1.Variantedettaglio2, 0) = COALESCE(amc.Variantedettaglio2, 0)
                ORDER BY
                    CASE WHEN @selectedListinoOid IS NULL AND UPPER(COALESCE(NULLIF(TRIM(l1.Listino), ''), '')) = 'WEB' THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(l1.Tipolistino, 0) = 1 THEN 1 ELSE 0 END,
                    CASE WHEN COALESCE(l1.Predefinito, 0) = 1 THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(l1.Mostrainricerca, 0) = 1 THEN 0 ELSE 1 END,
                    COALESCE(NULLIF(al1.Quantitaminima, 0), 1),
                    al1.OID
                LIMIT 1
            ),
            (
                SELECT COALESCE(NULLIF(al2.Valoreivato, 0), NULLIF(al2.Valore, 0), 0)
                FROM articololistino al2
                INNER JOIN listino l2 ON l2.OID = al2.Listino
                WHERE al2.Articolo = a.OID
                  AND (@selectedListinoOid IS NULL OR al2.Listino = @selectedListinoOid)
                  AND al2.Variantedettaglio1 IS NULL
                  AND al2.Variantedettaglio2 IS NULL
                ORDER BY
                    CASE WHEN @selectedListinoOid IS NULL AND UPPER(COALESCE(NULLIF(TRIM(l2.Listino), ''), '')) = 'WEB' THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(l2.Tipolistino, 0) = 1 THEN 1 ELSE 0 END,
                    CASE WHEN COALESCE(l2.Predefinito, 0) = 1 THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(l2.Mostrainricerca, 0) = 1 THEN 0 ELSE 1 END,
                    COALESCE(NULLIF(al2.Quantitaminima, 0), 1),
                    al2.OID
                LIMIT 1
            ),
            aml.Listinoivato1,
            aml.Listino1,
            0
        )
        """;

    public GestionaleArticleReadService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchArticlesAsync(
        string searchText,
        int? selectedPriceListOid = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchText?.Trim() ?? string.Empty;
        var searchTerms = SplitSearchTerms(normalizedSearch);
        var isBarcodeSearch = IsBarcodeSearch(normalizedSearch);
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var aggregatedResults = new List<GestionaleArticleSearchResult>();

        var variantResults = await SearchVariantArticlesAsync(connection, normalizedSearch, searchTerms, selectedPriceListOid, maxResults, cancellationToken);
        aggregatedResults.AddRange(variantResults);

        try
        {
            var exactResults = await SearchExactArticlesAsync(connection, normalizedSearch, selectedPriceListOid, maxResults, cancellationToken);
            aggregatedResults.AddRange(exactResults);
        }
        catch (MySqlException)
        {
            // Alcuni schema legacy non espongono tutte le colonne usate dal lookup exact: la ricerca continua con varianti e catalogo generale.
        }

        try
        {
            aggregatedResults.AddRange(
                await ExecuteArticleSearchAsync(
                    connection,
                    string.Format(
                        """
                        SELECT
                            a.OID,
                            a.Codicearticolo,
                            a.Descrizionearticolo,
                            {1} AS PrezzoVendita,
                            COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0) AS Giacenza,
                            COALESCE(a.Iva, 0) AS IvaOid,
                            0 AS AliquotaIva,
                            COALESCE(a.Tipoarticolo, 0) AS TipoArticoloOid,
                            NULL AS ArticoloPadreOid,
                            NULL AS VarianteDescrizione
                        FROM articolo a
                        LEFT JOIN (
                            SELECT
                                Articolo,
                                MAX(COALESCE(Listinoivato1, 0)) AS Listinoivato1,
                                MAX(COALESCE(Listino1, 0)) AS Listino1,
                                MAX(COALESCE(Disponibilita, 0)) AS Disponibilita
                            FROM articolomultilistino
                            GROUP BY Articolo
                        ) aml ON aml.Articolo = a.OID
                        LEFT JOIN (
                            SELECT
                                Articolo,
                                MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                            FROM articolosituazione
                            GROUP BY Articolo
                        ) asi ON asi.Articolo = a.OID
                        LEFT JOIN (
                            SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                            FROM valorizzazionearticolo va
                            INNER JOIN (
                                SELECT Articolo, MAX(OID) AS MaxOid
                                FROM valorizzazionearticolo
                                GROUP BY Articolo
                            ) latest ON latest.MaxOid = va.OID
                        ) vs ON vs.Articolo = a.OID
                        WHERE {0}
                        ORDER BY a.Descrizionearticolo
                        LIMIT @maxResults;
                        """,
                        BuildTokenizedSearchCondition(
                            searchTerms,
                            "a.Codicearticolo",
                            "a.Codiciabarre",
                            "a.Descrizionearticolo",
                            "COALESCE(a.Notearticolo, '')",
                            "COALESCE(a.Notearticoloestese, '')"),
                        PreferredSalesPriceExpression),
                    searchTerms,
                    selectedPriceListOid,
                    maxResults,
                    cancellationToken));
        }
        catch (MySqlException)
        {
            aggregatedResults.AddRange(
                await ExecuteArticleSearchAsync(
                    connection,
                    string.Format(
                        """
                        SELECT
                            a.OID,
                            a.Codicearticolo,
                            a.Descrizionearticolo,
                            {1} AS PrezzoVendita,
                            COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, 0) AS Giacenza,
                            COALESCE(a.Iva, 0) AS IvaOid,
                            0 AS AliquotaIva,
                            COALESCE(a.Tipoarticolo, 0) AS TipoArticoloOid,
                            NULL AS ArticoloPadreOid,
                            NULL AS VarianteDescrizione
                        FROM articolo a
                        LEFT JOIN (
                            SELECT
                                Articolo,
                                MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                            FROM articolosituazione
                            GROUP BY Articolo
                        ) asi ON asi.Articolo = a.OID
                        LEFT JOIN (
                            SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                            FROM valorizzazionearticolo va
                            INNER JOIN (
                                SELECT Articolo, MAX(OID) AS MaxOid
                                FROM valorizzazionearticolo
                                GROUP BY Articolo
                            ) latest ON latest.MaxOid = va.OID
                        ) vs ON vs.Articolo = a.OID
                        WHERE {0}
                        ORDER BY a.Descrizionearticolo
                        LIMIT @maxResults;
                        """,
                        BuildTokenizedSearchCondition(
                            searchTerms,
                            "a.Codicearticolo",
                            "a.Descrizionearticolo",
                            "COALESCE(a.Notearticolo, '')",
                            "COALESCE(a.Notearticoloestese, '')"),
                        PreferredSalesPriceExpression),
                    searchTerms,
                    selectedPriceListOid,
                    maxResults,
                    cancellationToken));
        }

        var sortedResults = SortResults(
            aggregatedResults,
            normalizedSearch,
            searchTerms,
            isBarcodeSearch);

        // Con barcode la variante reale deve arrivare fino al Banco senza essere
        // ricondotta al padre, altrimenti la riga vendita perde il dettaglio collegato.
        var projectedResults = isBarcodeSearch
            ? sortedResults
            : CollapseVariantsToParentArticles(sortedResults);

        return DeduplicateResults(projectedResults, maxResults);
    }

    public async Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchArticleMastersAsync(
        string searchText,
        int? selectedPriceListOid = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchArticlesAsync(
            searchText,
            selectedPriceListOid,
            maxResults,
            cancellationToken);

        return await ResolveMasterSearchResultsAsync(results, selectedPriceListOid, maxResults, cancellationToken);
    }

    public async Task<GestionaleArticleSearchResult?> FindArticleMasterByCodeOrBarcodeAsync(
        string codeOrBarcode,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = codeOrBarcode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        var exactMatches = await SearchArticlesAsync(
            normalizedCode,
            selectedPriceListOid,
            maxResults: 10,
            cancellationToken);

        var exactMatch = exactMatches.FirstOrDefault(item =>
            item.CodiceArticolo.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));

        exactMatch ??= exactMatches.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.BarcodeAlternativo) &&
            item.BarcodeAlternativo.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is null)
        {
            return null;
        }

        return await GetArticleMasterAsync(exactMatch, selectedPriceListOid, cancellationToken);
    }

    public async Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(
        int articleOid,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        if (articleOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await GetArticlePricingDetailCoreAsync(
            connection,
            articleOid,
            null,
            null,
            selectedPriceListOid,
            0,
            cancellationToken);
    }

    public async Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        if (articolo.Oid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await GetArticlePricingDetailCoreAsync(
            connection,
            articolo.Oid,
            articolo.VarianteDettaglioOid1,
            articolo.VarianteDettaglioOid2,
            selectedPriceListOid,
            articolo.PrezzoVendita,
            cancellationToken);
    }

    public async Task<GestionaleArticleLookupDetail?> GetArticleLookupDetailAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(articolo);
        if (articolo.Oid <= 0)
        {
            return null;
        }

        var settings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var pricingDetail = await GetArticlePricingDetailCoreAsync(
            connection,
            articolo.Oid,
            articolo.VarianteDettaglioOid1,
            articolo.VarianteDettaglioOid2,
            selectedPriceListOid,
            articolo.PrezzoVendita,
            cancellationToken);

        await using var detailCommand = connection.CreateCommand();
        detailCommand.CommandText =
            """
            SELECT
                a.OID,
                a.Categoria AS CategoriaOid,
                a.Iva AS IvaOid,
                a.Tassa AS TassaOid,
                COALESCE(a.Usatassa, 0) AS UsaTassa,
                a.Moltiplicatoretassa AS MoltiplicatoreTassa,
                a.Unitadimisura AS UnitaMisuraOid,
                a.Unitadimisura2 AS UnitaMisuraSecondariaOid,
                a.Contoprimanota AS ContoCostoOid,
                a.Contoprimanotaricavo AS ContoRicavoOid,
                a.Categoriaricarico AS CategoriaRicaricoOid,
                a.Variante1 AS Variante1Oid,
                a.Variante2 AS Variante2Oid,
                COALESCE(a.Garanziamesivendita, 0) AS GaranziaMesiVendita,
                COALESCE(a.Tipoarticolo, 0) AS TipoArticoloCode,
                COALESCE(a.Tracciabilita, 0) AS TracciabilitaCode,
                COALESCE(a.Tipocostoarticolo, 0) AS TipoCostoArticoloCode,
                COALESCE(a.Quantitaminimavendita, 0) AS QuantitaMinimaVenditaLegacy,
                COALESCE(a.Quantitamultiplivendita, 0) AS QuantitaMultiplaVenditaLegacy,
                COALESCE(a.Usavenditaalbancotouch, 0) AS UsaVenditaAlBancoTouch,
                COALESCE(a.Esporta, 0) AS Esporta,
                COALESCE(a.Escludiinventario, 0) AS EscludiInventario,
                COALESCE(a.Escluditotaledocumento, 0) AS EscludiTotaleDocumento,
                COALESCE(a.Escludiscontrino, 0) AS EscludiScontrino,
                COALESCE(a.Escludiscontosoggetto, 0) AS EscludiScontoSoggetto,
                COALESCE(a.Isobsolete, 0) AS IsObsoleto,
                COALESCE(a.Descrizionebreveinaltridatigestionali, 0) AS AggDescrBreveAllaDescrizione,
                COALESCE(a.Fonte, '') AS Fonte,
                COALESCE(a.Codicetipo, '') AS CodiceTipo,
                COALESCE(a.Codicevalore, '') AS CodiceValore,
                COALESCE(a.Avvertenze, '') AS Avvertenze,
                COALESCE(a.Online, 0) AS Online,
                a.Disponibilitaonline AS DisponibilitaOnlineOid,
                COALESCE(don.Disponibilitaonline, '') AS DisponibilitaOnlineLabel,
                COALESCE(a.Condizione, 0) AS CondizioneCode,
                COALESCE(a.Operazionesucartafedelta, 0) AS OperazioneSuCartaFedeltaCode,
                COALESCE(a.Codicearticolo, '') AS CodiceArticolo,
                COALESCE(a.Descrizionearticolo, '') AS DescrizioneArticolo,
                COALESCE(a.Notearticolo, '') AS DescrizioneBreveHtml,
                COALESCE(a.Notearticoloestese, '') AS DescrizioneLungaHtml,
                COALESCE(a.Tagecommerce, '') AS Tagecommerce,
                COALESCE(c.Categoria, '') AS Categoria,
                COALESCE(cm.Categoria, '') AS SottoCategoria,
                COALESCE(cc.Conto, '') AS ContoCostoLabel,
                COALESCE(cr.Conto, '') AS ContoRicavoLabel,
                COALESCE(car.Categoriaricarico, '') AS CategoriaRicaricoLabel,
                COALESCE(v1.Variante, '') AS Variante1LookupLabel,
                COALESCE(v2.Variante, '') AS Variante2LookupLabel,
                COALESCE(t.Tassa, '') AS AccisaLabel,
                (
                    SELECT ai.OID
                    FROM articoloimmagine ai
                    WHERE ai.Articolo = a.OID
                      AND (
                            (@varianteDettaglioOid1 IS NULL AND @varianteDettaglioOid2 IS NULL)
                         OR
                            ai.Variantedettaglio IS NULL
                         OR ai.Variantedettaglio = @varianteDettaglioOid1
                         OR ai.Variantedettaglio = @varianteDettaglioOid2
                      )
                    ORDER BY
                        CASE WHEN COALESCE(ai.Predefinita, 0) = 1 THEN 0 ELSE 1 END,
                        COALESCE(ai.Posizione, 0),
                        ai.OID
                    LIMIT 1
                ) AS ImageOid,
                (
                    SELECT ai.Fonteimmagine
                    FROM articoloimmagine ai
                    WHERE ai.Articolo = a.OID
                      AND (
                            (@varianteDettaglioOid1 IS NULL AND @varianteDettaglioOid2 IS NULL)
                         OR
                            ai.Variantedettaglio IS NULL
                         OR ai.Variantedettaglio = @varianteDettaglioOid1
                         OR ai.Variantedettaglio = @varianteDettaglioOid2
                      )
                    ORDER BY
                        CASE WHEN COALESCE(ai.Predefinita, 0) = 1 THEN 0 ELSE 1 END,
                        COALESCE(ai.Posizione, 0),
                        ai.OID
                    LIMIT 1
                ) AS ImageUrl
            FROM articolo a
            LEFT JOIN categoria c ON c.OID = a.Categoria
            LEFT JOIN categoria cm ON cm.OID = c.Categoriamadre
            LEFT JOIN contoprimanota cc ON cc.OID = a.Contoprimanota
            LEFT JOIN contoprimanota cr ON cr.OID = a.Contoprimanotaricavo
            LEFT JOIN categoriaricarico car ON car.OID = a.Categoriaricarico
            LEFT JOIN tassa t ON t.OID = a.Tassa
            LEFT JOIN variante v1 ON v1.OID = a.Variante1
            LEFT JOIN variante v2 ON v2.OID = a.Variante2
            LEFT JOIN disponibilitaonline don ON don.OID = a.Disponibilitaonline
            WHERE a.OID = @articleOid
            LIMIT 1;
            """;
        detailCommand.Parameters.AddWithValue("@articleOid", articolo.Oid);
        detailCommand.Parameters.AddWithValue("@varianteDettaglioOid1", articolo.VarianteDettaglioOid1.HasValue ? articolo.VarianteDettaglioOid1.Value : DBNull.Value);
        detailCommand.Parameters.AddWithValue("@varianteDettaglioOid2", articolo.VarianteDettaglioOid2.HasValue ? articolo.VarianteDettaglioOid2.Value : DBNull.Value);

        await using var detailReader = await detailCommand.ExecuteReaderAsync(cancellationToken);
        if (!await detailReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var descrizioneBreveHtml = detailReader.IsDBNull(detailReader.GetOrdinal("DescrizioneBreveHtml"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("DescrizioneBreveHtml")));
        var descrizioneLungaHtml = detailReader.IsDBNull(detailReader.GetOrdinal("DescrizioneLungaHtml"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("DescrizioneLungaHtml")));
        var categoria = detailReader.IsDBNull(detailReader.GetOrdinal("Categoria"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("Categoria")));
        var sottoCategoria = detailReader.IsDBNull(detailReader.GetOrdinal("SottoCategoria"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("SottoCategoria")));
        var tagEcommerce = detailReader.IsDBNull(detailReader.GetOrdinal("Tagecommerce"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("Tagecommerce")));
        var accisaLabel = detailReader.IsDBNull(detailReader.GetOrdinal("AccisaLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("AccisaLabel")));
        var imageUrl = detailReader.IsDBNull(detailReader.GetOrdinal("ImageUrl"))
            ? null
            : detailReader.GetString(detailReader.GetOrdinal("ImageUrl"));
        int? imageOid = detailReader.IsDBNull(detailReader.GetOrdinal("ImageOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("ImageOid")));
        int? categoriaOid = detailReader.IsDBNull(detailReader.GetOrdinal("CategoriaOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("CategoriaOid")));
        int? ivaOid = detailReader.IsDBNull(detailReader.GetOrdinal("IvaOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("IvaOid")));
        int? tassaOid = detailReader.IsDBNull(detailReader.GetOrdinal("TassaOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("TassaOid")));
        var usaTassa = !detailReader.IsDBNull(detailReader.GetOrdinal("UsaTassa")) &&
                       Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("UsaTassa"))) != 0;
        decimal? moltiplicatoreTassa = detailReader.IsDBNull(detailReader.GetOrdinal("MoltiplicatoreTassa"))
            ? null
            : Convert.ToDecimal(detailReader.GetValue(detailReader.GetOrdinal("MoltiplicatoreTassa")));
        int? unitaMisuraOid = detailReader.IsDBNull(detailReader.GetOrdinal("UnitaMisuraOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("UnitaMisuraOid")));
        int? unitaMisuraSecondariaOid = detailReader.IsDBNull(detailReader.GetOrdinal("UnitaMisuraSecondariaOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("UnitaMisuraSecondariaOid")));
        int? contoCostoOid = detailReader.IsDBNull(detailReader.GetOrdinal("ContoCostoOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("ContoCostoOid")));
        int? contoRicavoOid = detailReader.IsDBNull(detailReader.GetOrdinal("ContoRicavoOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("ContoRicavoOid")));
        int? categoriaRicaricoOid = detailReader.IsDBNull(detailReader.GetOrdinal("CategoriaRicaricoOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("CategoriaRicaricoOid")));
        int? variante1Oid = detailReader.IsDBNull(detailReader.GetOrdinal("Variante1Oid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("Variante1Oid")));
        int? variante2Oid = detailReader.IsDBNull(detailReader.GetOrdinal("Variante2Oid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("Variante2Oid")));
        int? garanziaMesiVendita = detailReader.IsDBNull(detailReader.GetOrdinal("GaranziaMesiVendita"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("GaranziaMesiVendita")));
        int? tipoArticoloCode = detailReader.IsDBNull(detailReader.GetOrdinal("TipoArticoloCode"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("TipoArticoloCode")));
        int? tracciabilitaCode = detailReader.IsDBNull(detailReader.GetOrdinal("TracciabilitaCode"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("TracciabilitaCode")));
        int? tipoCostoArticoloCode = detailReader.IsDBNull(detailReader.GetOrdinal("TipoCostoArticoloCode"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("TipoCostoArticoloCode")));
        var quantitaMinimaVenditaLegacy = detailReader.IsDBNull(detailReader.GetOrdinal("QuantitaMinimaVenditaLegacy"))
            ? 0m
            : Convert.ToDecimal(detailReader.GetValue(detailReader.GetOrdinal("QuantitaMinimaVenditaLegacy")));
        var quantitaMultiplaVenditaLegacy = detailReader.IsDBNull(detailReader.GetOrdinal("QuantitaMultiplaVenditaLegacy"))
            ? 0m
            : Convert.ToDecimal(detailReader.GetValue(detailReader.GetOrdinal("QuantitaMultiplaVenditaLegacy")));
        var usaVenditaAlBancoTouch = !detailReader.IsDBNull(detailReader.GetOrdinal("UsaVenditaAlBancoTouch")) &&
                                     Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("UsaVenditaAlBancoTouch"))) != 0;
        var esporta = !detailReader.IsDBNull(detailReader.GetOrdinal("Esporta")) &&
                      Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("Esporta"))) != 0;
        var escludiInventario = !detailReader.IsDBNull(detailReader.GetOrdinal("EscludiInventario")) &&
                                Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("EscludiInventario"))) != 0;
        var escludiTotaleDocumento = !detailReader.IsDBNull(detailReader.GetOrdinal("EscludiTotaleDocumento")) &&
                                     Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("EscludiTotaleDocumento"))) != 0;
        var escludiScontrino = !detailReader.IsDBNull(detailReader.GetOrdinal("EscludiScontrino")) &&
                               Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("EscludiScontrino"))) != 0;
        var escludiScontoSoggetto = !detailReader.IsDBNull(detailReader.GetOrdinal("EscludiScontoSoggetto")) &&
                                    Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("EscludiScontoSoggetto"))) != 0;
        var isObsoleto = !detailReader.IsDBNull(detailReader.GetOrdinal("IsObsoleto")) &&
                         Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("IsObsoleto"))) != 0;
        var aggDescrBreveAllaDescrizione = !detailReader.IsDBNull(detailReader.GetOrdinal("AggDescrBreveAllaDescrizione")) &&
                                           Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("AggDescrBreveAllaDescrizione"))) != 0;
        var fonte = detailReader.IsDBNull(detailReader.GetOrdinal("Fonte"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("Fonte")));
        var codiceTipo = detailReader.IsDBNull(detailReader.GetOrdinal("CodiceTipo"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("CodiceTipo")));
        var codiceValore = detailReader.IsDBNull(detailReader.GetOrdinal("CodiceValore"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("CodiceValore")));
        var avvertenze = detailReader.IsDBNull(detailReader.GetOrdinal("Avvertenze"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("Avvertenze")));
        var online = !detailReader.IsDBNull(detailReader.GetOrdinal("Online")) &&
                     Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("Online"))) != 0;
        int? disponibilitaOnlineOid = detailReader.IsDBNull(detailReader.GetOrdinal("DisponibilitaOnlineOid"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("DisponibilitaOnlineOid")));
        var disponibilitaOnlineLabel = detailReader.IsDBNull(detailReader.GetOrdinal("DisponibilitaOnlineLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("DisponibilitaOnlineLabel")));
        int? condizioneCode = detailReader.IsDBNull(detailReader.GetOrdinal("CondizioneCode"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("CondizioneCode")));
        int? operazioneSuCartaFedeltaCode = detailReader.IsDBNull(detailReader.GetOrdinal("OperazioneSuCartaFedeltaCode"))
            ? null
            : Convert.ToInt32(detailReader.GetValue(detailReader.GetOrdinal("OperazioneSuCartaFedeltaCode")));
        var contoCostoLabel = detailReader.IsDBNull(detailReader.GetOrdinal("ContoCostoLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("ContoCostoLabel")));
        var contoRicavoLabel = detailReader.IsDBNull(detailReader.GetOrdinal("ContoRicavoLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("ContoRicavoLabel")));
        var categoriaRicaricoLabel = detailReader.IsDBNull(detailReader.GetOrdinal("CategoriaRicaricoLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("CategoriaRicaricoLabel")));
        var variante1LookupLabel = detailReader.IsDBNull(detailReader.GetOrdinal("Variante1LookupLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("Variante1LookupLabel")));
        var variante2LookupLabel = detailReader.IsDBNull(detailReader.GetOrdinal("Variante2LookupLabel"))
            ? string.Empty
            : NormalizeLegacyText(detailReader.GetString(detailReader.GetOrdinal("Variante2LookupLabel")));

        await detailReader.CloseAsync();

        var specifiche = await LoadArticleLookupSpecificationsAsync(connection, articolo.Oid, cancellationToken);
        var ultimaVendita = await LoadArticleLastSaleDateAsync(connection, articolo.Oid, cancellationToken);
        var statisticheVendita = await LoadArticleSalesStatisticsAsync(connection, articolo.Oid, cancellationToken);

        return new GestionaleArticleLookupDetail
        {
            ArticoloOid = articolo.Oid,
            CategoriaOid = categoriaOid,
            IvaOid = ivaOid,
            TassaOid = tassaOid,
            UsaTassa = usaTassa,
            MoltiplicatoreTassa = moltiplicatoreTassa,
            UnitaMisuraOid = unitaMisuraOid,
            UnitaMisuraSecondariaOid = unitaMisuraSecondariaOid,
            ContoCostoOid = contoCostoOid,
            ContoRicavoOid = contoRicavoOid,
            CategoriaRicaricoOid = categoriaRicaricoOid,
            Variante1Oid = variante1Oid,
            Variante2Oid = variante2Oid,
            GaranziaMesiVendita = garanziaMesiVendita,
            TipoArticoloCode = tipoArticoloCode,
            TracciabilitaCode = tracciabilitaCode,
            TipoCostoArticoloCode = tipoCostoArticoloCode,
            UsaVenditaAlBancoTouch = usaVenditaAlBancoTouch,
            Esporta = esporta,
            EscludiInventario = escludiInventario,
            EscludiTotaleDocumento = escludiTotaleDocumento,
            EscludiScontrino = escludiScontrino,
            EscludiScontoSoggetto = escludiScontoSoggetto,
            IsObsoleto = isObsoleto,
            AggDescrBreveAllaDescrizione = aggDescrBreveAllaDescrizione,
            Fonte = fonte,
            CodiceTipo = codiceTipo,
            CodiceValore = codiceValore,
            Avvertenze = avvertenze,
            Online = online,
            DisponibilitaOnlineOid = disponibilitaOnlineOid,
            DisponibilitaOnlineLabel = disponibilitaOnlineLabel,
            CondizioneCode = condizioneCode,
            OperazioneSuCartaFedeltaCode = operazioneSuCartaFedeltaCode,
            VendutoUltimoMese = statisticheVendita.VendutoUltimoMese,
            VendutoUltimiTreMesi = statisticheVendita.VendutoUltimiTreMesi,
            CodiceArticolo = NormalizeLegacyText(articolo.CodiceArticolo),
            Descrizione = NormalizeLegacyText(articolo.Descrizione),
            VarianteLabel = NormalizeLegacyText(articolo.VarianteLabel),
            BarcodeAlternativo = NormalizeLegacyText(articolo.BarcodeAlternativo),
            DescrizioneBreveHtml = descrizioneBreveHtml,
            DescrizioneLungaHtml = descrizioneLungaHtml,
            Categoria = categoria,
            SottoCategoria = sottoCategoria,
            ContoCostoLabel = contoCostoLabel,
            ContoRicavoLabel = contoRicavoLabel,
            CategoriaRicaricoLabel = categoriaRicaricoLabel,
            Variante1LookupLabel = variante1LookupLabel,
            Variante2LookupLabel = variante2LookupLabel,
            ImageUrl = NormalizeImageUrl(imageUrl, imageOid, articolo.Oid, settings.FmContent),
            Brand = string.Empty,
            ExciseLabel = accisaLabel,
            LastSaleDate = ultimaVendita,
            ListinoNome = pricingDetail?.ListinoNome ?? string.Empty,
            PrezzoVendita = articolo.PrezzoVendita,
            Giacenza = articolo.Giacenza,
            QuantitaMinimaVendita = pricingDetail?.QuantitaMinimaVendita
                ?? (quantitaMinimaVenditaLegacy > 0 ? quantitaMinimaVenditaLegacy : 1),
            QuantitaMultiplaVendita = pricingDetail?.QuantitaMultiplaVendita
                ?? (quantitaMultiplaVenditaLegacy > 0 ? quantitaMultiplaVenditaLegacy : 1),
            Tags = BuildArticleLookupTags(tagEcommerce),
            Specifications = specifiche,
            FascePrezzoQuantita = pricingDetail?.FascePrezzoQuantita ?? []
        };
    }

    public async Task<IReadOnlyList<GestionaleArticleSearchResult>> GetArticleVariantsAsync(
        int parentArticleOid,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        if (parentArticleOid <= 0)
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var results = new List<GestionaleArticleSearchResult>();

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(
            """
            SELECT
                a.OID,
                a.Codicearticolo,
                a.Descrizionearticolo,
                COALESCE(
                    NULLIF({1}, 0),
                    NULLIF({0}, 0)
                ) AS PrezzoVendita,
                CASE
                    WHEN amc.Variantedettaglio1 IS NOT NULL OR amc.Variantedettaglio2 IS NOT NULL
                        THEN COALESCE(asv.DisponibilitaVariante, vv.DisponibilitaVariante, asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0)
                    ELSE COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0)
                END AS Giacenza,
                COALESCE(a.Iva, 0) AS IvaOid,
                0 AS AliquotaIva,
                COALESCE(a.Tipoarticolo, 0) AS TipoArticoloOid,
                a.OID AS ArticoloPadreOid,
                amc.Codicealternativo AS BarcodeAlternativo,
                amc.Variantedettaglio1 AS VarianteDettaglioOid1,
                amc.Variantedettaglio2 AS VarianteDettaglioOid2,
                CONCAT_WS(' / ',
                    NULLIF(TRIM(v1.Variante), ''),
                    NULLIF(TRIM(v2.Variante), '')
                ) AS VarianteNome,
                CONCAT_WS(' / ',
                    NULLIF(TRIM(vd1.Variantedettaglio), ''),
                    NULLIF(TRIM(vd2.Variantedettaglio), '')
                ) AS VarianteDescrizione
            FROM articolomulticodice amc
            INNER JOIN articolo a ON a.OID = amc.Articolo
            LEFT JOIN variantedettaglio vd1 ON vd1.OID = amc.Variantedettaglio1
            LEFT JOIN variantedettaglio vd2 ON vd2.OID = amc.Variantedettaglio2
            LEFT JOIN variante v1 ON v1.OID = vd1.Variante
            LEFT JOIN variante v2 ON v2.OID = vd2.Variante
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Listinoivato1, 0)) AS Listinoivato1,
                    MAX(COALESCE(Listino1, 0)) AS Listino1,
                    MAX(COALESCE(Disponibilita, 0)) AS Disponibilita
                FROM articolomultilistino
                GROUP BY Articolo
            ) aml ON aml.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                FROM articolosituazione
                GROUP BY Articolo
            ) asi ON asi.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0) AS Variantedettaglio2,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaVariante
                FROM articolosituazionecombinazionevariante
                GROUP BY
                    Articolo,
                    Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0)
            ) asv ON asv.Articolo = a.OID
                 AND asv.Variantedettaglio1 = amc.Variantedettaglio1
                 AND asv.Variantedettaglio2 = COALESCE(amc.Variantedettaglio2, 0)
            LEFT JOIN (
                SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                FROM valorizzazionearticolo va
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latestBase ON latestBase.MaxOid = va.OID
            ) vs ON vs.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    va.Articolo,
                    vav.Variantedettagliooid1,
                    COALESCE(vav.Variantedettagliooid2, 0) AS Variantedettagliooid2,
                    MAX(vav.Disponibilita) AS DisponibilitaVariante
                FROM valorizzazionearticolovariante vav
                INNER JOIN valorizzazionearticolo va ON va.OID = vav.Valorizzazione
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latestVariant ON latestVariant.MaxOid = va.OID
                GROUP BY
                    va.Articolo,
                    vav.Variantedettagliooid1,
                    COALESCE(vav.Variantedettagliooid2, 0)
            ) vv ON vv.Articolo = a.OID
                 AND vv.Variantedettagliooid1 = amc.Variantedettaglio1
                 AND vv.Variantedettagliooid2 = COALESCE(amc.Variantedettaglio2, 0)
            WHERE amc.Articolo = @parentArticleOid
              AND (amc.Variantedettaglio1 IS NOT NULL OR amc.Variantedettaglio2 IS NOT NULL)
            ORDER BY
                COALESCE(v1.Variante, ''),
                COALESCE(vd1.Variantedettaglio, ''),
                COALESCE(v2.Variante, ''),
                COALESCE(vd2.Variantedettaglio, ''),
                amc.Codicealternativo,
                a.Descrizionearticolo;
            """,
            PreferredSalesPriceExpression,
            PreferredVariantSalesPriceExpression);
        command.Parameters.AddWithValue("@parentArticleOid", parentArticleOid);
        command.Parameters.AddWithValue("@selectedListinoOid", selectedPriceListOid.HasValue ? selectedPriceListOid.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var oidOrdinal = reader.GetOrdinal("OID");
        var codiceOrdinal = reader.GetOrdinal("Codicearticolo");
        var descrizioneOrdinal = reader.GetOrdinal("Descrizionearticolo");
        var prezzoOrdinal = reader.GetOrdinal("PrezzoVendita");
        var giacenzaOrdinal = reader.GetOrdinal("Giacenza");
        var ivaOrdinal = reader.GetOrdinal("IvaOid");
        var aliquotaOrdinal = reader.GetOrdinal("AliquotaIva");
        var tipoArticoloOrdinal = reader.GetOrdinal("TipoArticoloOid");
        var padreOrdinal = reader.GetOrdinal("ArticoloPadreOid");
        var barcodeAlternativoOrdinal = reader.GetOrdinal("BarcodeAlternativo");
        var varianteDettaglioOid1Ordinal = reader.GetOrdinal("VarianteDettaglioOid1");
        var varianteDettaglioOid2Ordinal = reader.GetOrdinal("VarianteDettaglioOid2");
        var varianteNomeOrdinal = reader.GetOrdinal("VarianteNome");
        var varianteDescrizioneOrdinal = reader.GetOrdinal("VarianteDescrizione");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionaleArticleSearchResult
            {
                Oid = reader.GetInt32(oidOrdinal),
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(codiceOrdinal)),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(descrizioneOrdinal)),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                BarcodeAlternativo = reader.IsDBNull(barcodeAlternativoOrdinal) ? null : NormalizeLegacyText(reader.GetString(barcodeAlternativoOrdinal)),
                VarianteDettaglioOid1 = reader.IsDBNull(varianteDettaglioOid1Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid1Ordinal)),
                VarianteDettaglioOid2 = reader.IsDBNull(varianteDettaglioOid2Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid2Ordinal)),
                VarianteNome = reader.IsDBNull(varianteNomeOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteNomeOrdinal)),
                VarianteDescrizione = reader.IsDBNull(varianteDescrizioneOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteDescrizioneOrdinal))
            });
        }

        return results
            .GroupBy(static result => new
            {
                result.Oid,
                VarianteDettaglioOid1 = result.VarianteDettaglioOid1 ?? 0,
                VarianteDettaglioOid2 = result.VarianteDettaglioOid2 ?? 0,
                VarianteNome = result.VarianteNome ?? string.Empty,
                VarianteDescrizione = result.VarianteDescrizione ?? string.Empty
            })
            .Select(static group =>
            {
                var selected = group
                    .OrderByDescending(static item => item.Giacenza)
                    .ThenBy(static item => item.BarcodeAlternativo?.Length ?? int.MaxValue)
                    .First();

                return new GestionaleArticleSearchResult
                {
                    Oid = selected.Oid,
                    CodiceArticolo = selected.CodiceArticolo,
                    Descrizione = selected.Descrizione,
                    PrezzoVendita = selected.PrezzoVendita,
                    Giacenza = selected.Giacenza,
                    IvaOid = selected.IvaOid,
                    AliquotaIva = selected.AliquotaIva,
                    TipoArticoloOid = selected.TipoArticoloOid,
                    ArticoloPadreOid = selected.ArticoloPadreOid,
                    // Nel picker varianti la selezione deve seguire la variante reale,
                    // non il singolo barcode storico collegato nel tempo.
                    BarcodeAlternativo = null,
                    VarianteDettaglioOid1 = selected.VarianteDettaglioOid1,
                    VarianteDettaglioOid2 = selected.VarianteDettaglioOid2,
                    VarianteNome = selected.VarianteNome,
                    VarianteDescrizione = selected.VarianteDescrizione
                };
            })
            .OrderBy(static item => item.VarianteNome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.VarianteDescrizione, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GestionaleArticleSearchResult?> GetArticleMasterAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(articolo);

        var articleOid = articolo.IsVariante
            ? articolo.ArticoloPadreOid
            : articolo.Oid;

        if (!articleOid.HasValue || articleOid.Value <= 0)
        {
            return articolo.IsVariante ? null : articolo;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(
            """
            SELECT
                a.OID,
                a.Codicearticolo,
                a.Descrizionearticolo,
                {0} AS PrezzoVendita,
                COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0) AS Giacenza,
                COALESCE(a.Iva, 0) AS IvaOid,
                0 AS AliquotaIva,
                COALESCE(a.Tipoarticolo, 0) AS TipoArticoloOid
            FROM articolo a
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Listinoivato1, 0)) AS Listinoivato1,
                    MAX(COALESCE(Listino1, 0)) AS Listino1,
                    MAX(COALESCE(Disponibilita, 0)) AS Disponibilita
                FROM articolomultilistino
                GROUP BY Articolo
            ) aml ON aml.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                FROM articolosituazione
                GROUP BY Articolo
            ) asi ON asi.Articolo = a.OID
            LEFT JOIN (
                SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                FROM valorizzazionearticolo va
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latest ON latest.MaxOid = va.OID
            ) vs ON vs.Articolo = a.OID
            WHERE a.OID = @articleOid
            LIMIT 1;
            """,
            PreferredSalesPriceExpression);
        command.Parameters.AddWithValue("@articleOid", articleOid.Value);
        command.Parameters.AddWithValue("@selectedListinoOid", selectedPriceListOid.HasValue ? selectedPriceListOid.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return articolo.IsVariante ? null : articolo;
        }

        var master = new GestionaleArticleSearchResult
        {
            Oid = reader.GetInt32(reader.GetOrdinal("OID")),
            CodiceArticolo = reader.IsDBNull(reader.GetOrdinal("Codicearticolo")) ? string.Empty : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Codicearticolo"))),
            Descrizione = reader.IsDBNull(reader.GetOrdinal("Descrizionearticolo")) ? string.Empty : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Descrizionearticolo"))),
            PrezzoVendita = reader.IsDBNull(reader.GetOrdinal("PrezzoVendita")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PrezzoVendita"))),
            Giacenza = reader.IsDBNull(reader.GetOrdinal("Giacenza")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Giacenza"))),
            IvaOid = reader.IsDBNull(reader.GetOrdinal("IvaOid")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("IvaOid"))),
            AliquotaIva = reader.IsDBNull(reader.GetOrdinal("AliquotaIva")) ? 0 : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("AliquotaIva"))),
            TipoArticoloOid = reader.IsDBNull(reader.GetOrdinal("TipoArticoloOid")) ? null : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("TipoArticoloOid"))),
            HasVariantChildren = articolo.IsVariante || articolo.HasVariantChildren
        };

        return master;
    }

    private async Task<IReadOnlyList<GestionaleArticleSearchResult>> ResolveMasterSearchResultsAsync(
        IReadOnlyList<GestionaleArticleSearchResult> results,
        int? selectedPriceListOid,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var masters = new List<GestionaleArticleSearchResult>(results.Count);
        var seenMasterOids = new HashSet<int>();

        foreach (var result in results)
        {
            var master = await GetArticleMasterAsync(result, selectedPriceListOid, cancellationToken);
            if (master is null)
            {
                continue;
            }

            if (seenMasterOids.Add(master.Oid))
            {
                masters.Add(master);
            }

            if (masters.Count >= maxResults)
            {
                break;
            }
        }

        return masters;
    }

    public async Task<IReadOnlyList<GestionaleArticleCatalogRow>> BrowseArticlesAsync(
        GestionaleArticleCatalogFilter filter,
        int? selectedPriceListOid = null,
        int maxResults = 250,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var normalizedSearch = filter.SearchText?.Trim() ?? string.Empty;
        var searchTerms = SplitSearchTerms(normalizedSearch);
        var categoryPath = filter.CategoryPath?.Trim() ?? string.Empty;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(
            """
            SELECT
                a.OID,
                COALESCE(a.Codicearticolo, '') AS CodiceArticolo,
                COALESCE(a.Descrizionearticolo, '') AS DescrizioneArticolo,
                COALESCE(a.Codiciabarre, '') AS BarcodePrincipale,
                COALESCE(c.Categoria, '') AS Categoria,
                COALESCE(cm.Categoria, '') AS SottoCategoria,
                {1} AS PrezzoVendita,
                COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0) AS Giacenza,
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM articoloimmagine ai
                        WHERE ai.Articolo = a.OID
                    ) THEN 1
                    ELSE 0
                END AS HasImage
            FROM articolo a
            LEFT JOIN categoria c ON c.OID = a.Categoria
            LEFT JOIN categoria cm ON cm.OID = c.Categoriamadre
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Listinoivato1, 0)) AS Listinoivato1,
                    MAX(COALESCE(Listino1, 0)) AS Listino1,
                    MAX(COALESCE(Disponibilita, 0)) AS Disponibilita
                FROM articolomultilistino
                GROUP BY Articolo
            ) aml ON aml.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                FROM articolosituazione
                GROUP BY Articolo
            ) asi ON asi.Articolo = a.OID
            LEFT JOIN (
                SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                FROM valorizzazionearticolo va
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latest ON latest.MaxOid = va.OID
            ) vs ON vs.Articolo = a.OID
            WHERE {0}
              AND (
                    @categoryPath = ''
                 OR CASE
                        WHEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '')
                        WHEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '')
                        ELSE CONCAT(TRIM(cm.Categoria), ' > ', TRIM(c.Categoria))
                    END = @categoryPath
              )
              AND (@onlyAvailable = 0 OR COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0) > 0)
              AND (
                    @onlyWithImage = 0
                 OR EXISTS (
                        SELECT 1
                        FROM articoloimmagine ai
                        WHERE ai.Articolo = a.OID
                    )
              )
            ORDER BY
                CASE
                    WHEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '')
                    WHEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '')
                    ELSE CONCAT(TRIM(cm.Categoria), ' > ', TRIM(c.Categoria))
                END,
                a.Descrizionearticolo,
                a.Codicearticolo
            LIMIT @maxResults;
            """,
            BuildTokenizedSearchCondition(
                searchTerms,
                "a.Codicearticolo",
                "a.Codiciabarre",
                "a.Descrizionearticolo",
                "COALESCE(c.Categoria, '')",
                "COALESCE(cm.Categoria, '')",
                "COALESCE(a.Notearticolo, '')",
                "COALESCE(a.Notearticoloestese, '')"),
            PreferredSalesPriceExpression);
        AddSearchTermParameters(command, searchTerms);
        command.Parameters.AddWithValue("@selectedListinoOid", selectedPriceListOid.HasValue ? selectedPriceListOid.Value : DBNull.Value);
        command.Parameters.AddWithValue("@categoryPath", categoryPath);
        command.Parameters.AddWithValue("@onlyAvailable", filter.OnlyAvailable ? 1 : 0);
        command.Parameters.AddWithValue("@onlyWithImage", filter.OnlyWithImage ? 1 : 0);
        command.Parameters.AddWithValue("@maxResults", maxResults);

        var rows = new List<GestionaleArticleCatalogRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(MapCatalogRow(reader));
        }

        return rows;
    }

    public async Task<IReadOnlyList<string>> GetArticleCategoryPathsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT
                CASE
                    WHEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '')
                    WHEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '')
                    ELSE CONCAT(TRIM(cm.Categoria), ' > ', TRIM(c.Categoria))
                END AS CategoryPath
            FROM articolo a
            LEFT JOIN categoria c ON c.OID = a.Categoria
            LEFT JOIN categoria cm ON cm.OID = c.Categoriamadre
            WHERE COALESCE(NULLIF(TRIM(c.Categoria), ''), NULLIF(TRIM(cm.Categoria), '')) IS NOT NULL
            ORDER BY CategoryPath;
            """;

        var categories = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(reader.GetOrdinal("CategoryPath")))
            {
                continue;
            }

            var categoryPath = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("CategoryPath")));
            if (!string.IsNullOrWhiteSpace(categoryPath))
            {
                categories.Add(categoryPath);
            }
        }

        return categories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetArticleCategoryOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT
                c.OID,
                CASE
                    WHEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '')
                    WHEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '')
                    ELSE CONCAT(TRIM(cm.Categoria), ' > ', TRIM(c.Categoria))
                END AS CategoryPath
            FROM articolo a
            LEFT JOIN categoria c ON c.OID = a.Categoria
            LEFT JOIN categoria cm ON cm.OID = c.Categoriamadre
            WHERE COALESCE(NULLIF(TRIM(c.Categoria), ''), NULLIF(TRIM(cm.Categoria), '')) IS NOT NULL
            ORDER BY CategoryPath;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(reader.GetOrdinal("CategoryPath")))
            {
                continue;
            }

            var label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("CategoryPath")));
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = label
            });
        }

        return options
            .GroupBy(item => item.Oid)
            .Select(group => group.First())
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetArticleSecondaryCategoryOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                c.OID,
                CASE
                    WHEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '')
                    WHEN COALESCE(NULLIF(TRIM(c.Categoria), ''), '') = '' THEN COALESCE(NULLIF(TRIM(cm.Categoria), ''), '')
                    ELSE CONCAT(TRIM(cm.Categoria), ' >> ', TRIM(c.Categoria))
                END AS CategoryPath
            FROM categoria c
            LEFT JOIN categoria cm ON cm.OID = c.Categoriamadre
            WHERE COALESCE(NULLIF(TRIM(c.Categoria), ''), NULLIF(TRIM(cm.Categoria), '')) IS NOT NULL
            ORDER BY CategoryPath;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(reader.GetOrdinal("CategoryPath")))
            {
                continue;
            }

            var label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("CategoryPath")));
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = label
            });
        }

        return options
            .GroupBy(item => item.Oid)
            .Select(group => group.First())
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetArticleSecondaryCategoryOidsAsync(
        int articoloOid,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT Categoria
            FROM articolocategoria
            WHERE Articolo = @articoloOid
              AND Categoria IS NOT NULL
            ORDER BY Categoria;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        var categoryOids = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            categoryOids.Add(reader.GetInt32(0));
        }

        return categoryOids;
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetVatOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                OID,
                COALESCE(Codice, '') AS Codice,
                COALESCE(Iva, '') AS Iva,
                COALESCE(Percentualeiva, 0) AS PercentualeIva
            FROM iva
            ORDER BY PercentualeIva, Codice, OID;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var codice = reader.IsDBNull(reader.GetOrdinal("Codice"))
                ? string.Empty
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Codice")));
            var descrizioneIva = reader.IsDBNull(reader.GetOrdinal("Iva"))
                ? string.Empty
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Iva")));
            var percentuale = reader.IsDBNull(reader.GetOrdinal("PercentualeIva"))
                ? 0m
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PercentualeIva")));

            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = BuildVatOptionLabel(codice, descrizioneIva, percentuale)
            });
        }

        return options;
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetTaxOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT OID, COALESCE(Tassa, '') AS Tassa
            FROM tassa
            WHERE COALESCE(NULLIF(TRIM(Tassa), ''), '') <> ''
            ORDER BY Tassa;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Tassa")))
            });
        }

        return options;
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetUnitOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT OID, UPPER(TRIM(Unitadimisura)) AS UnitaMisura
            FROM unitadimisura
            WHERE COALESCE(NULLIF(TRIM(Unitadimisura), ''), '') <> ''
            ORDER BY UnitaMisura;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("UnitaMisura")))
            });
        }

        return options;
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetAccountOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT OID, COALESCE(Conto, '') AS Conto
            FROM contoprimanota
            WHERE COALESCE(NULLIF(TRIM(Conto), ''), '') <> ''
            ORDER BY Conto;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Conto")))
            });
        }

        return options;
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetMarkupCategoryOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT OID, COALESCE(Categoriaricarico, '') AS Categoriaricarico
            FROM categoriaricarico
            WHERE COALESCE(NULLIF(TRIM(Categoriaricarico), ''), '') <> ''
            ORDER BY Categoriaricarico;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Categoriaricarico")))
            });
        }

        return options;
    }

    public Task<IReadOnlyList<GestionaleLookupOption>> GetArticleTypeOptionsAsync(CancellationToken cancellationToken = default) =>
        GetLegacyEnumOptionsAsync(
            "Tipoarticolo",
            ["EnumTipoArticolo", "EnumTIpoArticolo"],
            "Normale",
            cancellationToken);

    public Task<IReadOnlyList<GestionaleLookupOption>> GetTraceabilityOptionsAsync(CancellationToken cancellationToken = default) =>
        GetLegacyEnumOptionsAsync(
            "Tracciabilita",
            ["EnumTracciabilita"],
            "Nessuna",
            cancellationToken);

    public Task<IReadOnlyList<GestionaleLookupOption>> GetCostTypeOptionsAsync(CancellationToken cancellationToken = default) =>
        GetLegacyEnumOptionsAsync(
            "Tipocostoarticolo",
            ["EnumTipoCostoArticolo"],
            "Normale",
            cancellationToken);

    public Task<IReadOnlyList<GestionaleLookupOption>> GetConditionOptionsAsync(CancellationToken cancellationToken = default) =>
        GetLegacyEnumOptionsAsync(
            "Condizione",
            ["EnumCondizione"],
            "Nuovo",
            cancellationToken);

    public Task<IReadOnlyList<GestionaleLookupOption>> GetLoyaltyOperationOptionsAsync(CancellationToken cancellationToken = default) =>
        GetLegacyEnumOptionsAsync(
            "Operazionesucartafedelta",
            ["EnumOperazioneSuCartaFedelta"],
            "Aumento punti carta fedelta'",
            cancellationToken);

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetVariantOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT OID, COALESCE(Variante, '') AS Variante
            FROM variante
            WHERE COALESCE(NULLIF(TRIM(Variante), ''), '') <> ''
            ORDER BY Variante;
            """;

        var options = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            options.Add(new GestionaleLookupOption
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                Label = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Variante")))
            });
        }

        return options;
    }

    private async Task<IReadOnlyList<GestionaleLookupOption>> GetLegacyEnumOptionsAsync(
        string columnName,
        IReadOnlyList<string> configKeyPrefixes,
        string zeroLabel,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var labelsByCode = await LoadLegacyEnumLabelsAsync(connection, configKeyPrefixes, cancellationToken);
        var codes = await LoadLegacyCodesAsync(connection, columnName, cancellationToken);
        var allCodes = labelsByCode.Keys
            .Concat(codes)
            .Distinct()
            .OrderBy(code => code)
            .ToList();

        var options = new List<GestionaleLookupOption>();
        foreach (var code in allCodes)
        {
            options.Add(new GestionaleLookupOption
            {
                Oid = code,
                Label = ResolveLegacyEnumLabel(labelsByCode, code, zeroLabel)
            });
        }

        return options;
    }

    private static string ResolveLegacyEnumLabel(
        IReadOnlyDictionary<int, string> labelsByCode,
        int code,
        string zeroLabel)
    {
        if (labelsByCode.TryGetValue(code, out var label) && !string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        return code == 0 ? zeroLabel : $"Codice {code}";
    }

    private static async Task<List<int>> LoadLegacyCodesAsync(
        MySqlConnection connection,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT COALESCE({columnName}, 0) AS Code
            FROM articolo
            ORDER BY Code;
            """;

        var codes = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.IsDBNull(reader.GetOrdinal("Code"))
                ? 0
                : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Code")));

            if (!codes.Contains(code))
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    private static async Task<Dictionary<int, string>> LoadLegacyEnumLabelsAsync(
        MySqlConnection connection,
        IReadOnlyList<string> keyPrefixes,
        CancellationToken cancellationToken)
    {
        if (keyPrefixes.Count == 0)
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        var whereParts = new List<string>();
        for (var index = 0; index < keyPrefixes.Count; index++)
        {
            var parameterName = $"@prefix{index}";
            whereParts.Add($"`Key` LIKE {parameterName}");
            command.Parameters.AddWithValue(parameterName, $"{keyPrefixes[index]}%");
        }

        command.CommandText =
            $"""
            SELECT `Key`, COALESCE(Keyvalue, '') AS Keyvalue
            FROM config
            WHERE {string.Join(" OR ", whereParts)}
            ORDER BY `Key`;
            """;

        var labelsByCode = new Dictionary<int, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(reader.GetOrdinal("Key"));
            var value = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Keyvalue")));
            var code = ExtractLegacyEnumCode(key, keyPrefixes);
            if (!code.HasValue)
            {
                continue;
            }

            labelsByCode[code.Value] = value;
        }

        return labelsByCode;
    }

    private static int? ExtractLegacyEnumCode(
        string rawValue,
        IReadOnlyList<string> keyPrefixes)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || keyPrefixes.Count == 0)
        {
            return null;
        }

        foreach (var prefix in keyPrefixes)
        {
            if (!rawValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = rawValue[prefix.Length..];
            if (string.IsNullOrWhiteSpace(suffix) || !Regex.IsMatch(suffix, @"^\d+$"))
            {
                continue;
            }

            return int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        return null;
    }

    public async Task<GestionaleArticleCodeValidationResult?> ValidateArticleCodeAsync(
        string articleCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeLegacyText(articleCode).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                SourceLabel,
                ArticoloOid,
                CodiceArticolo,
                DescrizioneArticolo,
                VarianteLabel
            FROM (
                SELECT
                    'Codice articolo' AS SourceLabel,
                    a.OID AS ArticoloOid,
                    COALESCE(a.Codicearticolo, '') AS CodiceArticolo,
                    COALESCE(a.Descrizionearticolo, '') AS DescrizioneArticolo,
                    '' AS VarianteLabel
                FROM articolo a
                WHERE UPPER(TRIM(COALESCE(a.Codicearticolo, ''))) = UPPER(TRIM(@articleCode))

                UNION ALL

                SELECT
                    'Barcode articolo' AS SourceLabel,
                    a.OID AS ArticoloOid,
                    COALESCE(a.Codicearticolo, '') AS CodiceArticolo,
                    COALESCE(a.Descrizionearticolo, '') AS DescrizioneArticolo,
                    '' AS VarianteLabel
                FROM articolo a
                WHERE UPPER(TRIM(COALESCE(a.Codiciabarre, ''))) = UPPER(TRIM(@articleCode))

                UNION ALL

                SELECT
                    'Barcode variante' AS SourceLabel,
                    a.OID AS ArticoloOid,
                    COALESCE(a.Codicearticolo, '') AS CodiceArticolo,
                    COALESCE(a.Descrizionearticolo, '') AS DescrizioneArticolo,
                    CONCAT_WS(' / ',
                        NULLIF(TRIM(vd1.Variantedettaglio), ''),
                        NULLIF(TRIM(vd2.Variantedettaglio), '')
                    ) AS VarianteLabel
                FROM articolomulticodice amc
                INNER JOIN articolo a ON a.OID = amc.Articolo
                LEFT JOIN variantedettaglio vd1 ON vd1.OID = amc.Variantedettaglio1
                LEFT JOIN variantedettaglio vd2 ON vd2.OID = amc.Variantedettaglio2
                WHERE UPPER(TRIM(COALESCE(amc.Codicealternativo, ''))) = UPPER(TRIM(@articleCode))
            ) matches
            ORDER BY CodiceArticolo, VarianteLabel, SourceLabel;
            """;
        command.Parameters.AddWithValue("@articleCode", normalizedCode);

        var matches = new List<GestionaleArticleCodeValidationMatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new GestionaleArticleCodeValidationMatch
            {
                ArticoloOid = reader.IsDBNull(reader.GetOrdinal("ArticoloOid"))
                    ? 0
                    : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("ArticoloOid"))),
                CodiceArticolo = reader.IsDBNull(reader.GetOrdinal("CodiceArticolo"))
                    ? normalizedCode
                    : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("CodiceArticolo"))),
                DescrizioneArticolo = reader.IsDBNull(reader.GetOrdinal("DescrizioneArticolo"))
                    ? string.Empty
                    : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("DescrizioneArticolo"))),
                SourceLabel = reader.IsDBNull(reader.GetOrdinal("SourceLabel"))
                    ? string.Empty
                    : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("SourceLabel"))),
                VarianteLabel = reader.IsDBNull(reader.GetOrdinal("VarianteLabel"))
                    ? null
                    : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("VarianteLabel")))
            });
        }

        var normalizedMatches = matches
            .GroupBy(match => new
            {
                match.ArticoloOid,
                match.CodiceArticolo,
                VarianteLabel = match.VarianteLabel ?? string.Empty
            })
            .Select(group =>
            {
                var first = group.First();
                return new GestionaleArticleCodeValidationMatch
                {
                    ArticoloOid = first.ArticoloOid,
                    CodiceArticolo = first.CodiceArticolo,
                    DescrizioneArticolo = first.DescrizioneArticolo,
                    VarianteLabel = first.VarianteLabel,
                    SourceLabel = string.Join(
                        " + ",
                        group.Select(match => match.SourceLabel)
                            .Where(source => !string.IsNullOrWhiteSpace(source))
                            .Distinct(StringComparer.OrdinalIgnoreCase))
                };
            })
            .ToList();

        if (normalizedMatches.Count == 0)
        {
            return new GestionaleArticleCodeValidationResult
            {
                CodiceArticolo = normalizedCode,
                DescrizioneArticolo = string.Empty,
                MatchCount = 0,
                Matches = []
            };
        }

        var firstMatch = normalizedMatches[0];
        return new GestionaleArticleCodeValidationResult
        {
            CodiceArticolo = firstMatch.CodiceArticolo,
            DescrizioneArticolo = firstMatch.DescrizioneArticolo,
            MatchCount = normalizedMatches.Count,
            Matches = normalizedMatches
        };
    }

    public async Task<IReadOnlyList<GestionaleArticleLegacyListinoRow>> GetArticleLegacyListinoRowsAsync(
        GestionaleArticleSearchResult articolo,
        int? selectedPriceListOid = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(articolo);
        if (articolo.Oid <= 0)
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var preferredListino = selectedPriceListOid.HasValue
            ? (Oid: selectedPriceListOid, Nome: $"Listino {selectedPriceListOid.Value}")
            : await ResolvePreferredSalesListinoAsync(connection, articolo.Oid, cancellationToken);
        var listinoPreferito = preferredListino.Oid;
        var includeAllVariantRows = !articolo.VarianteDettaglioOid1.HasValue && !articolo.VarianteDettaglioOid2.HasValue;

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                al.OID,
                al.Articolo,
                al.Listino,
                COALESCE(NULLIF(TRIM(l.Listino), ''), CONCAT('Listino ', al.Listino)) AS ListinoNome,
                COALESCE(NULLIF(al.Quantitaminima, 0), 1) AS QuantitaMinima,
                CASE
                    WHEN al.Variantedettaglio1 IS NULL AND al.Variantedettaglio2 IS NULL
                        THEN COALESCE(asi.Ultimocosto, 0)
                    ELSE COALESCE(ascv.Ultimocosto, asi.Ultimocosto, 0)
                END AS UltimoCostoLegacy,
                COALESCE(al.Valore, 0) AS PrezzoNetto,
                COALESCE(al.Valoreivato, al.Valore, 0) AS PrezzoIvato,
                al.Datafine,
                al.Variantedettaglio1,
                al.Variantedettaglio2,
                COALESCE(vd1.Variantedettaglio, '') AS Variante1Desc,
                COALESCE(vd2.Variantedettaglio, '') AS Variante2Desc
            FROM articololistino al
            INNER JOIN listino l ON l.OID = al.Listino
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Ultimocosto, 0)) AS Ultimocosto
                FROM articolosituazione
                GROUP BY Articolo
            ) asi ON asi.Articolo = al.Articolo
            LEFT JOIN (
                SELECT
                    Articolo,
                    COALESCE(Variantedettaglio1, 0) AS Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0) AS Variantedettaglio2,
                    MAX(COALESCE(Ultimocosto, 0)) AS Ultimocosto
                FROM articolosituazionecombinazionevariante
                GROUP BY
                    Articolo,
                    COALESCE(Variantedettaglio1, 0),
                    COALESCE(Variantedettaglio2, 0)
            ) ascv ON ascv.Articolo = al.Articolo
                  AND ascv.Variantedettaglio1 = COALESCE(al.Variantedettaglio1, 0)
                  AND ascv.Variantedettaglio2 = COALESCE(al.Variantedettaglio2, 0)
            LEFT JOIN variantedettaglio vd1 ON vd1.OID = al.Variantedettaglio1
            LEFT JOIN variantedettaglio vd2 ON vd2.OID = al.Variantedettaglio2
            WHERE al.Articolo = @articleOid
              AND (@preferredListinoOid IS NULL OR al.Listino = @preferredListinoOid)
              AND (
                    @includeAllVariantRows = 1
                 OR (al.Variantedettaglio1 IS NULL AND al.Variantedettaglio2 IS NULL)
                 OR (
                        COALESCE(al.Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
                    AND COALESCE(al.Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0)
                 )
              )
            ORDER BY
                CASE
                    WHEN al.Variantedettaglio1 IS NULL AND al.Variantedettaglio2 IS NULL AND COALESCE(NULLIF(al.Quantitaminima, 0), 1) = 1 THEN 0
                    WHEN al.Variantedettaglio1 IS NULL AND al.Variantedettaglio2 IS NULL THEN 1
                    WHEN COALESCE(al.Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
                     AND COALESCE(al.Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0)
                     AND COALESCE(NULLIF(al.Quantitaminima, 0), 1) = 1 THEN 2
                    WHEN COALESCE(al.Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
                     AND COALESCE(al.Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0) THEN 3
                    ELSE 4
                END,
                COALESCE(vd1.Variantedettaglio, ''),
                COALESCE(vd2.Variantedettaglio, ''),
                QuantitaMinima,
                al.OID;
            """;
        command.Parameters.AddWithValue("@articleOid", articolo.Oid);
        command.Parameters.AddWithValue("@preferredListinoOid", listinoPreferito.HasValue ? listinoPreferito.Value : DBNull.Value);
        command.Parameters.AddWithValue("@includeAllVariantRows", includeAllVariantRows ? 1 : 0);
        command.Parameters.AddWithValue("@varianteDettaglioOid1", articolo.VarianteDettaglioOid1.HasValue ? articolo.VarianteDettaglioOid1.Value : DBNull.Value);
        command.Parameters.AddWithValue("@varianteDettaglioOid2", articolo.VarianteDettaglioOid2.HasValue ? articolo.VarianteDettaglioOid2.Value : DBNull.Value);

        var rows = new List<GestionaleArticleLegacyListinoRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var quantitaMinima = reader.IsDBNull(reader.GetOrdinal("QuantitaMinima"))
                ? 1m
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("QuantitaMinima")));
            var varianteDettaglioOid1 = reader.IsDBNull(reader.GetOrdinal("Variantedettaglio1"))
                ? (int?)null
                : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Variantedettaglio1")));
            var varianteDettaglioOid2 = reader.IsDBNull(reader.GetOrdinal("Variantedettaglio2"))
                ? (int?)null
                : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Variantedettaglio2")));
            var varianteLabel = BuildVariantLabel(
                reader.IsDBNull(reader.GetOrdinal("Variante1Desc")) ? null : reader.GetString(reader.GetOrdinal("Variante1Desc")),
                reader.IsDBNull(reader.GetOrdinal("Variante2Desc")) ? null : reader.GetString(reader.GetOrdinal("Variante2Desc")));

            rows.Add(new GestionaleArticleLegacyListinoRow
            {
                RowOid = reader.GetInt32(reader.GetOrdinal("OID")),
                ArticoloOid = reader.GetInt32(reader.GetOrdinal("Articolo")),
                ListinoOid = reader.GetInt32(reader.GetOrdinal("Listino")),
                ListinoNome = reader.IsDBNull(reader.GetOrdinal("ListinoNome"))
                    ? preferredListino.Nome
                    : reader.GetString(reader.GetOrdinal("ListinoNome")),
                QuantitaMinima = quantitaMinima <= 0 ? 1m : quantitaMinima,
                UltimoCostoLegacy = reader.IsDBNull(reader.GetOrdinal("UltimoCostoLegacy"))
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("UltimoCostoLegacy"))),
                PrezzoNetto = reader.IsDBNull(reader.GetOrdinal("PrezzoNetto"))
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PrezzoNetto"))),
                PrezzoIvato = reader.IsDBNull(reader.GetOrdinal("PrezzoIvato"))
                    ? 0m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PrezzoIvato"))),
                DataFine = reader.IsDBNull(reader.GetOrdinal("Datafine"))
                    ? (DateTime?)null
                    : reader.GetDateTime(reader.GetOrdinal("Datafine")),
                VarianteDettaglioOid1 = varianteDettaglioOid1,
                VarianteDettaglioOid2 = varianteDettaglioOid2,
                VarianteLabel = varianteLabel,
                RowKind = ResolveLegacyListinoRowKind(quantitaMinima, varianteDettaglioOid1, varianteDettaglioOid2)
            });
        }

        return rows;
    }

    private static async Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchVariantArticlesAsync(
        MySqlConnection connection,
        string searchText,
        IReadOnlyList<string> searchTerms,
        int? selectedPriceListOid,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<GestionaleArticleSearchResult>();

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(
            """
            SELECT
                a.OID,
                a.Codicearticolo,
                a.Descrizionearticolo,
                COALESCE(
                    NULLIF({2}, 0),
                    NULLIF({1}, 0)
                ) AS PrezzoVendita,
                    CASE
                        WHEN amc.Variantedettaglio1 IS NOT NULL OR amc.Variantedettaglio2 IS NOT NULL
                            THEN COALESCE(asv.DisponibilitaVariante, vv.DisponibilitaVariante, asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0)
                        ELSE COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0)
                    END AS Giacenza,
                    COALESCE(a.Iva, 0) AS IvaOid,
                    0 AS AliquotaIva,
                    COALESCE(a.Tipoarticolo, 0) AS TipoArticoloOid,
                    a.OID AS ArticoloPadreOid,
                    amc.Codicealternativo AS BarcodeAlternativo,
                    amc.Variantedettaglio1 AS VarianteDettaglioOid1,
                    amc.Variantedettaglio2 AS VarianteDettaglioOid2,
                    CONCAT_WS(' / ',
                        NULLIF(TRIM(v1.Variante), ''),
                        NULLIF(TRIM(v2.Variante), '')
                    ) AS VarianteNome,
                    CONCAT_WS(' / ',
                        NULLIF(TRIM(vd1.Variantedettaglio), ''),
                        NULLIF(TRIM(vd2.Variantedettaglio), '')
                    ) AS VarianteDescrizione
            FROM articolomulticodice amc
            INNER JOIN articolo a ON a.OID = amc.Articolo
            LEFT JOIN variantedettaglio vd1 ON vd1.OID = amc.Variantedettaglio1
            LEFT JOIN variantedettaglio vd2 ON vd2.OID = amc.Variantedettaglio2
            LEFT JOIN variante v1 ON v1.OID = vd1.Variante
            LEFT JOIN variante v2 ON v2.OID = vd2.Variante
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Listinoivato1, 0)) AS Listinoivato1,
                    MAX(COALESCE(Listino1, 0)) AS Listino1,
                    MAX(COALESCE(Disponibilita, 0)) AS Disponibilita
                FROM articolomultilistino
                GROUP BY Articolo
            ) aml ON aml.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                FROM articolosituazione
                GROUP BY Articolo
            ) asi ON asi.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0) AS Variantedettaglio2,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaVariante
                FROM articolosituazionecombinazionevariante
                GROUP BY
                    Articolo,
                    Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0)
            ) asv ON asv.Articolo = a.OID
                 AND asv.Variantedettaglio1 = amc.Variantedettaglio1
                 AND asv.Variantedettaglio2 = COALESCE(amc.Variantedettaglio2, 0)
            LEFT JOIN (
                SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                FROM valorizzazionearticolo va
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latestBase ON latestBase.MaxOid = va.OID
            ) vs ON vs.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    va.Articolo,
                    vav.Variantedettagliooid1,
                    COALESCE(vav.Variantedettagliooid2, 0) AS Variantedettagliooid2,
                    MAX(vav.Disponibilita) AS DisponibilitaVariante
                FROM valorizzazionearticolovariante vav
                INNER JOIN valorizzazionearticolo va ON va.OID = vav.Valorizzazione
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latestVariant ON latestVariant.MaxOid = va.OID
                GROUP BY
                    va.Articolo,
                    vav.Variantedettagliooid1,
                    COALESCE(vav.Variantedettagliooid2, 0)
            ) vv ON vv.Articolo = a.OID
                 AND vv.Variantedettagliooid1 = amc.Variantedettaglio1
                 AND vv.Variantedettagliooid2 = COALESCE(amc.Variantedettaglio2, 0)
            WHERE amc.Codicealternativo = @exactSearch
               OR {0}
            ORDER BY a.Descrizionearticolo, VarianteNome, VarianteDescrizione
            LIMIT @maxResults;
            """,
            BuildTokenizedSearchCondition(
                searchTerms,
                "a.Codicearticolo",
                "a.Codiciabarre",
                "a.Descrizionearticolo",
                "COALESCE(a.Notearticolo, '')",
                "COALESCE(a.Notearticoloestese, '')",
                "COALESCE(vd1.Variantedettaglio, '')",
                "COALESCE(vd2.Variantedettaglio, '')",
                "COALESCE(v1.Variante, '')",
                "COALESCE(v2.Variante, '')"),
            PreferredSalesPriceExpression,
            PreferredVariantSalesPriceExpression);
        command.Parameters.AddWithValue("@exactSearch", searchText);
        command.Parameters.AddWithValue("@selectedListinoOid", selectedPriceListOid.HasValue ? selectedPriceListOid.Value : DBNull.Value);
        AddSearchTermParameters(command, searchTerms);
        command.Parameters.AddWithValue("@maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var oidOrdinal = reader.GetOrdinal("OID");
        var codiceOrdinal = reader.GetOrdinal("Codicearticolo");
        var descrizioneOrdinal = reader.GetOrdinal("Descrizionearticolo");
        var prezzoOrdinal = reader.GetOrdinal("PrezzoVendita");
        var giacenzaOrdinal = reader.GetOrdinal("Giacenza");
        var ivaOrdinal = reader.GetOrdinal("IvaOid");
        var aliquotaOrdinal = reader.GetOrdinal("AliquotaIva");
        var tipoArticoloOrdinal = reader.GetOrdinal("TipoArticoloOid");
        var padreOrdinal = reader.GetOrdinal("ArticoloPadreOid");
        var barcodeAlternativoOrdinal = reader.GetOrdinal("BarcodeAlternativo");
        var varianteDettaglioOid1Ordinal = reader.GetOrdinal("VarianteDettaglioOid1");
        var varianteDettaglioOid2Ordinal = reader.GetOrdinal("VarianteDettaglioOid2");
        var varianteNomeOrdinal = reader.GetOrdinal("VarianteNome");
        var varianteOrdinal = reader.GetOrdinal("VarianteDescrizione");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionaleArticleSearchResult
            {
                Oid = reader.GetInt32(oidOrdinal),
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(codiceOrdinal)),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(descrizioneOrdinal)),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                BarcodeAlternativo = reader.IsDBNull(barcodeAlternativoOrdinal) ? null : NormalizeLegacyText(reader.GetString(barcodeAlternativoOrdinal)),
                VarianteDettaglioOid1 = reader.IsDBNull(varianteDettaglioOid1Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid1Ordinal)),
                VarianteDettaglioOid2 = reader.IsDBNull(varianteDettaglioOid2Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid2Ordinal)),
                VarianteNome = reader.IsDBNull(varianteNomeOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteNomeOrdinal)),
                VarianteDescrizione = reader.IsDBNull(varianteOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteOrdinal))
            });
        }

        return results;
    }

    private static async Task<IReadOnlyList<GestionaleArticleSearchResult>> SearchExactArticlesAsync(
        MySqlConnection connection,
        string searchText,
        int? selectedPriceListOid,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<GestionaleArticleSearchResult>();

        await using var command = connection.CreateCommand();
        command.CommandText = string.Format(
            """
            SELECT
                a.OID,
                a.Codicearticolo,
                a.Descrizionearticolo,
                COALESCE(
                    NULLIF({1}, 0),
                    NULLIF({0}, 0)
                ) AS PrezzoVendita,
                CASE
                    WHEN amc.Variantedettaglio1 IS NOT NULL OR amc.Variantedettaglio2 IS NOT NULL
                        THEN COALESCE(asv.DisponibilitaVariante, vv.DisponibilitaVariante, asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0)
                    ELSE COALESCE(asi.DisponibilitaBase, vs.DisponibilitaBase, aml.Disponibilita, a.Disponibilitaonline, 0)
                END AS Giacenza,
                COALESCE(a.Iva, 0) AS IvaOid,
                0 AS AliquotaIva,
                COALESCE(a.Tipoarticolo, 0) AS TipoArticoloOid,
                CASE
                    WHEN amc.Codicealternativo IS NULL THEN NULL
                    ELSE a.OID
                END AS ArticoloPadreOid,
                amc.Codicealternativo AS BarcodeAlternativo,
                amc.Variantedettaglio1 AS VarianteDettaglioOid1,
                amc.Variantedettaglio2 AS VarianteDettaglioOid2,
                CONCAT_WS(' / ',
                    NULLIF(TRIM(v1.Variante), ''),
                    NULLIF(TRIM(v2.Variante), '')
                ) AS VarianteNome,
                CONCAT_WS(' / ',
                    NULLIF(TRIM(vd1.Variantedettaglio), ''),
                    NULLIF(TRIM(vd2.Variantedettaglio), '')
                ) AS VarianteDescrizione
            FROM articolo a
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Listinoivato1, 0)) AS Listinoivato1,
                    MAX(COALESCE(Listino1, 0)) AS Listino1,
                    MAX(COALESCE(Disponibilita, 0)) AS Disponibilita
                FROM articolomultilistino
                GROUP BY Articolo
            ) aml ON aml.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaBase
                FROM articolosituazione
                GROUP BY Articolo
            ) asi ON asi.Articolo = a.OID
            LEFT JOIN (
                SELECT
                    Articolo,
                    Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0) AS Variantedettaglio2,
                    MAX(COALESCE(Disponibilita, 0)) AS DisponibilitaVariante
                FROM articolosituazionecombinazionevariante
                GROUP BY
                    Articolo,
                    Variantedettaglio1,
                    COALESCE(Variantedettaglio2, 0)
            ) asv ON asv.Articolo = a.OID
                 AND asv.Variantedettaglio1 = amc.Variantedettaglio1
                 AND asv.Variantedettaglio2 = COALESCE(amc.Variantedettaglio2, 0)
            LEFT JOIN (
                SELECT va.Articolo, va.Disponibilita AS DisponibilitaBase
                FROM valorizzazionearticolo va
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latestBase ON latestBase.MaxOid = va.OID
            ) vs ON vs.Articolo = a.OID
            LEFT JOIN variantedettaglio vd1 ON vd1.OID = amc.Variantedettaglio1
            LEFT JOIN variantedettaglio vd2 ON vd2.OID = amc.Variantedettaglio2
            LEFT JOIN variante v1 ON v1.OID = vd1.Variante
            LEFT JOIN variante v2 ON v2.OID = vd2.Variante
            LEFT JOIN (
                SELECT
                    va.Articolo,
                    vav.Variantedettagliooid1,
                    COALESCE(vav.Variantedettagliooid2, 0) AS Variantedettagliooid2,
                    MAX(vav.Disponibilita) AS DisponibilitaVariante
                FROM valorizzazionearticolovariante vav
                INNER JOIN valorizzazionearticolo va ON va.OID = vav.Valorizzazione
                INNER JOIN (
                    SELECT Articolo, MAX(OID) AS MaxOid
                    FROM valorizzazionearticolo
                    GROUP BY Articolo
                ) latestVariant ON latestVariant.MaxOid = va.OID
                GROUP BY
                    va.Articolo,
                    vav.Variantedettagliooid1,
                    COALESCE(vav.Variantedettagliooid2, 0)
            ) vv ON vv.Articolo = a.OID
                 AND vv.Variantedettagliooid1 = amc.Variantedettaglio1
                 AND vv.Variantedettagliooid2 = COALESCE(amc.Variantedettaglio2, 0)
            WHERE a.Codicearticolo = @exactSearch
               OR a.Codiciabarre = @exactSearch
               OR amc.Codicealternativo = @exactSearch
            ORDER BY a.Descrizionearticolo, VarianteNome, VarianteDescrizione
            LIMIT @maxResults;
            """,
            PreferredSalesPriceExpression,
            PreferredVariantSalesPriceExpression);
        command.Parameters.AddWithValue("@exactSearch", searchText);
        command.Parameters.AddWithValue("@selectedListinoOid", selectedPriceListOid.HasValue ? selectedPriceListOid.Value : DBNull.Value);
        command.Parameters.AddWithValue("@maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var oidOrdinal = reader.GetOrdinal("OID");
        var codiceOrdinal = reader.GetOrdinal("Codicearticolo");
        var descrizioneOrdinal = reader.GetOrdinal("Descrizionearticolo");
        var prezzoOrdinal = reader.GetOrdinal("PrezzoVendita");
        var giacenzaOrdinal = reader.GetOrdinal("Giacenza");
        var ivaOrdinal = reader.GetOrdinal("IvaOid");
        var aliquotaOrdinal = reader.GetOrdinal("AliquotaIva");
        var tipoArticoloOrdinal = reader.GetOrdinal("TipoArticoloOid");
        var padreOrdinal = reader.GetOrdinal("ArticoloPadreOid");
        var barcodeAlternativoOrdinal = reader.GetOrdinal("BarcodeAlternativo");
        var varianteDettaglioOid1Ordinal = reader.GetOrdinal("VarianteDettaglioOid1");
        var varianteDettaglioOid2Ordinal = reader.GetOrdinal("VarianteDettaglioOid2");
        var varianteNomeOrdinal = reader.GetOrdinal("VarianteNome");
        var varianteOrdinal = reader.GetOrdinal("VarianteDescrizione");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionaleArticleSearchResult
            {
                Oid = reader.GetInt32(oidOrdinal),
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(codiceOrdinal)),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(descrizioneOrdinal)),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                BarcodeAlternativo = reader.IsDBNull(barcodeAlternativoOrdinal) ? null : NormalizeLegacyText(reader.GetString(barcodeAlternativoOrdinal)),
                VarianteDettaglioOid1 = reader.IsDBNull(varianteDettaglioOid1Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid1Ordinal)),
                VarianteDettaglioOid2 = reader.IsDBNull(varianteDettaglioOid2Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid2Ordinal)),
                VarianteNome = reader.IsDBNull(varianteNomeOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteNomeOrdinal)),
                VarianteDescrizione = reader.IsDBNull(varianteOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteOrdinal))
            });
        }

        return results;
    }

    private static async Task<IReadOnlyList<GestionaleArticleSearchResult>> ExecuteArticleSearchAsync(
        MySqlConnection connection,
        string commandText,
        IReadOnlyList<string> searchTerms,
        int? selectedPriceListOid,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<GestionaleArticleSearchResult>();

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddSearchTermParameters(command, searchTerms);
        command.Parameters.AddWithValue("@selectedListinoOid", selectedPriceListOid.HasValue ? selectedPriceListOid.Value : DBNull.Value);
        command.Parameters.AddWithValue("@maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var oidOrdinal = reader.GetOrdinal("OID");
        var codiceOrdinal = reader.GetOrdinal("Codicearticolo");
        var descrizioneOrdinal = reader.GetOrdinal("Descrizionearticolo");
        var prezzoOrdinal = reader.GetOrdinal("PrezzoVendita");
        var giacenzaOrdinal = reader.GetOrdinal("Giacenza");
        var ivaOrdinal = reader.GetOrdinal("IvaOid");
        var aliquotaOrdinal = reader.GetOrdinal("AliquotaIva");
        var tipoArticoloOrdinal = reader.GetOrdinal("TipoArticoloOid");
        var padreOrdinal = reader.GetOrdinal("ArticoloPadreOid");
        var varianteOrdinal = reader.GetOrdinal("VarianteDescrizione");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionaleArticleSearchResult
            {
                Oid = reader.GetInt32(oidOrdinal),
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(codiceOrdinal)),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : NormalizeLegacyText(reader.GetString(descrizioneOrdinal)),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                VarianteDescrizione = reader.IsDBNull(varianteOrdinal) ? null : NormalizeLegacyText(reader.GetString(varianteOrdinal))
            });
        }

        return results;
    }

    private static GestionaleArticleCatalogRow MapCatalogRow(MySqlDataReader reader)
    {
        var category = reader.IsDBNull(reader.GetOrdinal("Categoria"))
            ? string.Empty
            : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Categoria")));
        var subCategory = reader.IsDBNull(reader.GetOrdinal("SottoCategoria"))
            ? string.Empty
            : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("SottoCategoria")));

        var categoryPath = string.IsNullOrWhiteSpace(subCategory)
            ? category
            : string.IsNullOrWhiteSpace(category)
                ? subCategory
                : $"{subCategory} > {category}";

        return new GestionaleArticleCatalogRow
        {
            Oid = reader.GetInt32(reader.GetOrdinal("OID")),
            CodiceArticolo = reader.IsDBNull(reader.GetOrdinal("CodiceArticolo"))
                ? string.Empty
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("CodiceArticolo"))),
            Descrizione = reader.IsDBNull(reader.GetOrdinal("DescrizioneArticolo"))
                ? string.Empty
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("DescrizioneArticolo"))),
            BarcodePrincipale = reader.IsDBNull(reader.GetOrdinal("BarcodePrincipale"))
                ? null
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("BarcodePrincipale"))),
            CategoryPath = string.IsNullOrWhiteSpace(categoryPath) ? "Senza categoria" : categoryPath,
            Giacenza = reader.IsDBNull(reader.GetOrdinal("Giacenza"))
                ? 0m
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Giacenza"))),
            PrezzoVendita = reader.IsDBNull(reader.GetOrdinal("PrezzoVendita"))
                ? 0m
                : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("PrezzoVendita"))),
            HasImage = !reader.IsDBNull(reader.GetOrdinal("HasImage")) &&
                       Convert.ToInt32(reader.GetValue(reader.GetOrdinal("HasImage"))) == 1
        };
    }

    private static async Task<(int? Oid, string Nome)> ResolvePreferredSalesListinoAsync(
        MySqlConnection connection,
        int articleOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                al.Listino,
                COALESCE(NULLIF(TRIM(l.Listino), ''), CONCAT('Listino ', al.Listino)) AS Nome
            FROM articololistino al
            INNER JOIN listino l ON l.OID = al.Listino
            WHERE al.Articolo = @articleOid
            ORDER BY
                CASE WHEN COALESCE(l.Tipolistino, 0) = 1 THEN 1 ELSE 0 END,
                CASE WHEN COALESCE(l.Predefinito, 0) = 1 THEN 0 ELSE 1 END,
                CASE WHEN COALESCE(l.Mostrainricerca, 0) = 1 THEN 0 ELSE 1 END,
                COALESCE(NULLIF(al.Quantitaminima, 0), 1),
                al.OID
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@articleOid", articleOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (null, string.Empty);
        }

        var oid = reader.IsDBNull(reader.GetOrdinal("Listino"))
            ? (int?)null
            : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Listino")));
        var nome = reader.IsDBNull(reader.GetOrdinal("Nome"))
            ? string.Empty
            : reader.GetString(reader.GetOrdinal("Nome"));
        return (oid, nome);
    }

    private static async Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailCoreAsync(
        MySqlConnection connection,
        int articleOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        int? selectedPriceListOid,
        decimal fallbackPrezzoVendita,
        CancellationToken cancellationToken)
    {
        var detail = await LoadArticlePricingMetadataAsync(connection, articleOid, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var preferredListino = selectedPriceListOid.HasValue
            ? (Oid: selectedPriceListOid, Nome: $"Listino {selectedPriceListOid.Value}")
            : await ResolvePreferredSalesListinoAsync(connection, articleOid, cancellationToken);
        var listinoPreferito = preferredListino.Oid;
        var hasVariant = varianteDettaglioOid1.HasValue || varianteDettaglioOid2.HasValue;
        List<GestionaleArticleQuantityPriceTier> fascePrezzo;
        if (hasVariant)
        {
            var fasceVariante = await LoadArticlePriceTiersAsync(
                connection,
                articleOid,
                listinoPreferito,
                varianteDettaglioOid1,
                varianteDettaglioOid2,
                cancellationToken);
            var fascePadre = await LoadArticlePriceTiersAsync(
                connection,
                articleOid,
                listinoPreferito,
                null,
                null,
                cancellationToken);
            fascePrezzo = MergeVariantAndParentPriceTiers(fasceVariante, fascePadre);
        }
        else
        {
            fascePrezzo = await LoadArticlePriceTiersAsync(
                connection,
                articleOid,
                listinoPreferito,
                null,
                null,
                cancellationToken);
        }

        if (fascePrezzo.Count == 0 && fallbackPrezzoVendita > 0)
        {
            fascePrezzo.Add(new GestionaleArticleQuantityPriceTier
            {
                QuantitaMinima = 1,
                PrezzoUnitario = fallbackPrezzoVendita
            });
        }

        return new GestionaleArticlePricingDetail
        {
            ArticoloOid = detail.ArticoloOid,
            ListinoOid = listinoPreferito,
            ListinoNome = preferredListino.Nome,
            DataFineDefault = await ResolveDefaultPriceTierExpiryAsync(connection, articleOid, listinoPreferito, cancellationToken),
            UnitaMisuraPrincipale = string.IsNullOrWhiteSpace(detail.UnitaMisuraPrincipale) ? "PZ" : detail.UnitaMisuraPrincipale,
            UnitaMisuraSecondaria = detail.UnitaMisuraSecondaria,
            MoltiplicatoreUnitaSecondaria = detail.MoltiplicatoreUnitaSecondaria,
            QuantitaMinimaVendita = detail.QuantitaMinimaVendita <= 0 ? 1 : detail.QuantitaMinimaVendita,
            QuantitaMultiplaVendita = detail.QuantitaMultiplaVendita <= 0 ? 1 : detail.QuantitaMultiplaVendita,
            FascePrezzoQuantita = fascePrezzo
                .OrderBy(item => item.QuantitaMinima)
                .ToList()
        };
    }

    private static List<GestionaleArticleQuantityPriceTier> MergeVariantAndParentPriceTiers(
        IReadOnlyList<GestionaleArticleQuantityPriceTier> fasceVariante,
        IReadOnlyList<GestionaleArticleQuantityPriceTier> fascePadre)
    {
        var merged = new Dictionary<decimal, GestionaleArticleQuantityPriceTier>();

        foreach (var fasciaPadre in fascePadre)
        {
            merged[fasciaPadre.QuantitaMinima] = fasciaPadre;
        }

        foreach (var fasciaVariante in fasceVariante)
        {
            // Le fasce variante sovrascrivono solo la stessa soglia quantita`.
            // Le soglie mancanti continuano a ereditare il listino del padre come in FM.
            merged[fasciaVariante.QuantitaMinima] = fasciaVariante;
        }

        return merged.Values
            .OrderBy(item => item.QuantitaMinima)
            .ToList();
    }

    private static async Task<GestionaleArticlePricingDetail?> LoadArticlePricingMetadataAsync(
        MySqlConnection connection,
        int articleOid,
        CancellationToken cancellationToken)
    {
        await using var articleCommand = connection.CreateCommand();
        articleCommand.CommandText =
            """
            SELECT
                a.OID,
                UPPER(COALESCE(um.Unitadimisura, 'PZ')) AS UnitaMisuraPrincipale,
                UPPER(NULLIF(COALESCE(um2.Unitadimisura, ''), '')) AS UnitaMisuraSecondaria,
                COALESCE(
                    NULLIF(a.Moltiplicativoum, 0),
                    NULLIF(a.Moltiplicativoum2, 0),
                    0
                ) AS MoltiplicatoreUnitaSecondaria,
                COALESCE(NULLIF(a.Quantitaminimavendita, 0), 1) AS QuantitaMinimaVendita,
                COALESCE(NULLIF(a.Quantitamultiplivendita, 0), 1) AS QuantitaMultiplaVendita
            FROM articolo a
            LEFT JOIN unitadimisura um ON um.OID = a.Unitadimisura
            LEFT JOIN unitadimisura um2 ON um2.OID = a.Unitadimisura2
            WHERE a.OID = @articleOid
            LIMIT 1;
            """;
        articleCommand.Parameters.AddWithValue("@articleOid", articleOid);

        await using var articleReader = await articleCommand.ExecuteReaderAsync(cancellationToken);
        if (!await articleReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new GestionaleArticlePricingDetail
        {
            ArticoloOid = articleOid,
            UnitaMisuraPrincipale = articleReader.IsDBNull(articleReader.GetOrdinal("UnitaMisuraPrincipale"))
                ? "PZ"
                : articleReader.GetString(articleReader.GetOrdinal("UnitaMisuraPrincipale")),
            UnitaMisuraSecondaria = articleReader.IsDBNull(articleReader.GetOrdinal("UnitaMisuraSecondaria"))
                ? null
                : articleReader.GetString(articleReader.GetOrdinal("UnitaMisuraSecondaria")),
            MoltiplicatoreUnitaSecondaria = articleReader.IsDBNull(articleReader.GetOrdinal("MoltiplicatoreUnitaSecondaria"))
                ? 0
                : Convert.ToDecimal(articleReader.GetValue(articleReader.GetOrdinal("MoltiplicatoreUnitaSecondaria"))),
            QuantitaMinimaVendita = articleReader.IsDBNull(articleReader.GetOrdinal("QuantitaMinimaVendita"))
                ? 1
                : Convert.ToDecimal(articleReader.GetValue(articleReader.GetOrdinal("QuantitaMinimaVendita"))),
            QuantitaMultiplaVendita = articleReader.IsDBNull(articleReader.GetOrdinal("QuantitaMultiplaVendita"))
                ? 1
                : Convert.ToDecimal(articleReader.GetValue(articleReader.GetOrdinal("QuantitaMultiplaVendita")))
        };
    }

    private static async Task<List<GestionaleArticleQuantityPriceTier>> LoadArticlePriceTiersAsync(
        MySqlConnection connection,
        int articleOid,
        int? preferredListinoOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        CancellationToken cancellationToken)
    {
        var fascePrezzo = new List<GestionaleArticleQuantityPriceTier>();
        var hasVariant = varianteDettaglioOid1.HasValue || varianteDettaglioOid2.HasValue;

        await using var priceCommand = connection.CreateCommand();
        priceCommand.CommandText =
            """
            SELECT
                COALESCE(NULLIF(al.Quantitaminima, 0), 1) AS QuantitaMinima,
                MAX(COALESCE(al.Valoreivato, al.Valore, 0)) AS PrezzoUnitario,
                MAX(al.Datafine) AS DataFine
            FROM articololistino al
            WHERE al.Articolo = @articleOid
              AND (@preferredListinoOid IS NULL OR al.Listino = @preferredListinoOid)
              AND (
                    (@hasVariant = 1
                        AND COALESCE(al.Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
                        AND COALESCE(al.Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0))
                 OR (@hasVariant = 0
                        AND al.Variantedettaglio1 IS NULL
                        AND al.Variantedettaglio2 IS NULL)
              )
            GROUP BY COALESCE(NULLIF(al.Quantitaminima, 0), 1)
            ORDER BY QuantitaMinima;
            """;
        priceCommand.Parameters.AddWithValue("@articleOid", articleOid);
        priceCommand.Parameters.AddWithValue("@preferredListinoOid", preferredListinoOid.HasValue ? preferredListinoOid.Value : DBNull.Value);
        priceCommand.Parameters.AddWithValue("@hasVariant", hasVariant ? 1 : 0);
        priceCommand.Parameters.AddWithValue("@varianteDettaglioOid1", varianteDettaglioOid1.HasValue ? varianteDettaglioOid1.Value : DBNull.Value);
        priceCommand.Parameters.AddWithValue("@varianteDettaglioOid2", varianteDettaglioOid2.HasValue ? varianteDettaglioOid2.Value : DBNull.Value);

        await using var priceReader = await priceCommand.ExecuteReaderAsync(cancellationToken);
        while (await priceReader.ReadAsync(cancellationToken))
        {
            var quantitaMinima = priceReader.IsDBNull(priceReader.GetOrdinal("QuantitaMinima"))
                ? 1
                : Convert.ToDecimal(priceReader.GetValue(priceReader.GetOrdinal("QuantitaMinima")));
            var prezzoUnitario = priceReader.IsDBNull(priceReader.GetOrdinal("PrezzoUnitario"))
                ? 0
                : Convert.ToDecimal(priceReader.GetValue(priceReader.GetOrdinal("PrezzoUnitario")));
            var dataFine = priceReader.IsDBNull(priceReader.GetOrdinal("DataFine"))
                ? (DateTime?)null
                : priceReader.GetDateTime(priceReader.GetOrdinal("DataFine"));

            if (prezzoUnitario <= 0)
            {
                continue;
            }

            fascePrezzo.Add(new GestionaleArticleQuantityPriceTier
            {
                QuantitaMinima = quantitaMinima <= 0 ? 1 : quantitaMinima,
                PrezzoUnitario = prezzoUnitario,
                DataFine = dataFine
            });
        }

        return fascePrezzo;
    }

    private static GestionaleArticleLegacyListinoRowKind ResolveLegacyListinoRowKind(
        decimal quantitaMinima,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2)
    {
        var hasVariant = varianteDettaglioOid1.HasValue || varianteDettaglioOid2.HasValue;
        var isBaseQuantity = quantitaMinima <= 1;

        return (hasVariant, isBaseQuantity) switch
        {
            (false, true) => GestionaleArticleLegacyListinoRowKind.Base,
            (false, false) => GestionaleArticleLegacyListinoRowKind.Quantity,
            (true, true) => GestionaleArticleLegacyListinoRowKind.Variant,
            _ => GestionaleArticleLegacyListinoRowKind.VariantQuantity
        };
    }

    private static string BuildVariantLabel(string? variante1Desc, string? variante2Desc)
    {
        var labels = new[] { variante1Desc, variante2Desc }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        return labels.Length == 0
            ? string.Empty
            : string.Join(" / ", labels);
    }

    private static async Task<DateTime?> ResolveDefaultPriceTierExpiryAsync(
        MySqlConnection connection,
        int articleOid,
        int? preferredListinoOid,
        CancellationToken cancellationToken)
    {
        if (!preferredListinoOid.HasValue)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT al.Datafine
            FROM articololistino al
            WHERE al.Articolo = @articleOid
              AND al.Listino = @preferredListinoOid
            ORDER BY
                CASE
                    WHEN al.Variantedettaglio1 IS NULL AND al.Variantedettaglio2 IS NULL THEN 0
                    ELSE 1
                END,
                COALESCE(NULLIF(al.Quantitaminima, 0), 1),
                al.OID
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@articleOid", articleOid);
        command.Parameters.AddWithValue("@preferredListinoOid", preferredListinoOid.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToDateTime(result);
    }

    private static async Task<IReadOnlyList<GestionaleArticleLookupSpecification>> LoadArticleLookupSpecificationsAsync(
        MySqlConnection connection,
        int articoloOid,
        CancellationToken cancellationToken)
    {
        var specifications = new List<GestionaleArticleLookupSpecification>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COALESCE(NULLIF(TRIM(c.Caratteristica), ''), 'Specifica') AS NomeSpecifica,
                COALESCE(
                    NULLIF(TRIM(cv.Caratteristicavalore), ''),
                    NULLIF(TRIM(ast.Specificatecnica), '')
                ) AS ValoreSpecifica,
                COALESCE(ast.Posizione, 0) AS Posizione
            FROM articolospecificatecnica ast
            LEFT JOIN caratteristica c ON c.OID = ast.Caratteristica
            LEFT JOIN caratteristicavalore cv ON cv.OID = ast.Caratteristicavalore
            WHERE ast.Articolo = @articoloOid
            ORDER BY COALESCE(ast.Posizione, 0), ast.OID;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var nome = reader.IsDBNull(reader.GetOrdinal("NomeSpecifica"))
                ? string.Empty
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("NomeSpecifica"))).Trim();
            var valore = reader.IsDBNull(reader.GetOrdinal("ValoreSpecifica"))
                ? string.Empty
                : NormalizeLegacyText(reader.GetString(reader.GetOrdinal("ValoreSpecifica"))).Trim();

            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(valore))
            {
                continue;
            }

            specifications.Add(new GestionaleArticleLookupSpecification
            {
                Name = nome,
                Value = valore
            });
        }

        return specifications;
    }

    private static async Task<DateTime?> LoadArticleLastSaleDateAsync(
        MySqlConnection connection,
        int articoloOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT MAX(d.Data) AS UltimaVendita
            FROM documentoriga r
            INNER JOIN documento d ON d.OID = r.Documento
            WHERE r.Articolo = @articoloOid
              AND d.Modellodocumento = 27;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToDateTime(result);
    }

    private static async Task<(decimal VendutoUltimoMese, decimal VendutoUltimiTreMesi)> LoadArticleSalesStatisticsAsync(
        MySqlConnection connection,
        int articoloOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COALESCE(SUM(CASE WHEN d.Data >= DATE_SUB(CURDATE(), INTERVAL 1 MONTH) THEN COALESCE(r.Quantita, 0) ELSE 0 END), 0) AS VendutoUltimoMese,
                COALESCE(SUM(CASE WHEN d.Data >= DATE_SUB(CURDATE(), INTERVAL 3 MONTH) THEN COALESCE(r.Quantita, 0) ELSE 0 END), 0) AS VendutoUltimiTreMesi
            FROM documentoriga r
            INNER JOIN documento d ON d.OID = r.Documento
            WHERE r.Articolo = @articoloOid
              AND d.Modellodocumento = 27
              AND d.Data >= DATE_SUB(CURDATE(), INTERVAL 3 MONTH);
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0m, 0m);
        }

        var vendutoUltimoMese = reader.IsDBNull(reader.GetOrdinal("VendutoUltimoMese"))
            ? 0m
            : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("VendutoUltimoMese")));
        var vendutoUltimiTreMesi = reader.IsDBNull(reader.GetOrdinal("VendutoUltimiTreMesi"))
            ? 0m
            : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("VendutoUltimiTreMesi")));

        return (vendutoUltimoMese, vendutoUltimiTreMesi);
    }

    private static IReadOnlyList<string> BuildArticleLookupTags(string? tagEcommerce)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in SplitLookupTags(tagEcommerce))
        {
            tags.Add(tag);
        }

        return tags.Take(10).ToList();
    }

    private static IEnumerable<string> SplitLookupTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var tag in value
                     .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(tag => !string.IsNullOrWhiteSpace(tag)))
        {
            yield return tag;
        }
    }

    private static readonly string[] AllowedImageExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".webp"];

    private static string? NormalizeImageUrl(string? value, int? imageOid, int? articoloOid, FmContentSettings settings)
    {
        if (string.IsNullOrWhiteSpace(value) && !imageOid.HasValue && !articoloOid.HasValue)
        {
            return null;
        }

        var directories = EnumerateImageDirectories(settings).ToArray();
        if (directories.Length == 0)
        {
            return null;
        }

        foreach (var candidateName in EnumeratePreferredImageNames(value, imageOid, articoloOid))
        {
            var byPreferredName = TryResolveImageByName(candidateName, directories);
            if (byPreferredName is not null)
            {
                return byPreferredName;
            }
        }

        foreach (var numericCandidate in EnumerateLegacyLocalImageKeys(imageOid, articoloOid))
        {
            var byNumericName = TryResolveImageByBaseName(numericCandidate, directories);
            if (byNumericName is not null)
            {
                return byNumericName;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateImageDirectories(FmContentSettings settings)
    {
        var rootDirectory = string.IsNullOrWhiteSpace(settings.RootDirectory)
            ? @"C:\Facile Manager\DILTECH"
            : settings.RootDirectory.Trim();
        var configuredImagesDirectory = string.IsNullOrWhiteSpace(settings.ArticleImagesDirectory)
            ? Path.Combine(rootDirectory, "Immagini")
            : settings.ArticleImagesDirectory.Trim();

        foreach (var directory in new[]
                 {
                     configuredImagesDirectory,
                     Path.Combine(rootDirectory, "Immagini", "Articolo"),
                     Path.Combine(rootDirectory, "Immagini")
                 })
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                yield return directory;
            }
        }
    }

    private static IEnumerable<string> EnumeratePreferredImageNames(string? rawValue, int? imageOid, int? articoloOid)
    {
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var normalized = rawValue.Trim();
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri) || absoluteUri.IsFile)
            {
                var fileName = Path.GetFileName(normalized.Replace('/', Path.DirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    yield return fileName;
                }
            }
        }

        if (imageOid.HasValue)
        {
            foreach (var extension in AllowedImageExtensions)
            {
                yield return $"{imageOid.Value}{extension}";
            }
        }

        if (!imageOid.HasValue && articoloOid.HasValue)
        {
            foreach (var extension in AllowedImageExtensions)
            {
                yield return $"{articoloOid.Value}{extension}";
            }
        }
    }

    private static IEnumerable<string> EnumerateLegacyLocalImageKeys(int? imageOid, int? articoloOid)
    {
        if (imageOid.HasValue)
        {
            yield return imageOid.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!imageOid.HasValue && articoloOid.HasValue)
        {
            yield return articoloOid.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string? TryResolveImageByName(string fileName, IReadOnlyList<string> directories)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryResolveImageByBaseName(string baseName, IReadOnlyList<string> directories)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        foreach (var directory in directories)
        {
            var match = AllowedImageExtensions
                .Select(extension => Path.Combine(directory, $"{baseName}{extension}"))
                .FirstOrDefault(File.Exists);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string NormalizeLegacyText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var normalized = value.Trim();
        if (!ContainsSuspiciousEncodingArtifacts(normalized))
        {
            return normalized;
        }

        var current = normalized;
        for (var i = 0; i < 6 && ContainsSuspiciousEncodingArtifacts(current); i++)
        {
            var repaired = TryRepairUtf8Mojibake(current);
            if (repaired == current || ScoreEncodingArtifacts(repaired) > ScoreEncodingArtifacts(current))
            {
                break;
            }

            current = repaired;
        }

        return current.Replace("Â", " ").Trim();
    }

    private static bool ContainsSuspiciousEncodingArtifacts(string value)
    {
        return value.IndexOf('Ã') >= 0
            || value.IndexOf('â') >= 0
            || value.IndexOf('�') >= 0
            || value.IndexOf('Â') >= 0
            || value.IndexOf('Ë') >= 0;
    }

    private static string TryRepairUtf8Mojibake(string value)
    {
        try
        {
            var windows1252Bytes = Encoding.GetEncoding(1252).GetBytes(value);
            return Encoding.UTF8.GetString(windows1252Bytes);
        }
        catch
        {
            return value;
        }
    }

    private static int ScoreEncodingArtifacts(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var score = 0;
        foreach (var ch in value)
        {
            if (ch is 'Ã' or 'â' or '�' or 'Â' or 'Ë' or '?')
            {
                score++;
            }
        }

        return score;
    }

    private static IReadOnlyList<GestionaleArticleSearchResult> DeduplicateResults(
        IReadOnlyList<GestionaleArticleSearchResult> results,
        int maxResults)
    {
        return results
            .GroupBy(result => new
            {
                result.Oid,
                Codice = result.CodiceArticolo.Trim(),
                Descrizione = result.Descrizione.Trim(),
                VarianteNome = (result.VarianteNome ?? string.Empty).Trim(),
                VarianteDescrizione = (result.VarianteDescrizione ?? string.Empty).Trim()
            })
            .Select(group => group
                .OrderByDescending(item => item.Giacenza)
                .ThenByDescending(item => item.PrezzoVendita)
                .First())
            .Take(maxResults)
            .ToList();
    }

    private static IReadOnlyList<GestionaleArticleSearchResult> CollapseVariantsToParentArticles(
        IReadOnlyList<GestionaleArticleSearchResult> results)
    {
        return results
            .GroupBy(result => new
            {
                result.Oid,
                Codice = (result.CodiceArticolo ?? string.Empty).Trim(),
                Descrizione = (result.Descrizione ?? string.Empty).Trim()
            })
            .Select(group =>
            {
                var canonicalRow = group
                    .OrderByDescending(IsCanonicalParentSearchRow)
                    .ThenByDescending(item => item.Giacenza)
                    .ThenByDescending(item => item.PrezzoVendita)
                    .First();

                if (IsCanonicalParentSearchRow(canonicalRow))
                {
                    return new GestionaleArticleSearchResult
                    {
                        Oid = canonicalRow.Oid,
                        CodiceArticolo = canonicalRow.CodiceArticolo,
                        Descrizione = canonicalRow.Descrizione,
                        PrezzoVendita = canonicalRow.PrezzoVendita,
                        Giacenza = canonicalRow.Giacenza,
                        IvaOid = canonicalRow.IvaOid,
                        AliquotaIva = canonicalRow.AliquotaIva,
                        TipoArticoloOid = canonicalRow.TipoArticoloOid,
                        ArticoloPadreOid = canonicalRow.ArticoloPadreOid,
                        BarcodeAlternativo = canonicalRow.BarcodeAlternativo,
                        VarianteDettaglioOid1 = canonicalRow.VarianteDettaglioOid1,
                        VarianteDettaglioOid2 = canonicalRow.VarianteDettaglioOid2,
                        VarianteNome = canonicalRow.VarianteNome,
                        VarianteDescrizione = canonicalRow.VarianteDescrizione,
                        HasVariantChildren = group.Any(HasVisibleVariantInfo)
                    };
                }

                return new GestionaleArticleSearchResult
                {
                    Oid = canonicalRow.Oid,
                    CodiceArticolo = canonicalRow.CodiceArticolo,
                    Descrizione = canonicalRow.Descrizione,
                    PrezzoVendita = canonicalRow.PrezzoVendita,
                    Giacenza = canonicalRow.Giacenza,
                    IvaOid = canonicalRow.IvaOid,
                    AliquotaIva = canonicalRow.AliquotaIva,
                    TipoArticoloOid = canonicalRow.TipoArticoloOid,
                    ArticoloPadreOid = null,
                    BarcodeAlternativo = null,
                    VarianteDettaglioOid1 = null,
                    VarianteDettaglioOid2 = null,
                    VarianteNome = null,
                    VarianteDescrizione = null,
                    HasVariantChildren = group.Any(HasVisibleVariantInfo)
                };
            })
            .ToList();
    }

    private static bool IsCanonicalParentSearchRow(GestionaleArticleSearchResult result)
    {
        return !result.ArticoloPadreOid.HasValue &&
               !result.VarianteDettaglioOid1.HasValue &&
               !result.VarianteDettaglioOid2.HasValue &&
               string.IsNullOrWhiteSpace(result.VarianteNome) &&
               string.IsNullOrWhiteSpace(result.VarianteDescrizione) &&
               string.IsNullOrWhiteSpace(result.BarcodeAlternativo);
    }

    private static IReadOnlyList<GestionaleArticleSearchResult> SortResults(
        IReadOnlyList<GestionaleArticleSearchResult> results,
        string normalizedSearch,
        IReadOnlyList<string> searchTerms,
        bool isBarcodeSearch)
    {
        return results
            .OrderByDescending(result => CalculateSearchScore(result, normalizedSearch, searchTerms, isBarcodeSearch))
            .ThenByDescending(result => IsExactAlternativeBarcodeMatch(result, normalizedSearch))
            .ThenByDescending(result => isBarcodeSearch && result.IsVariante)
            .ThenBy(result => result.CodiceArticolo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Descrizione, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.VarianteLabel, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(result => result.VarianteDettaglioOid1.HasValue || result.VarianteDettaglioOid2.HasValue)
            .ThenByDescending(result => result.Giacenza)
            .ToList();
    }

    private static bool HasVisibleVariantInfo(GestionaleArticleSearchResult result)
    {
        return result.IsVariante ||
               !string.IsNullOrWhiteSpace(result.VarianteNome) ||
               !string.IsNullOrWhiteSpace(result.VarianteDescrizione) ||
               result.VarianteDettaglioOid1.HasValue ||
               result.VarianteDettaglioOid2.HasValue;
    }

    private static IReadOnlyList<string> SplitSearchTerms(string searchText)
    {
        return Regex
            .Split(searchText, @"[^0-9A-Za-z]+")
            .Select(term => term.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsBarcodeSearch(string searchText)
    {
        return !string.IsNullOrWhiteSpace(searchText) &&
               searchText.Length >= 8 &&
               searchText.Length <= 18 &&
               searchText.All(char.IsDigit);
    }

    private static bool IsExactAlternativeBarcodeMatch(GestionaleArticleSearchResult result, string normalizedSearch)
    {
        return !string.IsNullOrWhiteSpace(result.BarcodeAlternativo) &&
               result.BarcodeAlternativo.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateSearchScore(
        GestionaleArticleSearchResult result,
        string normalizedSearch,
        IReadOnlyList<string> searchTerms,
        bool isBarcodeSearch)
    {
        var score = 0;
        var codice = result.CodiceArticolo ?? string.Empty;
        var descrizione = result.Descrizione ?? string.Empty;
        var variante = result.VarianteLabel;
        var barcodeAlternativo = result.BarcodeAlternativo ?? string.Empty;
        var combinedText = string.Join(" ", new[] { codice, descrizione, variante, barcodeAlternativo }).Trim();
        var compactSearch = CollapseSearchText(normalizedSearch);
        var compactCombined = CollapseSearchText(combinedText);

        if (string.Equals(codice, normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            score += 1200;
        }

        if (string.Equals(barcodeAlternativo, normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            score += 1500;
        }

        if (combinedText.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            score += 600;
        }

        if (!string.IsNullOrWhiteSpace(compactSearch) &&
            compactCombined.Contains(compactSearch, StringComparison.OrdinalIgnoreCase))
        {
            score += 400;
        }

        foreach (var term in searchTerms)
        {
            if (codice.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 140;
            }

            if (descrizione.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 120;
            }

            if (variante.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 90;
            }

            if (barcodeAlternativo.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 160;
            }
        }

        if (isBarcodeSearch && result.IsVariante)
        {
            score += 120;
        }

        if (result.Giacenza > 0)
        {
            score += 25;
        }

        return score;
    }

    private static string CollapseSearchText(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit));
    }

    private static string BuildVatOptionLabel(string codice, string descrizioneIva, decimal percentuale)
    {
        var normalizedCode = NormalizeLegacyText(codice);
        var normalizedDescription = NormalizeLegacyText(descrizioneIva);

        if (!string.IsNullOrWhiteSpace(normalizedCode) && !string.IsNullOrWhiteSpace(normalizedDescription))
        {
            if (normalizedDescription.StartsWith(normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedDescription;
            }

            return $"{normalizedCode} - {normalizedDescription}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return normalizedDescription;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCode))
        {
            return percentuale > 0
                ? $"{normalizedCode} - {percentuale:0.##}%"
                : normalizedCode;
        }

        return $"{percentuale:0.##}%";
    }

    private static string BuildTokenizedSearchCondition(IReadOnlyList<string> searchTerms, params string[] columns)
    {
        if (searchTerms.Count == 0 || columns.Length == 0)
        {
            return "1 = 1";
        }

        var termConditions = new List<string>(searchTerms.Count);
        for (var index = 0; index < searchTerms.Count; index++)
        {
            var perColumnConditions = columns
                .Select(column => $"{column} LIKE @term{index}")
                .ToArray();

            termConditions.Add($"({string.Join(" OR ", perColumnConditions)})");
        }

        return string.Join(" AND ", termConditions);
    }

    private static void AddSearchTermParameters(MySqlCommand command, IReadOnlyList<string> searchTerms)
    {
        for (var index = 0; index < searchTerms.Count; index++)
        {
            command.Parameters.AddWithValue($"@term{index}", $"%{searchTerms[index]}%");
        }
    }

    public async Task<IReadOnlyList<ArticleImageRecord>> GetArticleImagesAsync(
        int articoloOid,
        CancellationToken cancellationToken = default)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                ai.OID,
                ai.Articolo,
                ai.Variantedettaglio,
                COALESCE(ai.Predefinita, 0)  AS Predefinita,
                COALESCE(ai.Posizione, 0)    AS Posizione,
                COALESCE(ai.Descrizione, '') AS Descrizione,
                COALESCE(ai.Fonteimmagine, '') AS Fonteimmagine,
                COALESCE(vd.Variantedettaglio, '') AS VarianteLabel
            FROM articoloimmagine ai
            LEFT JOIN variantedettaglio vd ON vd.OID = ai.Variantedettaglio
            WHERE ai.Articolo = @articoloOid
            ORDER BY
                CASE WHEN COALESCE(ai.Predefinita, 0) = 1 THEN 0 ELSE 1 END,
                COALESCE(ai.Posizione, 0),
                ai.OID;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        var results = new List<ArticleImageRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fonteimmagine = reader.GetString(reader.GetOrdinal("Fonteimmagine"));
            var varianteOid = reader.IsDBNull(reader.GetOrdinal("Variantedettaglio"))
                ? (int?)null
                : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Variantedettaglio")));

            results.Add(new ArticleImageRecord
            {
                Oid = reader.GetInt32(reader.GetOrdinal("OID")),
                ArticoloOid = articoloOid,
                VariantedettaglioOid = varianteOid,
                VariantedettaglioLabel = reader.GetString(reader.GetOrdinal("VarianteLabel")),
                Predefinita = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Predefinita"))) != 0,
                Posizione = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Posizione"))),
                Descrizione = NormalizeLegacyText(reader.GetString(reader.GetOrdinal("Descrizione"))),
                Fonteimmagine = fonteimmagine,
                LocalPath = NormalizeImageUrl(
                    fonteimmagine,
                    reader.GetInt32(reader.GetOrdinal("OID")),
                    articoloOid,
                    settings.FmContent)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<GestionaleLookupOption>> GetArticleVariantedettaglioOptionsAsync(
        int articoloOid,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT vd.OID, COALESCE(vd.Variantedettaglio, '') AS Label
            FROM variantedettaglio vd
            WHERE vd.OID IN (
                SELECT Variantedettaglio1
                FROM articolomulticodice
                WHERE Articolo = @articoloOid
                  AND Variantedettaglio1 IS NOT NULL
                UNION
                SELECT Variantedettaglio2
                FROM articolomulticodice
                WHERE Articolo = @articoloOid
                  AND Variantedettaglio2 IS NOT NULL
            )
            ORDER BY Label;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        var results = new List<GestionaleLookupOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionaleLookupOption
            {
                Oid = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("OID"))),
                Label = reader.GetString(reader.GetOrdinal("Label"))
            });
        }

        return results;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }
}
