using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.PriceLists;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionalePriceListReadService : IGestionalePriceListReadService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionalePriceListReadService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<GestionalePriceListSummary>> GetSalesPriceListsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var results = new List<GestionalePriceListSummary>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.OID,
                COALESCE(NULLIF(TRIM(l.Listino), ''), CONCAT('Listino ', l.OID)) AS Nome,
                COALESCE(l.Predefinito, 0) AS Predefinito,
                COALESCE(l.Mostrainricerca, 0) AS Mostrainricerca,
                COALESCE(l.Mostraneidocumenti, 0) AS Mostraneidocumenti
            FROM listino l
            WHERE COALESCE(l.Tipolistino, 0) <> 1
            ORDER BY
                CASE
                    WHEN UPPER(COALESCE(NULLIF(TRIM(l.Listino), ''), '')) = 'WEB' THEN 0
                    ELSE 1
                END,
                CASE WHEN COALESCE(l.Predefinito, 0) = 1 THEN 0 ELSE 1 END,
                COALESCE(NULLIF(TRIM(l.Listino), ''), CONCAT('Listino ', l.OID));
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapPriceList(reader));
        }

        return results;
    }

    public async Task<GestionalePriceListSummary?> GetPriceListByOidAsync(
        int priceListOid,
        CancellationToken cancellationToken = default)
    {
        if (priceListOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.OID,
                COALESCE(NULLIF(TRIM(l.Listino), ''), CONCAT('Listino ', l.OID)) AS Nome,
                COALESCE(l.Predefinito, 0) AS Predefinito,
                COALESCE(l.Mostrainricerca, 0) AS Mostrainricerca,
                COALESCE(l.Mostraneidocumenti, 0) AS Mostraneidocumenti
            FROM listino l
            WHERE l.OID = @priceListOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@priceListOid", priceListOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapPriceList(reader);
    }

    private static GestionalePriceListSummary MapPriceList(MySqlDataReader reader)
    {
        var oid = reader.GetInt32(reader.GetOrdinal("OID"));
        var nome = reader.IsDBNull(reader.GetOrdinal("Nome"))
            ? $"Listino {oid}"
            : reader.GetString(reader.GetOrdinal("Nome"));

        return new GestionalePriceListSummary
        {
            Oid = oid,
            Nome = nome,
            IsDefault = !reader.IsDBNull(reader.GetOrdinal("Predefinito")) && reader.GetBoolean(reader.GetOrdinal("Predefinito")),
            IsVisibleInSearch = !reader.IsDBNull(reader.GetOrdinal("Mostrainricerca")) && reader.GetBoolean(reader.GetOrdinal("Mostrainricerca")),
            IsVisibleInDocuments = !reader.IsDBNull(reader.GetOrdinal("Mostraneidocumenti")) && reader.GetBoolean(reader.GetOrdinal("Mostraneidocumenti")),
            IsWeb = string.Equals(nome.Trim(), "Web", StringComparison.OrdinalIgnoreCase)
        };
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }
}
