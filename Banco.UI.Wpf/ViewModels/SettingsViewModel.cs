using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using System.IO;

namespace Banco.UI.Wpf.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private const string DefaultHost = "127.0.0.1";
    private const int DefaultPort = 3306;
    private const string DefaultDatabase = "db_diltech";
    private const string DefaultUsername = "root";
    private const string DefaultPassword = "Root2000$$";

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IGestionaleConnectionService _connectionService;
    private readonly ILocalStoreBootstrapper _localStoreBootstrapper;
    private readonly IPosProcessLogService _logService;

    private string _host = string.Empty;
    private int _port;
    private string _database = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _localStoreDirectory = string.Empty;
    private string _fmRootDirectory = string.Empty;
    private string _fmArticleImagesDirectory = string.Empty;
    private string _statusMessage = "Configurazione non ancora caricata.";

    public SettingsViewModel(
        IApplicationConfigurationService configurationService,
        IGestionaleConnectionService connectionService,
        ILocalStoreBootstrapper localStoreBootstrapper,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _connectionService = connectionService;
        _localStoreBootstrapper = localStoreBootstrapper;
        _logService = logService;

        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        TestConnectionCommand = new RelayCommand(() => _ = TestConnectionAsync());

        _ = LoadAsync();
    }

    public string Titolo => "Configurazioni generali";

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Database
    {
        get => _database;
        set => SetProperty(ref _database, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string LocalStoreDirectory
    {
        get => _localStoreDirectory;
        set => SetProperty(ref _localStoreDirectory, value);
    }

    public string FmRootDirectory
    {
        get => _fmRootDirectory;
        set
        {
            if (SetProperty(ref _fmRootDirectory, value))
            {
                NotifyPropertyChanged(nameof(FmArticleImagesResolvedPath));
            }
        }
    }

    public string FmArticleImagesDirectory
    {
        get => _fmArticleImagesDirectory;
        set
        {
            if (SetProperty(ref _fmArticleImagesDirectory, value))
            {
                NotifyPropertyChanged(nameof(FmArticleImagesResolvedPath));
            }
        }
    }

    public string FmArticleImagesResolvedPath => ResolveArticleImagesPath(FmRootDirectory, FmArticleImagesDirectory);

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }

    public RelayCommand TestConnectionCommand { get; }

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();

        Host = settings.GestionaleDatabase.Host;
        Port = settings.GestionaleDatabase.Port;
        Database = settings.GestionaleDatabase.Database;
        Username = settings.GestionaleDatabase.Username;
        Password = settings.GestionaleDatabase.Password;
        LocalStoreDirectory = settings.LocalStore.BaseDirectory;
        FmRootDirectory = settings.FmContent.RootDirectory;
        FmArticleImagesDirectory = settings.FmContent.ArticleImagesDirectory;
        StatusMessage = "Configurazione caricata.";
        _logService.Info(nameof(SettingsViewModel), $"Configurazioni generali caricate. Host={Host}, Database={Database}, LocalStore={LocalStoreDirectory}, FmRoot={FmRootDirectory}, ArticleImages={FmArticleImagesResolvedPath}.");
    }

    private async Task SaveAsync()
    {
        var settings = await BuildSettingsAsync();
        await _configurationService.SaveAsync(settings);
        await _localStoreBootstrapper.InitializeAsync(settings.LocalStore);
        StatusMessage = $"Configurazione salvata. DB attivo: {settings.GestionaleDatabase.Host}. Immagini articoli: {settings.FmContent.ArticleImagesDirectory}.";
        _logService.Info(nameof(SettingsViewModel), $"Configurazioni generali salvate. Host={settings.GestionaleDatabase.Host}, Database={settings.GestionaleDatabase.Database}, LocalStore={settings.LocalStore.BaseDirectory}, FmRoot={settings.FmContent.RootDirectory}, ArticleImages={settings.FmContent.ArticleImagesDirectory}.");
    }

    private async Task TestConnectionAsync()
    {
        var settings = await BuildSettingsAsync();
        var result = await _connectionService.TestConnectionAsync(settings.GestionaleDatabase);
        StatusMessage = $"{result.Message} Tempo: {result.Duration.TotalMilliseconds:N0} ms";
        _logService.Info(nameof(SettingsViewModel), $"Test connessione DB eseguito. Host={settings.GestionaleDatabase.Host}, Database={settings.GestionaleDatabase.Database}, Messaggio='{result.Message}', DurataMs={result.Duration.TotalMilliseconds:N0}.");
    }

    private async Task<AppSettings> BuildSettingsAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var normalizedHost = string.IsNullOrWhiteSpace(Host) ? DefaultHost : Host.Trim();
        settings.GestionaleDatabase = new GestionaleDatabaseSettings
        {
            Host = normalizedHost,
            Port = Port <= 0 ? DefaultPort : Port,
            Database = string.IsNullOrWhiteSpace(Database) ? DefaultDatabase : Database.Trim(),
            Username = string.IsNullOrWhiteSpace(Username) ? DefaultUsername : Username.Trim(),
            Password = string.IsNullOrWhiteSpace(Password) ? DefaultPassword : Password
        };

        settings.LocalStore = new LocalStoreSettings
        {
            BaseDirectory = LocalStoreDirectory
        };

        settings.FmContent = new FmContentSettings
        {
            RootDirectory = string.IsNullOrWhiteSpace(FmRootDirectory) ? @"C:\Facile Manager\DILTECH" : FmRootDirectory.Trim(),
            ArticleImagesDirectory = string.IsNullOrWhiteSpace(FmArticleImagesDirectory)
                ? ResolveArticleImagesPath(FmRootDirectory, string.Empty)
                : FmArticleImagesDirectory.Trim()
        };

        Host = settings.GestionaleDatabase.Host;
        Port = settings.GestionaleDatabase.Port;
        Database = settings.GestionaleDatabase.Database;
        Username = settings.GestionaleDatabase.Username;
        Password = settings.GestionaleDatabase.Password;
        FmRootDirectory = settings.FmContent.RootDirectory;
        FmArticleImagesDirectory = settings.FmContent.ArticleImagesDirectory;

        return settings;
    }

    private static string ResolveArticleImagesPath(string rootDirectory, string articleImagesDirectory)
    {
        var resolvedRootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? @"C:\Facile Manager\DILTECH"
            : rootDirectory.Trim();

        return string.IsNullOrWhiteSpace(articleImagesDirectory)
            ? Path.Combine(resolvedRootDirectory, "Immagini")
            : articleImagesDirectory.Trim();
    }
}
