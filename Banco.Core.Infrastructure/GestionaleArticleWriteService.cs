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
        await connection.OpenAsync(cancellationToken);
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

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.GestionaleDatabase.Host,
            Port = (uint)settings.GestionaleDatabase.Port,
            Database = settings.GestionaleDatabase.Database,
            UserID = settings.GestionaleDatabase.Username,
            Password = settings.GestionaleDatabase.Password,
            CharacterSet = settings.GestionaleDatabase.CharacterSet ?? string.Empty,
            AllowUserVariables = true
        };

        return new MySqlConnection(builder.ConnectionString);
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

    private static decimal NormalizePositive(decimal value) => value <= 0 ? 1 : value;

    private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();

    private static string NormalizeCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
