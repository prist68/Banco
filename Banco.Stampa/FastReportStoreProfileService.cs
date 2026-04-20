using Banco.Vendita.Abstractions;
using MySqlConnector;

namespace Banco.Stampa;

public sealed class FastReportStoreProfileService : IFastReportStoreProfileService
{
    private readonly IApplicationConfigurationService _configurationService;

    public FastReportStoreProfileService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<FastReportStoreProfile> GetStoreProfileAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await CreateOpenConnectionAsync(settings.GestionaleDatabase, cancellationToken);

        var configValues = await LoadConfigValuesAsync(connection, cancellationToken);
        var cityLookup = await LoadCityLookupAsync(connection, configValues.City, cancellationToken);

        var ragioneSociale = FirstNotEmpty(configValues.RagioneSociale, "SVAPOBAT");
        var indirizzo = FirstNotEmpty(configValues.Indirizzo, "Corso Vittorio Emanuele 90");
        var citta = FirstNotEmpty(configValues.City, "Barletta");
        var partitaIva = NormalizePartitaIva(FirstNotEmpty(configValues.PartitaIva, "07003640724"));
        var telefono = FirstNotEmpty(configValues.Telefono, "340 56 15 907");
        var email = FirstNotEmpty(configValues.Email, "info@diltech.it");

        return new FastReportStoreProfile
        {
            RagioneSociale = ragioneSociale,
            Indirizzo = indirizzo,
            Cap = FirstNotEmpty(cityLookup.Cap),
            Citta = citta,
            Provincia = FirstNotEmpty(cityLookup.Provincia),
            PartitaIva = partitaIva,
            Telefono = telefono,
            Email = email,
            RiferimentoScontrino = FirstNotEmpty(configValues.RiferimentoScontrino, "*** WWW.SVAPOBAT.IT ***")
        };
    }

    private static async Task<MySqlConnection> CreateOpenConnectionAsync(
        Banco.Vendita.Configuration.GestionaleDatabaseSettings settings,
        CancellationToken cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(settings.CharacterSet))
        {
            builder.CharacterSet = settings.CharacterSet.Trim();
        }

        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<(string RagioneSociale, string Indirizzo, string City, string PartitaIva, string Telefono, string Email, string RiferimentoScontrino)> LoadConfigValuesAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT `Key`, Modulo, `Value`
            FROM config
            WHERE `Key` IN (
                'KEY_Ragionesociale',
                'KEY_Indirizzo',
                'KEY_Citta',
                'KEY_Partitaiva',
                'KEY_Telefono',
                'KEY_Email',
                'KEY_Riferimento_Su_Scontrino_Predefinito')
            ORDER BY OID DESC;
            """;

        var rows = new List<(string Key, string Modulo, string Value)>();

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }

        string Pick(string key, params string[] preferredModules)
        {
            foreach (var preferredModule in preferredModules)
            {
                var preferredValue = rows
                    .FirstOrDefault(row =>
                        key.Equals(row.Key, StringComparison.OrdinalIgnoreCase) &&
                        preferredModule.Equals(row.Modulo, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(row.Value))
                    .Value;

                if (!string.IsNullOrWhiteSpace(preferredValue))
                {
                    return preferredValue.Trim();
                }
            }

            var fallbackValue = rows
                .FirstOrDefault(row =>
                    key.Equals(row.Key, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(row.Value))
                .Value;

            return fallbackValue?.Trim() ?? string.Empty;
        }

        return (
            Pick("KEY_Ragionesociale", "Impostazioni di base"),
            Pick("KEY_Indirizzo", "Impostazioni di base"),
            Pick("KEY_Citta", "Impostazioni di base"),
            Pick("KEY_Partitaiva", "Impostazioni di base"),
            Pick("KEY_Telefono", "Impostazioni di base"),
            Pick("KEY_Email", "Impostazioni di base"),
            Pick("KEY_Riferimento_Su_Scontrino_Predefinito", "Modellodocumentovenditaalbanco27", "Modellodocumentovenditaalbanco"));
    }

    private static async Task<(string Cap, string Provincia)> LoadCityLookupAsync(
        MySqlConnection connection,
        string cityName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return (string.Empty, string.Empty);
        }

        const string sql =
            """
            SELECT Cap, Provincia
            FROM citta
            WHERE Citta = @cityName
            ORDER BY OID DESC
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@cityName", cityName.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (string.Empty, string.Empty);
        }

        return (
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string NormalizePartitaIva(string partitaIva)
    {
        if (string.IsNullOrWhiteSpace(partitaIva))
        {
            return string.Empty;
        }

        var trimmed = partitaIva.Trim();
        return trimmed.StartsWith("IT", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"IT{trimmed}";
    }
}
