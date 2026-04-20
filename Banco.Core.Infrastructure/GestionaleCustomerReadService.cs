using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Customers;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleCustomerReadService : IGestionaleCustomerReadService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleCustomerReadService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<GestionaleCustomerSummary>> SearchCustomersAsync(
        string searchText,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var results = new List<GestionaleCustomerSummary>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                s.OID,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), '') AS RagioneSociale,
                COALESCE(NULLIF(TRIM(s.Rappresentantelegalenome), ''), '') AS Nome,
                NULLIF(TRIM(s.Codicecartafedelta), '') AS CodiceCartaFedelta,
                s.Clientelistino AS ClienteListinoOid,
                NULLIF(TRIM(l.Listino), '') AS ClienteListinoNome,
                s.Punticartafedeltainiziali AS PuntiIniziali,
                s.Punticartafedelta AS PuntiAssegnati,
                CASE
                    WHEN s.Punticartafedeltainiziali IS NULL AND s.Punticartafedelta IS NULL THEN NULL
                    ELSE COALESCE(s.Punticartafedeltainiziali, 0) + COALESCE(s.Punticartafedelta, 0)
                END AS PuntiTotali,
                CASE
                    WHEN NULLIF(TRIM(s.Codicecartafedelta), '') IS NULL THEN 0
                    ELSE 1
                END AS HaRaccoltaPunti
            FROM soggetto s
            LEFT JOIN listino l ON l.OID = s.Clientelistino
            WHERE s.Ragionesociale1 LIKE @search
               OR s.Rappresentantelegalenome LIKE @search
               OR s.Codice LIKE @search
               OR CAST(s.OID AS CHAR) LIKE @search
            ORDER BY s.Ragionesociale1, s.Rappresentantelegalenome
            LIMIT @maxResults;
            """;
        command.Parameters.AddWithValue("@search", $"%{normalizedSearch}%");
        command.Parameters.AddWithValue("@maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapCustomer(reader, isFornitore: false));
        }

        return results;
    }

    public async Task<IReadOnlyList<GestionaleCustomerSummary>> SearchSuppliersAsync(
        string searchText,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return [];
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var results = new List<GestionaleCustomerSummary>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                s.OID,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), '') AS RagioneSociale,
                COALESCE(NULLIF(TRIM(s.Rappresentantelegalenome), ''), '') AS Nome,
                NULLIF(TRIM(s.Codicecartafedelta), '') AS CodiceCartaFedelta,
                s.Clientelistino AS ClienteListinoOid,
                NULLIF(TRIM(l.Listino), '') AS ClienteListinoNome,
                s.Punticartafedeltainiziali AS PuntiIniziali,
                s.Punticartafedelta AS PuntiAssegnati,
                CASE
                    WHEN s.Punticartafedeltainiziali IS NULL AND s.Punticartafedelta IS NULL THEN NULL
                    ELSE COALESCE(s.Punticartafedeltainiziali, 0) + COALESCE(s.Punticartafedelta, 0)
                END AS PuntiTotali,
                CASE
                    WHEN NULLIF(TRIM(s.Codicecartafedelta), '') IS NULL THEN 0
                    ELSE 1
                END AS HaRaccoltaPunti
            FROM soggetto s
            LEFT JOIN listino l ON l.OID = s.Clientelistino
            WHERE (
                    s.Fornitorepagamento IS NOT NULL
                 OR s.Fornitoreiva IS NOT NULL
                 OR s.Fornitorelistino IS NOT NULL
                 OR s.Fornitorecontoprimanota IS NOT NULL
                  )
              AND (
                    s.Ragionesociale1 LIKE @search
                 OR s.Rappresentantelegalenome LIKE @search
                 OR s.Codice LIKE @search
                 OR CAST(s.OID AS CHAR) LIKE @search
                  )
            ORDER BY s.Ragionesociale1, s.Rappresentantelegalenome
            LIMIT @maxResults;
            """;
        command.Parameters.AddWithValue("@search", $"%{normalizedSearch}%");
        command.Parameters.AddWithValue("@maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapCustomer(reader, isFornitore: true));
        }

        return results;
    }

    public async Task<GestionaleCustomerSummary?> GetCustomerByOidAsync(
        int customerOid,
        CancellationToken cancellationToken = default)
    {
        if (customerOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                s.OID,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), '') AS RagioneSociale,
                COALESCE(NULLIF(TRIM(s.Rappresentantelegalenome), ''), '') AS Nome,
                NULLIF(TRIM(s.Codicecartafedelta), '') AS CodiceCartaFedelta,
                s.Clientelistino AS ClienteListinoOid,
                NULLIF(TRIM(l.Listino), '') AS ClienteListinoNome,
                s.Punticartafedeltainiziali AS PuntiIniziali,
                s.Punticartafedelta AS PuntiAssegnati,
                CASE
                    WHEN s.Punticartafedeltainiziali IS NULL AND s.Punticartafedelta IS NULL THEN NULL
                    ELSE COALESCE(s.Punticartafedeltainiziali, 0) + COALESCE(s.Punticartafedelta, 0)
                END AS PuntiTotali,
                CASE
                    WHEN NULLIF(TRIM(s.Codicecartafedelta), '') IS NULL THEN 0
                    ELSE 1
                END AS HaRaccoltaPunti
            FROM soggetto s
            LEFT JOIN listino l ON l.OID = s.Clientelistino
            WHERE s.OID = @customerOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@customerOid", customerOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapCustomer(reader, isFornitore: false);
    }

    private static GestionaleCustomerSummary MapCustomer(MySqlDataReader reader, bool isFornitore)
    {
        var oidOrdinal = reader.GetOrdinal("OID");
        var ragioneSocialeOrdinal = reader.GetOrdinal("RagioneSociale");
        var nomeOrdinal = reader.GetOrdinal("Nome");
        var codiceCartaFedeltaOrdinal = reader.GetOrdinal("CodiceCartaFedelta");
        var clienteListinoOidOrdinal = reader.GetOrdinal("ClienteListinoOid");
        var clienteListinoNomeOrdinal = reader.GetOrdinal("ClienteListinoNome");
        var puntiInizialiOrdinal = reader.GetOrdinal("PuntiIniziali");
        var puntiAssegnatiOrdinal = reader.GetOrdinal("PuntiAssegnati");
        var puntiTotaliOrdinal = reader.GetOrdinal("PuntiTotali");
        var haRaccoltaPuntiOrdinal = reader.GetOrdinal("HaRaccoltaPunti");

        return new GestionaleCustomerSummary
        {
            Oid = reader.GetInt32(oidOrdinal),
            IsFornitore = isFornitore,
            RagioneSociale = reader.IsDBNull(ragioneSocialeOrdinal) ? string.Empty : reader.GetString(ragioneSocialeOrdinal),
            Nome = reader.IsDBNull(nomeOrdinal) ? null : reader.GetString(nomeOrdinal),
            CodiceCartaFedelta = reader.IsDBNull(codiceCartaFedeltaOrdinal) ? null : reader.GetString(codiceCartaFedeltaOrdinal),
            ClienteListinoOid = reader.IsDBNull(clienteListinoOidOrdinal) ? null : Convert.ToInt32(reader.GetValue(clienteListinoOidOrdinal)),
            ClienteListinoNome = reader.IsDBNull(clienteListinoNomeOrdinal) ? null : reader.GetString(clienteListinoNomeOrdinal),
            PuntiIniziali = reader.IsDBNull(puntiInizialiOrdinal) ? null : Convert.ToDecimal(reader.GetValue(puntiInizialiOrdinal)),
            PuntiAssegnati = reader.IsDBNull(puntiAssegnatiOrdinal) ? null : Convert.ToDecimal(reader.GetValue(puntiAssegnatiOrdinal)),
            PuntiTotali = reader.IsDBNull(puntiTotaliOrdinal) ? null : Convert.ToDecimal(reader.GetValue(puntiTotaliOrdinal)),
            HaRaccoltaPunti = !reader.IsDBNull(haRaccoltaPuntiOrdinal) && reader.GetBoolean(haRaccoltaPuntiOrdinal)
        };
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }
}
