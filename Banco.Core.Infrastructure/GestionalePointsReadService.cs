using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Points;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionalePointsReadService : IGestionalePointsReadService, IGestionalePointsWriteService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionalePointsReadService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<GestionalePointsCampaignSummary>> GetCampaignsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                OID,
                Attiva,
                Basecalcolo,
                Calcolosuvaloreivato,
                Europerpunto,
                Fine,
                Importominimo,
                Inizio,
                Nomeoperazione
            FROM cartafedelta
            ORDER BY Nomeoperazione, OID;
            """;

        var results = new List<GestionalePointsCampaignSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var oidOrdinal = reader.GetOrdinal("OID");
        var attivaOrdinal = reader.GetOrdinal("Attiva");
        var baseCalcoloOrdinal = reader.GetOrdinal("Basecalcolo");
        var calcoloIvaOrdinal = reader.GetOrdinal("Calcolosuvaloreivato");
        var euroPerPuntoOrdinal = reader.GetOrdinal("Europerpunto");
        var fineOrdinal = reader.GetOrdinal("Fine");
        var importoMinimoOrdinal = reader.GetOrdinal("Importominimo");
        var inizioOrdinal = reader.GetOrdinal("Inizio");
        var nomeOrdinal = reader.GetOrdinal("Nomeoperazione");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionalePointsCampaignSummary
            {
                Oid = reader.GetInt32(oidOrdinal),
                Attiva = ReadNullableBool(reader, attivaOrdinal),
                BaseCalcolo = ReadNullableString(reader, baseCalcoloOrdinal),
                CalcolaSuValoreIva = ReadNullableBool(reader, calcoloIvaOrdinal),
                EuroPerPunto = ReadNullableDecimal(reader, euroPerPuntoOrdinal),
                Fine = ReadNullableDateTime(reader, fineOrdinal),
                ImportoMinimo = ReadNullableDecimal(reader, importoMinimoOrdinal),
                Inizio = ReadNullableDateTime(reader, inizioOrdinal),
                NomeOperazione = ReadNullableString(reader, nomeOrdinal) ?? string.Empty
            });
        }

        return results;
    }

    public async Task<GestionalePointsCampaignEditModel?> GetCampaignAsync(
        int campaignOid,
        CancellationToken cancellationToken = default)
    {
        if (campaignOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                OID,
                Attiva,
                Basecalcolo,
                Calcolosuvaloreivato,
                Europerpunto,
                Fine,
                Importominimo,
                Inizio,
                Nomeoperazione,
                OptimisticLockField
            FROM cartafedelta
            WHERE OID = @campaignOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@campaignOid", campaignOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapCampaignEditModel(reader);
    }

    public async Task<int> SaveCampaignAsync(
        GestionalePointsCampaignEditModel campaign,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (campaign.IsNuovo)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText =
                    """
                    INSERT INTO cartafedelta
                    (
                        Attiva,
                        Basecalcolo,
                        Calcolosuvaloreivato,
                        Europerpunto,
                        Fine,
                        Importominimo,
                        Inizio,
                        Nomeoperazione,
                        OptimisticLockField
                    )
                    VALUES
                    (
                        @Attiva,
                        @Basecalcolo,
                        @Calcolosuvaloreivato,
                        @Europerpunto,
                        @Fine,
                        @Importominimo,
                        @Inizio,
                        @Nomeoperazione,
                        @OptimisticLockField
                    );
                    """;

                AddCampaignParameters(insertCommand, campaign);
                insertCommand.Parameters.AddWithValue("@OptimisticLockField", campaign.OptimisticLockField ?? 0);

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                var insertedOid = Convert.ToInt32(insertCommand.LastInsertedId);
                await transaction.CommitAsync(cancellationToken);
                campaign.Oid = insertedOid;
                return insertedOid;
            }

            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE cartafedelta
                SET
                    Attiva = @Attiva,
                    Basecalcolo = @Basecalcolo,
                    Calcolosuvaloreivato = @Calcolosuvaloreivato,
                    Europerpunto = @Europerpunto,
                    Fine = @Fine,
                    Importominimo = @Importominimo,
                    Inizio = @Inizio,
                    Nomeoperazione = @Nomeoperazione,
                    OptimisticLockField = COALESCE(OptimisticLockField, 0) + 1
                WHERE OID = @Oid;
                """;
            updateCommand.Parameters.AddWithValue("@Oid", campaign.Oid);
            AddCampaignParameters(updateCommand, campaign);

            var affectedRows = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException($"Campagna punti {campaign.Oid} non trovata.");
            }

            await transaction.CommitAsync(cancellationToken);
            campaign.OptimisticLockField = (campaign.OptimisticLockField ?? 0) + 1;
            return campaign.Oid;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task CancelCampaignAsync(
        int campaignOid,
        CancellationToken cancellationToken = default)
    {
        if (campaignOid <= 0)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE cartafedelta
            SET
                Attiva = 0,
                Fine = COALESCE(Fine, NOW()),
                OptimisticLockField = COALESCE(OptimisticLockField, 0) + 1
            WHERE OID = @Oid;
            """;
        command.Parameters.AddWithValue("@Oid", campaignOid);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Campagna punti {campaignOid} non trovata.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GestionalePointsArticleSummary>> GetArticlesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                OID,
                Codicearticolo,
                Descrizionearticolo,
                Europerunpunto,
                Operazionesucartafedelta,
                Puntidasottrarrecartafedeleta,
                Tracciabilita
            FROM articolo
            WHERE COALESCE(Europerunpunto, 0) <> 0
               OR COALESCE(Operazionesucartafedelta, 0) <> 0
               OR COALESCE(Puntidasottrarrecartafedeleta, 0) <> 0
            ORDER BY Descrizionearticolo, Codicearticolo;
            """;

        var results = new List<GestionalePointsArticleSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var oidOrdinal = reader.GetOrdinal("OID");
        var codiceOrdinal = reader.GetOrdinal("Codicearticolo");
        var descrizioneOrdinal = reader.GetOrdinal("Descrizionearticolo");
        var euroPerPuntoOrdinal = reader.GetOrdinal("Europerunpunto");
        var operazioneOrdinal = reader.GetOrdinal("Operazionesucartafedelta");
        var puntiDaSottrarreOrdinal = reader.GetOrdinal("Puntidasottrarrecartafedeleta");
        var tracciabilitaOrdinal = reader.GetOrdinal("Tracciabilita");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionalePointsArticleSummary
            {
                Oid = reader.GetInt32(oidOrdinal),
                CodiceArticolo = ReadNullableString(reader, codiceOrdinal) ?? string.Empty,
                Descrizione = ReadNullableString(reader, descrizioneOrdinal) ?? string.Empty,
                EuroPerPunto = ReadNullableDecimal(reader, euroPerPuntoOrdinal),
                OperazioneSuCartaFedelta = ReadNullableBool(reader, operazioneOrdinal),
                PuntiDaSottrarre = ReadNullableInt32(reader, puntiDaSottrarreOrdinal),
                Tracciabilita = ReadNullableInt32(reader, tracciabilitaOrdinal)
            });
        }

        return results;
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }

    private static GestionalePointsCampaignEditModel MapCampaignEditModel(MySqlDataReader reader)
    {
        var oidOrdinal = reader.GetOrdinal("OID");
        var attivaOrdinal = reader.GetOrdinal("Attiva");
        var baseCalcoloOrdinal = reader.GetOrdinal("Basecalcolo");
        var calcoloIvaOrdinal = reader.GetOrdinal("Calcolosuvaloreivato");
        var euroPerPuntoOrdinal = reader.GetOrdinal("Europerpunto");
        var fineOrdinal = reader.GetOrdinal("Fine");
        var importoMinimoOrdinal = reader.GetOrdinal("Importominimo");
        var inizioOrdinal = reader.GetOrdinal("Inizio");
        var nomeOrdinal = reader.GetOrdinal("Nomeoperazione");
        var optimisticLockOrdinal = reader.GetOrdinal("OptimisticLockField");

        return new GestionalePointsCampaignEditModel
        {
            Oid = reader.GetInt32(oidOrdinal),
            Attiva = ReadNullableBool(reader, attivaOrdinal),
            BaseCalcolo = ReadNullableString(reader, baseCalcoloOrdinal),
            CalcolaSuValoreIva = ReadNullableBool(reader, calcoloIvaOrdinal),
            EuroPerPunto = ReadNullableDecimal(reader, euroPerPuntoOrdinal),
            Fine = ReadNullableDateTime(reader, fineOrdinal),
            ImportoMinimo = ReadNullableDecimal(reader, importoMinimoOrdinal),
            Inizio = ReadNullableDateTime(reader, inizioOrdinal),
            NomeOperazione = ReadNullableString(reader, nomeOrdinal) ?? string.Empty,
            OptimisticLockField = ReadNullableInt32(reader, optimisticLockOrdinal)
        };
    }

    private static void AddCampaignParameters(MySqlCommand command, GestionalePointsCampaignEditModel campaign)
    {
        command.Parameters.AddWithValue("@Attiva", campaign.Attiva ?? false);
        command.Parameters.AddWithValue("@Basecalcolo", (object?)campaign.BaseCalcolo ?? DBNull.Value);
        command.Parameters.AddWithValue("@Calcolosuvaloreivato", campaign.CalcolaSuValoreIva ?? false);
        command.Parameters.AddWithValue("@Europerpunto", (object?)campaign.EuroPerPunto ?? DBNull.Value);
        command.Parameters.AddWithValue("@Fine", (object?)campaign.Fine ?? DBNull.Value);
        command.Parameters.AddWithValue("@Importominimo", (object?)campaign.ImportoMinimo ?? DBNull.Value);
        command.Parameters.AddWithValue("@Inizio", (object?)campaign.Inizio ?? DBNull.Value);
        command.Parameters.AddWithValue("@Nomeoperazione", campaign.NomeOperazione);
    }

    private static bool? ReadNullableBool(MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            bool boolValue => boolValue,
            byte byteValue => byteValue != 0,
            sbyte sbyteValue => sbyteValue != 0,
            short shortValue => shortValue != 0,
            ushort ushortValue => ushortValue != 0,
            int intValue => intValue != 0,
            uint uintValue => uintValue != 0,
            long longValue => longValue != 0,
            ulong ulongValue => ulongValue != 0,
            decimal decimalValue => decimalValue != 0,
            string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
            string stringValue when int.TryParse(stringValue, out var intValue) => intValue != 0,
            _ => Convert.ToInt32(value) != 0
        };
    }

    private static int? ReadNullableInt32(MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal? ReadNullableDecimal(MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? ReadNullableDateTime(MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static string? ReadNullableString(MySqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToString(reader.GetValue(ordinal));
    }
}
