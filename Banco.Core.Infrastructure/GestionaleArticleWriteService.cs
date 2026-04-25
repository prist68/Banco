using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleArticleWriteService : IGestionaleArticleWriteService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleArticleWriteService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task SaveArticleAsync(
        GestionaleArticleLegacyUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.ArticoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(update.ArticoloOid));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var unitaPrincipaleOid = await ResolveUnitaMisuraOidAsync(connection, transaction, update.UnitaMisuraPrincipale, cancellationToken);
        int? unitaSecondariaOid = null;
        if (!string.IsNullOrWhiteSpace(update.UnitaMisuraSecondaria))
        {
            unitaSecondariaOid = await ResolveUnitaMisuraOidAsync(connection, transaction, update.UnitaMisuraSecondaria, cancellationToken);
        }

        await using (var articleCommand = connection.CreateCommand())
        {
            articleCommand.Transaction = transaction;
            articleCommand.CommandText =
                """
                UPDATE articolo
                SET Descrizionearticolo = @descrizione,
                    Unitadimisura = @unitadimisura,
                    Unitadimisura2 = @unitadimisura2,
                    Moltiplicativoum = @moltiplicatore,
                    Moltiplicativoum2 = @moltiplicatore,
                    Quantitaminimavendita = @quantitaMinima,
                    Quantitamultiplivendita = @quantitaMultipla
                WHERE OID = @articoloOid;
                """;
            articleCommand.Parameters.AddWithValue("@descrizione", NormalizeText(update.DescrizioneArticolo));
            articleCommand.Parameters.AddWithValue("@unitadimisura", unitaPrincipaleOid);
            articleCommand.Parameters.AddWithValue("@unitadimisura2", unitaSecondariaOid.HasValue ? unitaSecondariaOid.Value : DBNull.Value);
            articleCommand.Parameters.AddWithValue("@moltiplicatore", update.MoltiplicatoreUnitaSecondaria.HasValue ? update.MoltiplicatoreUnitaSecondaria.Value : 0m);
            articleCommand.Parameters.AddWithValue("@quantitaMinima", NormalizePositive(update.QuantitaMinimaVendita));
            articleCommand.Parameters.AddWithValue("@quantitaMultipla", NormalizePositive(update.QuantitaMultiplaVendita));
            articleCommand.Parameters.AddWithValue("@articoloOid", update.ArticoloOid);
            await articleCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var listinoOid = await ResolvePreferredSalesListinoOidAsync(connection, transaction, update.ArticoloOid, cancellationToken);
        if (listinoOid.HasValue)
        {
            await UpsertPreferredPriceAsync(connection, transaction, update, listinoOid.Value, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveQuantityPriceTiersAsync(
        GestionaleArticleLegacyOffersUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.ArticoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(update.ArticoloOid));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var listinoOid = await ResolvePreferredSalesListinoOidAsync(connection, transaction, update.ArticoloOid, cancellationToken);
        if (!listinoOid.HasValue)
        {
            throw new InvalidOperationException("Nessun listino legacy di vendita disponibile per l'articolo selezionato.");
        }

        await SyncQuantityPriceTiersAsync(connection, transaction, update, listinoOid.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveArticleTagsAsync(
        int articoloOid,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(articoloOid));
        }

        var normalizedTags = NormalizeTagsText(tags);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE articolo
            SET Tagecommerce = @tags
            WHERE OID = @articoloOid;
            """;
        command.Parameters.AddWithValue("@tags", normalizedTags);
        command.Parameters.AddWithValue("@articoloOid", articoloOid);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task PropagateQuantityPriceTierAsync(
        GestionaleArticleLegacyOfferPropagationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArticoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ArticoloOid));
        }

        if (request.Targets.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var listinoOid = await ResolvePreferredSalesListinoOidAsync(connection, transaction, request.ArticoloOid, cancellationToken);
        if (!listinoOid.HasValue)
        {
            throw new InvalidOperationException("Nessun listino legacy di vendita disponibile per l'articolo selezionato.");
        }

        var defaultDataFine = await ResolveDefaultPriceTierExpiryAsync(
            connection,
            transaction,
            request.ArticoloOid,
            listinoOid.Value,
            cancellationToken);

        foreach (var target in request.Targets)
        {
            await UpsertQuantityPriceTierAsync(
                connection,
                transaction,
                request.ArticoloOid,
                listinoOid.Value,
                target.VarianteDettaglioOid1,
                target.VarianteDettaglioOid2,
                new GestionaleArticleLegacyPriceTierUpdate
                {
                    QuantitaMinima = request.PriceTier.QuantitaMinima,
                    PrezzoNetto = request.PriceTier.PrezzoNetto,
                    PrezzoIvato = request.PriceTier.PrezzoIvato,
                    DataFine = request.PriceTier.DataFine ?? defaultDataFine
                },
                request.AliquotaIva,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }

    private static async Task<int> ResolveUnitaMisuraOidAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string codice,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT OID
            FROM unitadimisura
            WHERE UPPER(TRIM(Unitadimisura)) = @codice
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@codice", NormalizeCode(codice));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            throw new InvalidOperationException($"Unita` di misura legacy non trovata: {codice}.");
        }

        return Convert.ToInt32(result);
    }

    private static async Task<int?> ResolvePreferredSalesListinoOidAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int articoloOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT al.Listino
            FROM articololistino al
            INNER JOIN listino l ON l.OID = al.Listino
            WHERE al.Articolo = @articoloOid
            ORDER BY
                CASE WHEN COALESCE(l.Tipolistino, 0) = 1 THEN 1 ELSE 0 END,
                CASE WHEN COALESCE(l.Predefinito, 0) = 1 THEN 0 ELSE 1 END,
                CASE WHEN COALESCE(l.Mostrainricerca, 0) = 1 THEN 0 ELSE 1 END,
                COALESCE(NULLIF(al.Quantitaminima, 0), 1),
                al.OID
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt32(result);
    }

    private static async Task UpsertPreferredPriceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        GestionaleArticleLegacyUpdate update,
        int listinoOid,
        CancellationToken cancellationToken)
    {
        await using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText =
            """
            SELECT OID
            FROM articololistino
            WHERE Articolo = @articoloOid
              AND Listino = @listinoOid
              AND COALESCE(Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
              AND COALESCE(Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0)
              AND COALESCE(NULLIF(Quantitaminima, 0), 1) = 1
            ORDER BY OID
            LIMIT 1;
            """;
        findCommand.Parameters.AddWithValue("@articoloOid", update.ArticoloOid);
        findCommand.Parameters.AddWithValue("@listinoOid", listinoOid);
        findCommand.Parameters.AddWithValue("@varianteDettaglioOid1", update.VarianteDettaglioOid1.HasValue ? update.VarianteDettaglioOid1.Value : DBNull.Value);
        findCommand.Parameters.AddWithValue("@varianteDettaglioOid2", update.VarianteDettaglioOid2.HasValue ? update.VarianteDettaglioOid2.Value : DBNull.Value);

        var rowOid = await findCommand.ExecuteScalarAsync(cancellationToken);
        if (rowOid is null || rowOid == DBNull.Value)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO articololistino (
                    Articolo, Listino, Variantedettaglio1, Variantedettaglio2,
                    Quantitaminima, Valore, Valoreivato)
                VALUES (
                    @articoloOid, @listinoOid, @varianteDettaglioOid1, @varianteDettaglioOid2,
                    1, @prezzo, @prezzo);
                """;
            insertCommand.Parameters.AddWithValue("@articoloOid", update.ArticoloOid);
            insertCommand.Parameters.AddWithValue("@listinoOid", listinoOid);
            insertCommand.Parameters.AddWithValue("@varianteDettaglioOid1", update.VarianteDettaglioOid1.HasValue ? update.VarianteDettaglioOid1.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@varianteDettaglioOid2", update.VarianteDettaglioOid2.HasValue ? update.VarianteDettaglioOid2.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@prezzo", update.PrezzoVendita);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE articololistino
            SET Valore = @prezzo,
                Valoreivato = @prezzo
            WHERE OID = @oid;
            """;
        updateCommand.Parameters.AddWithValue("@prezzo", update.PrezzoVendita);
        updateCommand.Parameters.AddWithValue("@oid", Convert.ToInt32(rowOid));
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SyncQuantityPriceTiersAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        GestionaleArticleLegacyOffersUpdate update,
        int listinoOid,
        CancellationToken cancellationToken)
    {
        var defaultDataFine = await ResolveDefaultPriceTierExpiryAsync(
            connection,
            transaction,
            update.ArticoloOid,
            listinoOid,
            cancellationToken);

        var normalizedTiers = update.PriceTiers
            .Where(item => item.QuantitaMinima > 1 && item.PrezzoIvato > 0)
            .GroupBy(item => NormalizePositive(item.QuantitaMinima))
            .Select(group => group
                .OrderByDescending(item => item.PrezzoIvato)
                .First())
            .Select(item => new GestionaleArticleLegacyPriceTierUpdate
            {
                QuantitaMinima = item.QuantitaMinima,
                PrezzoNetto = item.PrezzoNetto,
                PrezzoIvato = item.PrezzoIvato,
                DataFine = item.DataFine ?? defaultDataFine
            })
            .OrderBy(item => item.QuantitaMinima)
            .ToList();

        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText =
            """
            SELECT OID, COALESCE(NULLIF(Quantitaminima, 0), 1) AS QuantitaMinima
            FROM articololistino
            WHERE Articolo = @articoloOid
              AND Listino = @listinoOid
              AND COALESCE(Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
              AND COALESCE(Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0)
              AND COALESCE(NULLIF(Quantitaminima, 0), 1) > 1
            ORDER BY QuantitaMinima, OID;
            """;
        selectCommand.Parameters.AddWithValue("@articoloOid", update.ArticoloOid);
        selectCommand.Parameters.AddWithValue("@listinoOid", listinoOid);
        selectCommand.Parameters.AddWithValue("@varianteDettaglioOid1", update.VarianteDettaglioOid1.HasValue ? update.VarianteDettaglioOid1.Value : DBNull.Value);
        selectCommand.Parameters.AddWithValue("@varianteDettaglioOid2", update.VarianteDettaglioOid2.HasValue ? update.VarianteDettaglioOid2.Value : DBNull.Value);

        var existingRows = new Dictionary<decimal, int>();
        await using (var reader = await selectCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var quantitaMinima = reader.IsDBNull(reader.GetOrdinal("QuantitaMinima"))
                    ? 1m
                    : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("QuantitaMinima")));
                if (quantitaMinima <= 1 || existingRows.ContainsKey(quantitaMinima))
                {
                    continue;
                }

                existingRows[quantitaMinima] = reader.GetInt32(reader.GetOrdinal("OID"));
            }
        }

        foreach (var tier in normalizedTiers)
        {
            if (existingRows.TryGetValue(tier.QuantitaMinima, out var existingOid))
            {
                await UpdateQuantityPriceTierAsync(connection, transaction, existingOid, tier, update.AliquotaIva, cancellationToken);
                existingRows.Remove(tier.QuantitaMinima);
                continue;
            }

            await InsertQuantityPriceTierAsync(
                connection,
                transaction,
                update.ArticoloOid,
                listinoOid,
                update.VarianteDettaglioOid1,
                update.VarianteDettaglioOid2,
                tier,
                update.AliquotaIva,
                cancellationToken);
        }

        foreach (var leftoverOid in existingRows.Values)
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM articololistino WHERE OID = @oid;";
            deleteCommand.Parameters.AddWithValue("@oid", leftoverOid);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertQuantityPriceTierAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int articoloOid,
        int listinoOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        GestionaleArticleLegacyPriceTierUpdate tier,
        decimal aliquotaIva,
        CancellationToken cancellationToken)
    {
        var quantitaMinima = NormalizePositive(tier.QuantitaMinima);
        if (quantitaMinima <= 1 || tier.PrezzoIvato <= 0)
        {
            return;
        }

        await using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText =
            """
            SELECT OID
            FROM articololistino
            WHERE Articolo = @articoloOid
              AND Listino = @listinoOid
              AND COALESCE(Variantedettaglio1, 0) = COALESCE(@varianteDettaglioOid1, 0)
              AND COALESCE(Variantedettaglio2, 0) = COALESCE(@varianteDettaglioOid2, 0)
              AND COALESCE(NULLIF(Quantitaminima, 0), 1) = @quantitaMinima
            ORDER BY OID
            LIMIT 1;
            """;
        findCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
        findCommand.Parameters.AddWithValue("@listinoOid", listinoOid);
        findCommand.Parameters.AddWithValue("@varianteDettaglioOid1", varianteDettaglioOid1.HasValue ? varianteDettaglioOid1.Value : DBNull.Value);
        findCommand.Parameters.AddWithValue("@varianteDettaglioOid2", varianteDettaglioOid2.HasValue ? varianteDettaglioOid2.Value : DBNull.Value);
        findCommand.Parameters.AddWithValue("@quantitaMinima", quantitaMinima);

        var existingOid = await findCommand.ExecuteScalarAsync(cancellationToken);
        if (existingOid is null || existingOid == DBNull.Value)
        {
            await InsertQuantityPriceTierAsync(
                connection,
                transaction,
                articoloOid,
                listinoOid,
                varianteDettaglioOid1,
                varianteDettaglioOid2,
                tier,
                aliquotaIva,
                cancellationToken);
            return;
        }

        await UpdateQuantityPriceTierAsync(connection, transaction, Convert.ToInt32(existingOid), tier, aliquotaIva, cancellationToken);
    }

    private static async Task InsertQuantityPriceTierAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int articoloOid,
        int listinoOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        GestionaleArticleLegacyPriceTierUpdate tier,
        decimal aliquotaIva,
        CancellationToken cancellationToken)
    {
        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            """
            INSERT INTO articololistino (
                Articolo, Listino, Variantedettaglio1, Variantedettaglio2,
                Quantitaminima, Datafine, Valore, Valoreivato)
            VALUES (
                @articoloOid, @listinoOid, @varianteDettaglioOid1, @varianteDettaglioOid2,
                @quantitaMinima, @dataFine, @prezzoNetto, @prezzoIvato);
            """;
        insertCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
        insertCommand.Parameters.AddWithValue("@listinoOid", listinoOid);
        insertCommand.Parameters.AddWithValue("@varianteDettaglioOid1", varianteDettaglioOid1.HasValue ? varianteDettaglioOid1.Value : DBNull.Value);
        insertCommand.Parameters.AddWithValue("@varianteDettaglioOid2", varianteDettaglioOid2.HasValue ? varianteDettaglioOid2.Value : DBNull.Value);
        insertCommand.Parameters.AddWithValue("@quantitaMinima", NormalizePositive(tier.QuantitaMinima));
        insertCommand.Parameters.AddWithValue("@dataFine", tier.DataFine.HasValue ? tier.DataFine.Value : DBNull.Value);
        insertCommand.Parameters.AddWithValue("@prezzoNetto", ResolveLegacyNetPrice(tier.PrezzoNetto, tier.PrezzoIvato, aliquotaIva));
        insertCommand.Parameters.AddWithValue("@prezzoIvato", tier.PrezzoIvato);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateQuantityPriceTierAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int rowOid,
        GestionaleArticleLegacyPriceTierUpdate tier,
        decimal aliquotaIva,
        CancellationToken cancellationToken)
    {
        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE articololistino
            SET Quantitaminima = @quantitaMinima,
                Datafine = @dataFine,
                Valore = @prezzoNetto,
                Valoreivato = @prezzoIvato
            WHERE OID = @oid;
            """;
        updateCommand.Parameters.AddWithValue("@quantitaMinima", NormalizePositive(tier.QuantitaMinima));
        updateCommand.Parameters.AddWithValue("@dataFine", tier.DataFine.HasValue ? tier.DataFine.Value : DBNull.Value);
        updateCommand.Parameters.AddWithValue("@prezzoNetto", ResolveLegacyNetPrice(tier.PrezzoNetto, tier.PrezzoIvato, aliquotaIva));
        updateCommand.Parameters.AddWithValue("@prezzoIvato", tier.PrezzoIvato);
        updateCommand.Parameters.AddWithValue("@oid", rowOid);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DateTime?> ResolveDefaultPriceTierExpiryAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int articoloOid,
        int listinoOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Datafine
            FROM articololistino
            WHERE Articolo = @articoloOid
              AND Listino = @listinoOid
            ORDER BY
                CASE
                    WHEN Variantedettaglio1 IS NULL AND Variantedettaglio2 IS NULL THEN 0
                    ELSE 1
                END,
                COALESCE(NULLIF(Quantitaminima, 0), 1),
                OID
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);
        command.Parameters.AddWithValue("@listinoOid", listinoOid);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return Convert.ToDateTime(result);
    }

    private static decimal ResolveLegacyNetPrice(decimal prezzoNetto, decimal prezzoIvato, decimal aliquotaIva)
    {
        if (prezzoNetto > 0)
        {
            return decimal.Round(prezzoNetto, 4, MidpointRounding.AwayFromZero);
        }

        return CalculateNetPrice(prezzoIvato, aliquotaIva);
    }

    private static decimal CalculateNetPrice(decimal prezzoIvato, decimal aliquotaIva)
    {
        if (prezzoIvato <= 0)
        {
            return 0;
        }

        if (aliquotaIva <= 0)
        {
            return decimal.Round(prezzoIvato, 4, MidpointRounding.AwayFromZero);
        }

        var divisore = 1m + (aliquotaIva / 100m);
        return divisore <= 0
            ? decimal.Round(prezzoIvato, 4, MidpointRounding.AwayFromZero)
            : decimal.Round(prezzoIvato / divisore, 4, MidpointRounding.AwayFromZero);
    }

    public async Task<ArticleImageRecord> AddArticleImageAsync(
        ArticleImageAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArticoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ArticoloOid));
        }

        if (!File.Exists(request.SourceFilePath))
        {
            throw new FileNotFoundException("File sorgente non trovato.", request.SourceFilePath);
        }

        var settings = await _configurationService.LoadAsync(cancellationToken);
        var imagesDir = ResolveImagesDirectory(settings);

        Directory.CreateDirectory(imagesDir);

        var ext = Path.GetExtension(request.SourceFilePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".png";
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int nextPosizione;
        var hasExistingImages = false;
        var nextDescrizione = string.Empty;
        await using (var posCommand = connection.CreateCommand())
        {
            posCommand.Transaction = transaction;
            posCommand.CommandText =
                """
                SELECT COALESCE(MAX(Posizione), 0)
                FROM articoloimmagine
                WHERE Articolo = @articoloOid;
                """;
            posCommand.Parameters.AddWithValue("@articoloOid", request.ArticoloOid);
            var maxPosizione = Convert.ToInt32(await posCommand.ExecuteScalarAsync(cancellationToken));
            nextPosizione = maxPosizione + 1;
        }

        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM articoloimmagine
                WHERE Articolo = @articoloOid;
                """;
            countCommand.Parameters.AddWithValue("@articoloOid", request.ArticoloOid);
            hasExistingImages = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        nextDescrizione = await ResolveNextImageDescriptionAsync(
            connection,
            transaction,
            request.ArticoloOid,
            cancellationToken);

        int newOid;
        string finalFileName = string.Empty;
        string? destPath = null;
        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO articoloimmagine (Articolo, Variantedettaglio, Descrizione, Predefinita, Posizione, Fonteimmagine)
                VALUES (@articoloOid, @variantedettaglioOid, @descrizione, @predefinita, @posizione, @fonteimmagine);
                """;
            insertCommand.Parameters.AddWithValue("@articoloOid", request.ArticoloOid);
            insertCommand.Parameters.AddWithValue("@variantedettaglioOid",
                request.VariantedettaglioOid.HasValue ? request.VariantedettaglioOid.Value : DBNull.Value);
            insertCommand.Parameters.AddWithValue("@descrizione", nextDescrizione);
            insertCommand.Parameters.AddWithValue("@predefinita", hasExistingImages ? 0 : 1);
            insertCommand.Parameters.AddWithValue("@posizione", nextPosizione);
            insertCommand.Parameters.AddWithValue("@fonteimmagine", string.Empty);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            newOid = Convert.ToInt32(insertCommand.LastInsertedId);
        }

        try
        {
            finalFileName = $"{newOid}{ext}";
            destPath = Path.Combine(imagesDir, finalFileName);
            File.Copy(request.SourceFilePath, destPath, overwrite: false);

            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE articoloimmagine
                SET Fonteimmagine = @fonteimmagine
                WHERE OID = @oid;
                """;
            updateCommand.Parameters.AddWithValue("@fonteimmagine", finalFileName);
            updateCommand.Parameters.AddWithValue("@oid", newOid);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(destPath) && File.Exists(destPath))
            {
                File.Delete(destPath);
            }

            throw;
        }

        return new ArticleImageRecord
        {
            Oid = newOid,
            ArticoloOid = request.ArticoloOid,
            VariantedettaglioOid = request.VariantedettaglioOid,
            VariantedettaglioLabel = string.Empty,
            Predefinita = !hasExistingImages,
            Posizione = nextPosizione,
            Descrizione = nextDescrizione,
            Fonteimmagine = finalFileName,
            LocalPath = destPath
        };
    }

    public async Task DeleteArticleImageAsync(
        int imageOid,
        CancellationToken cancellationToken = default)
    {
        if (imageOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageOid));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        var settings = await _configurationService.LoadAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COALESCE(Fonteimmagine, '') AS Fonteimmagine,
                Articolo
            FROM articoloimmagine
            WHERE OID = @oid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@oid", imageOid);

        string? fonteimmagine = null;
        int articoloOid;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return;
            }

            fonteimmagine = reader.GetString(reader.GetOrdinal("Fonteimmagine"));
            articoloOid = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Articolo")));
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM articoloimmagine WHERE OID = @oid;";
            deleteCommand.Parameters.AddWithValue("@oid", imageOid);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var needsNewPredefinita = false;
        if (!string.IsNullOrWhiteSpace(fonteimmagine))
        {
            await using var checkPredefinitaCommand = connection.CreateCommand();
            checkPredefinitaCommand.Transaction = transaction;
            checkPredefinitaCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM articoloimmagine
                WHERE Articolo = @articoloOid
                  AND COALESCE(Predefinita, 0) = 1;
                """;
            checkPredefinitaCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
            needsNewPredefinita = Convert.ToInt32(await checkPredefinitaCommand.ExecuteScalarAsync(cancellationToken)) == 0;
        }

        if (needsNewPredefinita)
        {
            await using var setFallbackPredefinitaCommand = connection.CreateCommand();
            setFallbackPredefinitaCommand.Transaction = transaction;
            setFallbackPredefinitaCommand.CommandText =
                """
                UPDATE articoloimmagine
                SET Predefinita = 1
                WHERE OID = (
                    SELECT OID
                    FROM (
                        SELECT OID
                        FROM articoloimmagine
                        WHERE Articolo = @articoloOid
                        ORDER BY COALESCE(Posizione, 0), OID
                        LIMIT 1
                    ) AS fallbackimage
                );
                """;
            setFallbackPredefinitaCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
            await setFallbackPredefinitaCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var fileStillReferenced = false;
        if (!string.IsNullOrWhiteSpace(fonteimmagine))
        {
            await using var referenceCommand = connection.CreateCommand();
            referenceCommand.Transaction = transaction;
            referenceCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM articoloimmagine
                WHERE Fonteimmagine = @fonteimmagine;
                """;
            referenceCommand.Parameters.AddWithValue("@fonteimmagine", fonteimmagine);
            fileStillReferenced = Convert.ToInt32(await referenceCommand.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        await transaction.CommitAsync(cancellationToken);

        if (!fileStillReferenced)
        {
            TryDeleteImageFile(imageOid, fonteimmagine, settings);
        }
    }

    public async Task SetArticleImageAsPredefinitaAsync(
        int imageOid,
        int articoloOid,
        CancellationToken cancellationToken = default)
    {
        if (imageOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageOid));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.Transaction = transaction;
            clearCommand.CommandText =
                "UPDATE articoloimmagine SET Predefinita = 0 WHERE Articolo = @articoloOid;";
            clearCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
            await clearCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var setCommand = connection.CreateCommand())
        {
            setCommand.Transaction = transaction;
            setCommand.CommandText =
                "UPDATE articoloimmagine SET Predefinita = 1 WHERE OID = @oid;";
            setCommand.Parameters.AddWithValue("@oid", imageOid);
            await setCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateArticleImageVariantAsync(
        int imageOid,
        int? variantedettaglioOid,
        CancellationToken cancellationToken = default)
    {
        if (imageOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageOid));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE articoloimmagine
            SET Variantedettaglio = @variantedettaglioOid
            WHERE OID = @oid;
            """;
        command.Parameters.AddWithValue("@variantedettaglioOid",
            variantedettaglioOid.HasValue ? variantedettaglioOid.Value : DBNull.Value);
        command.Parameters.AddWithValue("@oid", imageOid);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateArticleImagePositionsAsync(
        IReadOnlyList<(int Oid, int Posizione)> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var (oid, posizione) in updates)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "UPDATE articoloimmagine SET Posizione = @posizione WHERE OID = @oid;";
            command.Parameters.AddWithValue("@posizione", posizione);
            command.Parameters.AddWithValue("@oid", oid);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveArticleSecondaryCategoriesAsync(
        int articoloOid,
        IReadOnlyList<int> categoryOids,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(articoloOid));
        }

        ArgumentNullException.ThrowIfNull(categoryOids);

        var normalizedCategoryOids = categoryOids
            .Where(static oid => oid > 0)
            .Distinct()
            .OrderBy(static oid => oid)
            .ToList();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText =
                "DELETE FROM articolocategoria WHERE Articolo = @articoloOid;";
            deleteCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var categoryOid in normalizedCategoryOids)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO articolocategoria (Articolo, Categoria, OptimisticLockField)
                VALUES (@articoloOid, @categoriaOid, 0);
                """;
            insertCommand.Parameters.AddWithValue("@articoloOid", articoloOid);
            insertCommand.Parameters.AddWithValue("@categoriaOid", categoryOid);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string> ResolveNextImageDescriptionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int articoloOid,
        CancellationToken cancellationToken)
    {
        var baseDescription = await ResolveArticleImageDescriptionBaseAsync(
            connection,
            transaction,
            articoloOid,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(baseDescription))
        {
            baseDescription = $"Articolo {articoloOid}";
        }

        var nextIndex = 1;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COALESCE(Descrizione, '')
            FROM articoloimmagine
            WHERE Articolo = @articoloOid;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var currentDescription = NormalizeText(reader.GetString(0));
            var currentIndex = TryExtractTrailingImageIndex(baseDescription, currentDescription);
            if (currentIndex.HasValue)
            {
                nextIndex = Math.Max(nextIndex, currentIndex.Value + 1);
            }
        }

        return $"{baseDescription} {nextIndex}";
    }

    private static async Task<string> ResolveArticleImageDescriptionBaseAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int articoloOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COALESCE(Descrizionearticolo, '')
            FROM articolo
            WHERE OID = @articoloOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value
            ? string.Empty
            : NormalizeText(Convert.ToString(result));
    }

    private static int? TryExtractTrailingImageIndex(string baseDescription, string currentDescription)
    {
        if (string.IsNullOrWhiteSpace(currentDescription))
        {
            return null;
        }

        if (currentDescription.Equals(baseDescription, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (!currentDescription.StartsWith(baseDescription, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = currentDescription[baseDescription.Length..].Trim();
        return int.TryParse(suffix, out var value) && value > 0
            ? value
            : null;
    }

    private static string ResolveImagesDirectory(Banco.Vendita.Configuration.AppSettings settings)
    {
        var fmContent = settings.FmContent;
        var root = string.IsNullOrWhiteSpace(fmContent.RootDirectory)
            ? @"C:\Facile Manager\DILTECH"
            : fmContent.RootDirectory.Trim();

        return string.IsNullOrWhiteSpace(fmContent.ArticleImagesDirectory)
            ? Path.Combine(root, "Immagini")
            : fmContent.ArticleImagesDirectory.Trim();
    }

    private static void TryDeleteImageFile(int imageOid, string? fonteimmagine, Banco.Vendita.Configuration.AppSettings settings)
    {
        var imagesDirectory = ResolveImagesDirectory(settings);
        var fullPath = ResolveLocalImageFilePath(imageOid, fonteimmagine, imagesDirectory);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return;
        }

        try
        {
            File.Delete(fullPath);
        }
        catch
        {
            // La rimozione fisica non deve sporcare la coerenza del record legacy gia` eliminato.
        }
    }

    private static string? ResolveLocalImageFilePath(int imageOid, string? fonteimmagine, string imagesDirectory)
    {
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" })
        {
            var byOid = Path.Combine(imagesDirectory, $"{imageOid}{extension}");
            if (File.Exists(byOid))
            {
                return byOid;
            }
        }

        if (string.IsNullOrWhiteSpace(fonteimmagine))
        {
            return null;
        }

        var fileName = Path.GetFileName(fonteimmagine.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var byStoredName = Path.Combine(imagesDirectory, fileName);
        return File.Exists(byStoredName) ? byStoredName : null;
    }

    private static decimal NormalizePositive(decimal value) => value <= 0 ? 1 : value;

    private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();

    private static string NormalizeTagsText(IEnumerable<string> tags) =>
        string.Join(
            ", ",
            tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string NormalizeCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
