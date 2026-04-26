using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;

namespace Banco.AI.ViewModels;

public sealed class AiSettingsViewModel : ViewModelBase
{
    private readonly IAiIntegrationSettingsRepository _settingsRepository;
    private readonly IPosProcessLogService _logService;

    private bool _enabled;
    private string _provider = "DeepSeek";
    private string _apiBaseUrl = "https://api.deepseek.com";
    private string _apiKey = string.Empty;
    private string _model = "deepseek-v4-pro";
    private string _notes = string.Empty;
    private string _statusMessage = "Impostazioni AI non ancora caricate.";

    public AiSettingsViewModel(
        IAiIntegrationSettingsRepository settingsRepository,
        IPosProcessLogService logService)
    {
        _settingsRepository = settingsRepository;
        _logService = logService;
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        ReloadCommand = new RelayCommand(() => _ = LoadAsync());
        UseDeepSeekCommand = new RelayCommand(UseDeepSeekPreset);

        _ = LoadAsync();
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string Provider
    {
        get => _provider;
        set => SetProperty(ref _provider, value);
    }

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public RelayCommand UseDeepSeekCommand { get; }

    private async Task LoadAsync()
    {
        var settings = await _settingsRepository.LoadAsync();
        ApplySettings(settings);
        StatusMessage = "Impostazioni AI caricate da SQLite.";
    }

    private async Task SaveAsync()
    {
        var settings = new AiIntegrationSettings
        {
            Enabled = Enabled,
            Provider = Provider,
            ApiBaseUrl = ApiBaseUrl,
            ApiKey = ApiKey,
            Model = Model,
            Notes = Notes
        };

        await _settingsRepository.SaveAsync(settings);
        ApplySettings(settings);
        StatusMessage = "Impostazioni AI salvate nel database SQLite locale.";
        _logService.Info(nameof(AiSettingsViewModel), $"Impostazioni AI salvate in SQLite. Provider={Provider}, ApiBaseUrl={ApiBaseUrl}, Model={Model}, Enabled={Enabled}.");
    }

    private void ApplySettings(AiIntegrationSettings settings)
    {
        Enabled = settings.Enabled;
        Provider = settings.Provider;
        ApiBaseUrl = settings.ApiBaseUrl;
        ApiKey = settings.ApiKey;
        Model = settings.Model;
        Notes = settings.Notes;
    }

    private void UseDeepSeekPreset()
    {
        Provider = "DeepSeek";
        ApiBaseUrl = "https://api.deepseek.com";
        Model = "deepseek-v4-pro";
        StatusMessage = "Preset DeepSeek applicato. Inserisci la API key e salva in SQLite.";
    }
}
