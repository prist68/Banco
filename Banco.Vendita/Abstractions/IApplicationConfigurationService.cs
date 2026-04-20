using Banco.Vendita.Configuration;

namespace Banco.Vendita.Abstractions;

public interface IApplicationConfigurationService
{
    event EventHandler<ApplicationConfigurationChangedEventArgs>? SettingsChanged;

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    string GetSettingsFilePath();

    string GetConfigurationScopeLabel();
}
