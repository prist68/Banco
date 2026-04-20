using Banco.Vendita.Configuration;
using MySqlConnector;
using System.Collections.Concurrent;

namespace Banco.Core.Infrastructure;

internal static class GestionaleConnectionFactory
{
    private static readonly ConcurrentDictionary<string, string> CharacterSetCache = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<MySqlConnection> CreateOpenConnectionAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var resolvedCharacterSet = await ResolveCharacterSetAsync(settings.GestionaleDatabase, cancellationToken);
        var builder = CreateConnectionStringBuilder(settings.GestionaleDatabase, resolvedCharacterSet);
        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public static MySqlConnectionStringBuilder CreateConnectionStringBuilder(
        GestionaleDatabaseSettings settings,
        string? characterSet = null)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Host,
            Port = (uint)settings.Port,
            Database = settings.Database,
            UserID = settings.Username,
            Password = settings.Password,
            ConnectionTimeout = 5
        };

        if (!string.IsNullOrWhiteSpace(characterSet))
        {
            builder.CharacterSet = characterSet;
        }

        return builder;
    }

    private static async Task<string?> ResolveCharacterSetAsync(
        GestionaleDatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.CharacterSet))
        {
            return settings.CharacterSet.Trim();
        }

        var cacheKey = BuildCharacterSetCacheKey(settings);
        if (CharacterSetCache.TryGetValue(cacheKey, out var cachedCharacterSet))
        {
            return string.IsNullOrWhiteSpace(cachedCharacterSet) ? null : cachedCharacterSet;
        }

        await using var probeConnection = new MySqlConnection(CreateConnectionStringBuilder(settings).ConnectionString);
        await probeConnection.OpenAsync(cancellationToken);

        await using var command = probeConnection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT c.CHARACTER_SET_NAME
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = DATABASE()
              AND c.TABLE_NAME IN ('documento', 'documentoriga', 'soggetto', 'articolo')
              AND c.CHARACTER_SET_NAME IS NOT NULL;
            """;

        var detectedCharsets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                detectedCharsets.Add(reader.GetString(0));
            }
        }

        if (detectedCharsets.Contains("utf8mb4"))
        {
            CharacterSetCache[cacheKey] = "utf8mb4";
            return "utf8mb4";
        }

        if (detectedCharsets.Contains("utf8"))
        {
            CharacterSetCache[cacheKey] = "utf8";
            return "utf8";
        }

        if (detectedCharsets.Contains("latin1"))
        {
            CharacterSetCache[cacheKey] = "latin1";
            return "latin1";
        }

        await using var fallbackCommand = probeConnection.CreateCommand();
        fallbackCommand.CommandText = "SELECT @@character_set_database;";
        var fallback = await fallbackCommand.ExecuteScalarAsync(cancellationToken);
        var resolved = fallback?.ToString()?.Trim();
        CharacterSetCache[cacheKey] = resolved ?? string.Empty;
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }

    private static string BuildCharacterSetCacheKey(GestionaleDatabaseSettings settings)
    {
        return string.Join("|",
            settings.Host?.Trim() ?? string.Empty,
            settings.Port,
            settings.Database?.Trim() ?? string.Empty,
            settings.Username?.Trim() ?? string.Empty);
    }
}
