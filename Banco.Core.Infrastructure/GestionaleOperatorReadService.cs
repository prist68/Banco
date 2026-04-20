using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Operators;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleOperatorReadService : IGestionaleOperatorReadService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleOperatorReadService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<GestionaleOperatorSummary>> GetOperatorsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var results = await LoadFromOperatorTableAsync(connection, cancellationToken);
        if (results.Count > 0)
        {
            return results;
        }

        return await LoadFromDocumentHistoryAsync(connection, cancellationToken);
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }

    private static async Task<IReadOnlyList<GestionaleOperatorSummary>> LoadFromOperatorTableAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        var grouped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                o.OID,
                o.Oidutentefacile,
                TRIM(o.Utente) AS Operatore
            FROM operatore o
            WHERE o.Attivo = 1
              AND o.Utente IS NOT NULL
              AND TRIM(o.Utente) <> ''
            ORDER BY Operatore, o.OID;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(2))
            {
                continue;
            }

            var operatore = reader.GetString(2).Trim();
            if (string.IsNullOrWhiteSpace(operatore))
            {
                continue;
            }

            if (!grouped.TryGetValue(operatore, out var tokens))
            {
                tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { operatore };
                grouped[operatore] = tokens;
            }

            if (!reader.IsDBNull(0))
            {
                tokens.Add(reader.GetInt32(0).ToString());
            }

            if (!reader.IsDBNull(1))
            {
                tokens.Add(reader.GetInt32(1).ToString());
            }
        }

        return grouped
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new GestionaleOperatorSummary
            {
                Nome = item.Key,
                MatchTokens = item.Value.OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray()
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<GestionaleOperatorSummary>> LoadFromDocumentHistoryAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        var results = new List<GestionaleOperatorSummary>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT TRIM(d.Utente) AS Operatore
            FROM documento d
            WHERE d.Utente IS NOT NULL
              AND TRIM(d.Utente) <> ''
            ORDER BY Operatore;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            var operatore = reader.GetString(0).Trim();
            if (string.IsNullOrWhiteSpace(operatore))
            {
                continue;
            }

            results.Add(new GestionaleOperatorSummary
            {
                Nome = operatore,
                MatchTokens = new[] { operatore }
            });
        }

        return results;
    }
}
