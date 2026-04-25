using Banco.Vendita.Abstractions;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteLocalArticleTagRepository : ILocalArticleTagRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public SqliteLocalArticleTagRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<string>?> GetTagsAsync(
        int articoloOid,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync();
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TagsText
            FROM LocalArticleTags
            WHERE ArticoloOid = $articoloOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$articoloOid", articoloOid);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result == DBNull.Value)
        {
            return null;
        }

        return SplitTags(Convert.ToString(result));
    }

    public async Task SaveTagsAsync(
        int articoloOid,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(articoloOid));
        }

        var normalizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var connection = await OpenConnectionAsync();
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO LocalArticleTags (ArticoloOid, TagsText, UpdatedAt)
            VALUES ($articoloOid, $tagsText, $updatedAt)
            ON CONFLICT(ArticoloOid) DO UPDATE SET
                TagsText = excluded.TagsText,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$articoloOid", articoloOid);
        command.Parameters.AddWithValue("$tagsText", string.Join(", ", normalizedTags));
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetSuggestedTagsAsync(
        string? searchText = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchText?.Trim() ?? string.Empty;
        var limit = Math.Clamp(maxResults, 1, 100);

        await using var connection = await OpenConnectionAsync();
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TagsText
            FROM LocalArticleTags
            WHERE TagsText <> ''
            ORDER BY UpdatedAt DESC;
            """;

        var suggestions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            foreach (var tag in SplitTags(reader.GetString(0)))
            {
                if (!string.IsNullOrWhiteSpace(normalizedSearch) &&
                    !tag.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (suggestions.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                suggestions.Add(tag);
                if (suggestions.Count >= limit)
                {
                    return suggestions;
                }
            }
        }

        return suggestions;
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var settings = await _configurationService.LoadAsync();
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }

    private static async Task EnsureTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS LocalArticleTags (
                ArticoloOid INTEGER PRIMARY KEY,
                TagsText TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyList<string> SplitTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
