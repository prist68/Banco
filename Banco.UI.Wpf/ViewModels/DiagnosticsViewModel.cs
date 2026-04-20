using Banco.Vendita.Abstractions;

using Banco.Vendita.Configuration;

namespace Banco.UI.Wpf.ViewModels;

public sealed class DiagnosticsViewModel : ViewModelBase
{
    private readonly IApplicationConfigurationService _configurationService;
    private string _localStorePath = string.Empty;
    private string _configurationPath = string.Empty;
    private string _configurationScope = string.Empty;
    private string _gestionaleDatabaseConnection = string.Empty;
    private string _fmRootDirectory = string.Empty;
    private string _articleImagesPath = string.Empty;

    public DiagnosticsViewModel(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
        _configurationService.SettingsChanged += OnSettingsChanged;
        _ = LoadAsync();
    }

    public string Titolo => "Percorsi";

    public string LocalStorePath
    {
        get => _localStorePath;
        private set => SetProperty(ref _localStorePath, value);
    }

    public string ConfigurationPath
    {
        get => _configurationPath;
        private set => SetProperty(ref _configurationPath, value);
    }

    public string ConfigurationScope
    {
        get => _configurationScope;
        private set => SetProperty(ref _configurationScope, value);
    }

    public string GestionaleDatabaseConnection
    {
        get => _gestionaleDatabaseConnection;
        private set => SetProperty(ref _gestionaleDatabaseConnection, value);
    }

    public string FmRootDirectory
    {
        get => _fmRootDirectory;
        private set => SetProperty(ref _fmRootDirectory, value);
    }

    public string ArticleImagesPath
    {
        get => _articleImagesPath;
        private set => SetProperty(ref _articleImagesPath, value);
    }

    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        _ = RefreshAsync();
    }

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();
        LocalStorePath = settings.LocalStore.DatabasePath;
        ConfigurationPath = _configurationService.GetSettingsFilePath();
        ConfigurationScope = _configurationService.GetConfigurationScopeLabel();
        GestionaleDatabaseConnection = $"{settings.GestionaleDatabase.Host}:{settings.GestionaleDatabase.Port} / {settings.GestionaleDatabase.Database}";
        FmRootDirectory = settings.FmContent.RootDirectory;
        ArticleImagesPath = settings.FmContent.ArticleImagesDirectory;
    }
}
