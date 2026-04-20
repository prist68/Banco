using System.Diagnostics;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Diagnostics;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleConnectionService : IGestionaleConnectionService
{
    public async Task<ConnectionTestResult> TestConnectionAsync(
        GestionaleDatabaseSettings settings,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var appSettings = new AppSettings
            {
                GestionaleDatabase = settings
            };

            await using var connection = await GestionaleConnectionFactory.CreateOpenConnectionAsync(
                appSettings,
                cancellationToken);

            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Success = true,
                Message = string.IsNullOrWhiteSpace(settings.CharacterSet)
                    ? "Connessione al gestionale riuscita. Codifica rilevata automaticamente."
                    : $"Connessione al gestionale riuscita. Codifica impostata: {settings.CharacterSet}.",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Connessione non riuscita: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
    }
}
