using Banco.Vendita.Configuration;

namespace Banco.Vendita.Abstractions;

public interface IAiIntegrationSettingsRepository
{
    Task<AiIntegrationSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AiIntegrationSettings settings, CancellationToken cancellationToken = default);
}
