using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;

namespace Banco.Backup.ViewModels;

public sealed class RestoreConfigurationViewModel : ViewModelBase
{
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;

    private string _defaultRestoreDirectory = string.Empty;
    private bool _autoSelectLatestBackup;
    private string _statusMessage = "Configurazione restore non ancora caricata.";

    public RestoreConfigurationViewModel(
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService,
        BackupImportViewModel importTool)
    {
        _configurationService = configurationService;
        _logService = logService;
        ImportTool = importTool;
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        _ = LoadAsync();
    }

    public string DefaultRestoreDirectory
    {
        get => _defaultRestoreDirectory;
        set => SetProperty(ref _defaultRestoreDirectory, value);
    }

    public bool AutoSelectLatestBackup
    {
        get => _autoSelectLatestBackup;
        set => SetProperty(ref _autoSelectLatestBackup, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public BackupImportViewModel ImportTool { get; }

    public RelayCommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();
        ApplySettings(settings.RestoreConfiguration);
        StatusMessage = "Default restore caricati.";
    }

    private async Task SaveAsync()
    {
        var settings = await _configurationService.LoadAsync();
        settings.RestoreConfiguration = new RestoreConfigurationSettings
        {
            DefaultRestoreDirectory = string.IsNullOrWhiteSpace(DefaultRestoreDirectory) ? @"C:\Facile Manager" : DefaultRestoreDirectory.Trim(),
            AutoSelectLatestBackup = AutoSelectLatestBackup
        };

        await _configurationService.SaveAsync(settings);
        ApplySettings(settings.RestoreConfiguration);
        StatusMessage = "Configurazione restore salvata.";
        _logService.Info(nameof(RestoreConfigurationViewModel), $"Configurazione restore salvata. Cartella={settings.RestoreConfiguration.DefaultRestoreDirectory}, AutoSelectLatest={settings.RestoreConfiguration.AutoSelectLatestBackup}.");
    }

    private void ApplySettings(RestoreConfigurationSettings settings)
    {
        DefaultRestoreDirectory = settings.DefaultRestoreDirectory;
        AutoSelectLatestBackup = settings.AutoSelectLatestBackup;
    }
}
