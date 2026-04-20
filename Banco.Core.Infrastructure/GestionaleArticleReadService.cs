using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using MySqlConnector;
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
                            "a.Descrizionearticolo"),
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
                            "a.Descrizionearticolo"),
                        PreferredSalesPriceExpression),
                    searchTerms,
                    selectedPriceListOid,
                    maxResults,
                    cancellationToken));
        }

        return DeduplicateResults(
            SortResults(aggregatedResults, normalizedSearch, searchTerms, isBarcodeSearch),
            maxResults);
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
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : reader.GetString(codiceOrdinal),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : reader.GetString(descrizioneOrdinal),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                BarcodeAlternativo = reader.IsDBNull(barcodeAlternativoOrdinal) ? null : reader.GetString(barcodeAlternativoOrdinal),
                VarianteDettaglioOid1 = reader.IsDBNull(varianteDettaglioOid1Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid1Ordinal)),
                VarianteDettaglioOid2 = reader.IsDBNull(varianteDettaglioOid2Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid2Ordinal)),
                VarianteNome = reader.IsDBNull(varianteNomeOrdinal) ? null : reader.GetString(varianteNomeOrdinal),
                VarianteDescrizione = reader.IsDBNull(varianteOrdinal) ? null : reader.GetString(varianteOrdinal)
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
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : reader.GetString(codiceOrdinal),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : reader.GetString(descrizioneOrdinal),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                BarcodeAlternativo = reader.IsDBNull(barcodeAlternativoOrdinal) ? null : reader.GetString(barcodeAlternativoOrdinal),
                VarianteDettaglioOid1 = reader.IsDBNull(varianteDettaglioOid1Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid1Ordinal)),
                VarianteDettaglioOid2 = reader.IsDBNull(varianteDettaglioOid2Ordinal) ? null : Convert.ToInt32(reader.GetValue(varianteDettaglioOid2Ordinal)),
                VarianteNome = reader.IsDBNull(varianteNomeOrdinal) ? null : reader.GetString(varianteNomeOrdinal),
                VarianteDescrizione = reader.IsDBNull(varianteOrdinal) ? null : reader.GetString(varianteOrdinal)
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
                CodiceArticolo = reader.IsDBNull(codiceOrdinal) ? string.Empty : reader.GetString(codiceOrdinal),
                Descrizione = reader.IsDBNull(descrizioneOrdinal) ? string.Empty : reader.GetString(descrizioneOrdinal),
                PrezzoVendita = reader.IsDBNull(prezzoOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(prezzoOrdinal)),
                Giacenza = reader.IsDBNull(giacenzaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(giacenzaOrdinal)),
                IvaOid = reader.IsDBNull(ivaOrdinal) ? 0 : Convert.ToInt32(reader.GetValue(ivaOrdinal)),
                AliquotaIva = reader.IsDBNull(aliquotaOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(aliquotaOrdinal)),
                TipoArticoloOid = reader.IsDBNull(tipoArticoloOrdinal) ? null : Convert.ToInt32(reader.GetValue(tipoArticoloOrdinal)),
                ArticoloPadreOid = reader.IsDBNull(padreOrdinal) ? null : Convert.ToInt32(reader.GetValue(padreOrdinal)),
                VarianteDescrizione = reader.IsDBNull(varianteOrdinal) ? null : reader.GetString(varianteOrdinal)
            });
        }

        return results;
    }

    private static async Task<int?> ResolvePreferredSalesListinoOidAsync(
        MySqlConnection connection,
        int articleOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT al.Listino
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

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt32(result);
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

        var listinoPreferito = selectedPriceListOid ?? await ResolvePreferredSalesListinoOidAsync(connection, articleOid, cancellationToken);
        var fascePrezzo = await LoadArticlePriceTiersAsync(
            connection,
            articleOid,
            listinoPreferito,
            varianteDettaglioOid1,
            varianteDettaglioOid2,
            cancellationToken);

        if (fascePrezzo.Count == 0 && (varianteDettaglioOid1.HasValue || varianteDettaglioOid2.HasValue))
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
                MAX(COALESCE(al.Valoreivato, al.Valore, 0)) AS PrezzoUnitario
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

            if (prezzoUnitario <= 0)
            {
                continue;
            }

            fascePrezzo.Add(new GestionaleArticleQuantityPriceTier
            {
                QuantitaMinima = quantitaMinima <= 0 ? 1 : quantitaMinima,
                PrezzoUnitario = prezzoUnitario
            });
        }

        return fascePrezzo;
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
            .ThenByDescending(result => result.VarianteDettaglioOid1.HasValue || result.VarianteDettaglioOid2.HasValue)
            .ThenByDescending(result => result.Giacenza)
            .ThenBy(result => result.CodiceArticolo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.VarianteLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }
}
