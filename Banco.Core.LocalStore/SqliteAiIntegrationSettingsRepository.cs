using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteAiIntegrationSettingsRepository : IAiIntegrationSettingsRepository
{
    private const string SettingsKey = "default";
    private readonly IApplicationConfigurationService _configurationService;

    public SqliteAiIntegrationSettingsRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<AiIntegrationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var appSettings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(appSettings.LocalStore.BaseDirectory, appSettings.LocalStore.DatabasePath, cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Enabled, Provider, ApiBaseUrl, ApiKey, Model, Notes
            FROM AiIntegrationSettings
            WHERE SettingsKey = $settingsKey;
            """;
        command.Parameters.AddWithValue("$settingsKey", SettingsKey);

        bool enabled;
        string provider;
        string apiBaseUrl;
        string storedApiKey;
        string model;
        string notes;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new AiIntegrationSettings();
            }

            enabled = reader.GetInt32(0) != 0;
            provider = reader.IsDBNull(1) ? "DeepSeek" : reader.GetString(1);
            apiBaseUrl = reader.IsDBNull(2) ? "https://api.deepseek.com" : reader.GetString(2);
            storedApiKey = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            model = reader.IsDBNull(4) ? "deepseek-v4-pro" : reader.GetString(4);
            notes = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
        }

        if (!string.IsNullOrWhiteSpace(storedApiKey) && !LocalSecretProtector.IsProtected(storedApiKey))
        {
            await MigratePlainApiKeyAsync(connection, storedApiKey, appSettings.LocalStore.DatabasePath, cancellationToken);
        }

        return new AiIntegrationSettings
        {
            Enabled = enabled,
            Provider = provider,
            ApiBaseUrl = apiBaseUrl,
            ApiKey = LocalSecretProtector.Unprotect(storedApiKey, appSettings.LocalStore.DatabasePath),
            Model = model,
            Notes = notes
        };
    }

    public async Task SaveAsync(AiIntegrationSettings settings, CancellationToken cancellationToken = default)
    {
        var appSettings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(appSettings.LocalStore.BaseDirectory, appSettings.LocalStore.DatabasePath, cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        var normalizedSettings = Normalize(settings);
        var protectedApiKey = LocalSecretProtector.Protect(normalizedSettings.ApiKey, appSettings.LocalStore.DatabasePath);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO AiIntegrationSettings (SettingsKey, Enabled, Provider, ApiBaseUrl, ApiKey, Model, Notes, UpdatedAt)
            VALUES ($settingsKey, $enabled, $provider, $apiBaseUrl, $apiKey, $model, $notes, $updatedAt)
            ON CONFLICT(SettingsKey) DO UPDATE SET
                Enabled = excluded.Enabled,
                Provider = excluded.Provider,
                ApiBaseUrl = excluded.ApiBaseUrl,
                ApiKey = excluded.ApiKey,
                Model = excluded.Model,
                Notes = excluded.Notes,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$settingsKey", SettingsKey);
        command.Parameters.AddWithValue("$enabled", normalizedSettings.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$provider", normalizedSettings.Provider);
        command.Parameters.AddWithValue("$apiBaseUrl", normalizedSettings.ApiBaseUrl);
        command.Parameters.AddWithValue("$apiKey", protectedApiKey);
        command.Parameters.AddWithValue("$model", normalizedSettings.Model);
        command.Parameters.AddWithValue("$notes", normalizedSettings.Notes);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        string baseDirectory,
        string databasePath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(baseDirectory);
        var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS AiIntegrationSettings (
                SettingsKey TEXT PRIMARY KEY,
                Enabled INTEGER NOT NULL DEFAULT 0,
                Provider TEXT NOT NULL DEFAULT 'DeepSeek',
                ApiBaseUrl TEXT NOT NULL DEFAULT 'https://api.deepseek.com',
                ApiKey TEXT NOT NULL DEFAULT '',
                Model TEXT NOT NULL DEFAULT 'deepseek-v4-pro',
                Notes TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "ApiBaseUrl", "TEXT NOT NULL DEFAULT 'https://api.deepseek.com'", cancellationToken);
    }

    private static AiIntegrationSettings Normalize(AiIntegrationSettings settings)
    {
        return new AiIntegrationSettings
        {
            Enabled = settings.Enabled,
            Provider = string.IsNullOrWhiteSpace(settings.Provider) ? "DeepSeek" : settings.Provider.Trim(),
            ApiBaseUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl) ? "https://api.deepseek.com" : settings.ApiBaseUrl.Trim(),
            ApiKey = settings.ApiKey?.Trim() ?? string.Empty,
            Model = string.IsNullOrWhiteSpace(settings.Model) ? "deepseek-v4-pro" : settings.Model.Trim(),
            Notes = settings.Notes?.Trim() ?? string.Empty
        };
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string columnName,
        string sqlType,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE AiIntegrationSettings ADD COLUMN {columnName} {sqlType};";

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            // La colonna esiste gia`: non serve alcuna azione.
        }
    }

    private static async Task MigratePlainApiKeyAsync(
        SqliteConnection connection,
        string apiKey,
        string scope,
        CancellationToken cancellationToken)
    {
        var protectedApiKey = LocalSecretProtector.Protect(apiKey, scope);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE AiIntegrationSettings
            SET ApiKey = $apiKey,
                UpdatedAt = $updatedAt
            WHERE SettingsKey = $settingsKey;
            """;
        command.Parameters.AddWithValue("$settingsKey", SettingsKey);
        command.Parameters.AddWithValue("$apiKey", protectedApiKey);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
