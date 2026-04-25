using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;

namespace Banco.UI.Wpf.ArchiveSettingsModule;

public sealed class SqliteSettingsViewModel : ViewModelBase
{
    private readonly IApplicationConfigurationService _configurationService;
    private readonly ILocalStoreBootstrapper _localStoreBootstrapper;
    private readonly IPosProcessLogService _logService;

    private string _localStoreDirectory = string.Empty;
    private string _configurationPath = string.Empty;
    private string _configurationScope = string.Empty;
    private string _effectiveDatabasePath = string.Empty;
    private string _statusMessage = "Configurazione SQLite non ancora caricata.";

    public SqliteSettingsViewModel(
        IApplicationConfigurationService configurationService,
        ILocalStoreBootstrapper localStoreBootstrapper,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _localStoreBootstrapper = localStoreBootstrapper;
        _logService = logService;
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        _ = LoadAsync();
    }

    public string LocalStoreDirectory
    {
        get => _localStoreDirectory;
        set => SetProperty(ref _localStoreDirectory, value);
    }

    public string ConfigurationPath
    {
        get => _configurationPath;
        set => SetProperty(ref _configurationPath, value);
    }

    public string ConfigurationScope
    {
        get => _configurationScope;
        set => SetProperty(ref _configurationScope, value);
    }

    public string EffectiveDatabasePath
    {
        get => _effectiveDatabasePath;
        set => SetProperty(ref _effectiveDatabasePath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();
        ApplySettings(settings);
        StatusMessage = "Configurazione SQLite caricata.";
    }

    private async Task SaveAsync()
    {
        var settings = await _configurationService.LoadAsync();
        settings.LocalStore = new LocalStoreSettings
        {
            BaseDirectory = LocalStoreDirectory
        };

        await _configurationService.SaveAsync(settings);
        await _localStoreBootstrapper.InitializeAsync(settings.LocalStore);
        ApplySettings(settings);
        StatusMessage = $"Configurazione SQLite salvata. Database tecnico: {settings.LocalStore.DatabasePath}.";
        _logService.Info(nameof(SqliteSettingsViewModel), $"Configurazione SQLite salvata. BaseDirectory={settings.LocalStore.BaseDirectory}, DatabasePath={settings.LocalStore.DatabasePath}.");
    }

    private void ApplySettings(AppSettings settings)
    {
        LocalStoreDirectory = settings.LocalStore.BaseDirectory;
        EffectiveDatabasePath = settings.LocalStore.DatabasePath;
        ConfigurationPath = _configurationService.GetSettingsFilePath();
        ConfigurationScope = _configurationService.GetConfigurationScopeLabel();
    }
}
