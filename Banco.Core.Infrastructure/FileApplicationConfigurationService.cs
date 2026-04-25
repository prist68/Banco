using System.Text.Json;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Microsoft.Extensions.Configuration;

namespace Banco.Core.Infrastructure;

public sealed class FileApplicationConfigurationService : IApplicationConfigurationService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(120);

    private readonly string _settingsFilePath;
    private readonly string _legacyUserSettingsFilePath;
    private readonly bool _isServerScopedConfiguration;
    private readonly string _programBaseDirectory;
    private readonly string _defaultLocalStoreDirectory;
    private readonly string _legacyUserBaseDirectory;

    public event EventHandler<ApplicationConfigurationChangedEventArgs>? SettingsChanged;

    public FileApplicationConfigurationService(IConfiguration configuration)
    {
        var configuredBaseDirectory = configuration["ApplicationPaths:UserDataDirectory"];
        var userBaseDirectory = ExpandPath(configuredBaseDirectory);

        if (string.IsNullOrWhiteSpace(userBaseDirectory))
        {
            userBaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Banco");
        }

        _legacyUserBaseDirectory = userBaseDirectory;
        _programBaseDirectory = NormalizePath(AppContext.BaseDirectory);
        var configuredServerDirectory = ResolveServerConfigDirectory(configuration["ApplicationPaths:ServerConfigDirectory"]);
        var serverBaseDirectory = string.IsNullOrWhiteSpace(configuredServerDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Config")
            : configuredServerDirectory;
        _defaultLocalStoreDirectory = Path.Combine(_programBaseDirectory, "LocalStore");
        var serverSettingsFilePath = Path.Combine(serverBaseDirectory, "appsettings.user.json");
        var userSettingsFilePath = Path.Combine(userBaseDirectory, "appsettings.user.json");

        _legacyUserSettingsFilePath = userSettingsFilePath;
        _isServerScopedConfiguration = true;
        _settingsFilePath = _isServerScopedConfiguration
            ? serverSettingsFilePath
            : userSettingsFilePath;

        var activeDirectory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(activeDirectory))
        {
            Directory.CreateDirectory(activeDirectory);
        }
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            return await LoadInternalAsync(cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        AppSettings normalizedSettings;
        ApplicationConfigurationChangedEventArgs changeArgs;

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var previousSettings = await LoadInternalAsync(cancellationToken);
            normalizedSettings = NormalizeSettings(settings);
            await SaveInternalAsync(normalizedSettings, cancellationToken);
            changeArgs = BuildChangeArgs(previousSettings, normalizedSettings);
        }
        finally
        {
            FileLock.Release();
        }

        SettingsChanged?.Invoke(this, changeArgs);
    }

    public string GetSettingsFilePath() => _settingsFilePath;

    public string GetConfigurationScopeLabel() => _isServerScopedConfiguration
        ? "Configurazione server"
        : "Configurazione utente";

    private async Task SaveInternalAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = $"{_settingsFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            await ReplaceFileWithRetryAsync(tempFilePath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private async Task<AppSettings> LoadInternalAsync(CancellationToken cancellationToken)
    {
        EnsureServerConfigurationSeeded();

        if (!File.Exists(_settingsFilePath))
        {
            var defaultSettings = CreateDefaultSettings();
            await SaveInternalAsync(defaultSettings, cancellationToken);
            return defaultSettings;
        }

        AppSettings? loadedSettings;
        try
        {
            await using var stream = await OpenReadSharedAsync(cancellationToken);
            loadedSettings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                SerializerOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            BackupCorruptedSettingsFile();

            var recoveredSettings = CreateDefaultSettings();
            await SaveInternalAsync(recoveredSettings, cancellationToken);
            return recoveredSettings;
        }

        var sourceSettings = loadedSettings ?? CreateDefaultSettings();
        var normalizedSettings = NormalizeSettings(sourceSettings);
        if (loadedSettings is null || RequiresSettingsRewrite(sourceSettings, normalizedSettings))
        {
            await SaveInternalAsync(normalizedSettings, cancellationToken);
        }

        return normalizedSettings;
    }

    private async Task<FileStream> OpenReadSharedAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetryCount; attempt++)
        {
            try
            {
                return new FileStream(
                    _settingsFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.Asynchronous);
            }
            catch (IOException) when (attempt < MaxRetryCount - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        return new FileStream(
            _settingsFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous);
    }

    private async Task ReplaceFileWithRetryAsync(string tempFilePath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRetryCount; attempt++)
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    File.Replace(tempFilePath, _settingsFilePath, null, true);
                }
                else
                {
                    File.Move(tempFilePath, _settingsFilePath);
                }

                return;
            }
            catch (IOException) when (attempt < MaxRetryCount - 1)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        if (File.Exists(_settingsFilePath))
        {
            File.Replace(tempFilePath, _settingsFilePath, null, true);
            return;
        }

        File.Move(tempFilePath, _settingsFilePath);
    }

    private AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            GestionaleDatabase = new GestionaleDatabaseSettings(),
            LocalStore = new LocalStoreSettings
            {
                BaseDirectory = _defaultLocalStoreDirectory
            },
            ShellUi = new ShellUiSettings(),
            PosIntegration = new PosIntegrationSettings(),
            WinEcrIntegration = new WinEcrIntegrationSettings(),
            FmContent = new FmContentSettings(),
            BackupConfiguration = new BackupConfigurationSettings(),
            RestoreConfiguration = new RestoreConfigurationSettings(),
            GridLayouts = new Dictionary<string, GridLayoutSettings>(StringComparer.OrdinalIgnoreCase),
            DocumentListLayout = new DocumentListLayoutSettings(),
            BancoDocumentGridLayout = new BancoDocumentGridLayoutSettings()
        };
    }

    private static string ExpandPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Environment.ExpandEnvironmentVariables(path);
    }

    private void EnsureServerConfigurationSeeded()
    {
        if (!_isServerScopedConfiguration || File.Exists(_settingsFilePath))
        {
            return;
        }

        if (!File.Exists(_legacyUserSettingsFilePath))
        {
            return;
        }

        File.Copy(_legacyUserSettingsFilePath, _settingsFilePath, overwrite: false);
    }

    private void BackupCorruptedSettingsFile()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_settingsFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_settingsFilePath);
        var extension = Path.GetExtension(_settingsFilePath);
        var backupFileName = $"{fileNameWithoutExtension}.corrupted-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
        var backupFilePath = string.IsNullOrWhiteSpace(directory)
            ? backupFileName
            : Path.Combine(directory, backupFileName);

        File.Move(_settingsFilePath, backupFilePath, overwrite: true);
    }

    private AppSettings NormalizeSettings(AppSettings settings)
    {
        settings.GestionaleDatabase = NormalizeGestionaleDatabase(settings.GestionaleDatabase);
        settings.PosIntegration = NormalizePosIntegration(settings.PosIntegration);
        settings.WinEcrIntegration = NormalizeWinEcrIntegration(settings.WinEcrIntegration);
        settings.FmContent = NormalizeFmContent(settings.FmContent);
        settings.BackupConfiguration = NormalizeBackupConfiguration(settings.BackupConfiguration);
        settings.RestoreConfiguration = NormalizeRestoreConfiguration(settings.RestoreConfiguration);
        settings.LocalStore ??= new LocalStoreSettings();

        if (ShouldUseProgramLocalStore(settings.LocalStore.BaseDirectory))
        {
            settings.LocalStore.BaseDirectory = _defaultLocalStoreDirectory;
        }

        return settings;
    }

    private bool RequiresSettingsRewrite(AppSettings original, AppSettings normalized)
    {
        return !AreEquivalent(original.GestionaleDatabase, normalized.GestionaleDatabase) ||
               !AreEquivalent(original.PosIntegration, normalized.PosIntegration) ||
               !AreEquivalent(original.WinEcrIntegration, normalized.WinEcrIntegration) ||
               !AreEquivalent(original.FmContent, normalized.FmContent) ||
               !AreEquivalent(original.BackupConfiguration, normalized.BackupConfiguration) ||
               !AreEquivalent(original.RestoreConfiguration, normalized.RestoreConfiguration) ||
               NormalizePath(original.LocalStore.BaseDirectory) != NormalizePath(normalized.LocalStore.BaseDirectory);
    }

    private ApplicationConfigurationChangedEventArgs BuildChangeArgs(AppSettings previousSettings, AppSettings currentSettings)
    {
        return new ApplicationConfigurationChangedEventArgs
        {
            Settings = currentSettings,
            GestionaleDatabaseChanged = !AreEquivalent(previousSettings.GestionaleDatabase, currentSettings.GestionaleDatabase),
            LocalStoreChanged = NormalizePath(previousSettings.LocalStore.BaseDirectory) != NormalizePath(currentSettings.LocalStore.BaseDirectory)
        };
    }

    private static bool AreEquivalent(GestionaleDatabaseSettings original, GestionaleDatabaseSettings normalized)
    {
        return string.Equals(original.Host ?? string.Empty, normalized.Host ?? string.Empty, StringComparison.Ordinal) &&
               original.Port == normalized.Port &&
               string.Equals(original.Database ?? string.Empty, normalized.Database ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.Username ?? string.Empty, normalized.Username ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.Password ?? string.Empty, normalized.Password ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.CharacterSet ?? string.Empty, normalized.CharacterSet ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(PosIntegrationSettings original, PosIntegrationSettings normalized)
    {
        return original.Enabled == normalized.Enabled &&
               string.Equals(original.DeviceName ?? string.Empty, normalized.DeviceName ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.ManagementMode ?? string.Empty, normalized.ManagementMode ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.ProtocolType ?? string.Empty, normalized.ProtocolType ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.ConnectionType ?? string.Empty, normalized.ConnectionType ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.PosIpAddress ?? string.Empty, normalized.PosIpAddress ?? string.Empty, StringComparison.Ordinal) &&
               original.PosPort == normalized.PosPort &&
               string.Equals(original.TerminalId ?? string.Empty, normalized.TerminalId ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.CashRegisterId ?? string.Empty, normalized.CashRegisterId ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.CashRegisterIpAddress ?? string.Empty, normalized.CashRegisterIpAddress ?? string.Empty, StringComparison.Ordinal) &&
               original.CashRegisterPort == normalized.CashRegisterPort &&
               original.AmountExchangeRequired == normalized.AmountExchangeRequired &&
               original.ConfirmAmountFromEcr == normalized.ConfirmAmountFromEcr &&
               original.PrintTicketOnEcr == normalized.PrintTicketOnEcr &&
               original.CheckTerminalId == normalized.CheckTerminalId &&
               string.Equals(original.ReceiptFooterText ?? string.Empty, normalized.ReceiptFooterText ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.Notes ?? string.Empty, normalized.Notes ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(WinEcrIntegrationSettings original, WinEcrIntegrationSettings normalized)
    {
        return original.Enabled == normalized.Enabled &&
               string.Equals(original.DriverName ?? string.Empty, normalized.DriverName ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.DeviceSerialNumber ?? string.Empty, normalized.DeviceSerialNumber ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.ReceiptXmlPath ?? string.Empty, normalized.ReceiptXmlPath ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.AutoRunCommandFilePath ?? string.Empty, normalized.AutoRunCommandFilePath ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.AutoRunErrorFilePath ?? string.Empty, normalized.AutoRunErrorFilePath ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.DitronType ?? string.Empty, normalized.DitronType ?? string.Empty, StringComparison.Ordinal) &&
               original.DriverType == normalized.DriverType &&
               original.StandardRowCharacters == normalized.StandardRowCharacters &&
               original.PrecontoRowCharacters == normalized.PrecontoRowCharacters &&
               original.AutoRunPollingMilliseconds == normalized.AutoRunPollingMilliseconds &&
               string.Equals(original.ReceiptFooterText ?? string.Empty, normalized.ReceiptFooterText ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.Notes ?? string.Empty, normalized.Notes ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(FmContentSettings original, FmContentSettings normalized)
    {
        return string.Equals(original.RootDirectory ?? string.Empty, normalized.RootDirectory ?? string.Empty, StringComparison.Ordinal) &&
               string.Equals(original.ArticleImagesDirectory ?? string.Empty, normalized.ArticleImagesDirectory ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(BackupConfigurationSettings original, BackupConfigurationSettings normalized)
    {
        var originalScheduledTimes = original.ScheduledAdditionalTimes ?? [];
        var normalizedScheduledTimes = normalized.ScheduledAdditionalTimes ?? [];

        return string.Equals(original.DefaultBackupDirectory ?? string.Empty, normalized.DefaultBackupDirectory ?? string.Empty, StringComparison.Ordinal) &&
               original.RememberLastOptions == normalized.RememberLastOptions &&
               original.DisableBackupPrompt == normalized.DisableBackupPrompt &&
               original.BackupSuggestionDays == normalized.BackupSuggestionDays &&
               original.LastBackupAt == normalized.LastBackupAt &&
               original.UseDifferentialBackupNaming == normalized.UseDifferentialBackupNaming &&
               string.Equals(original.EnabledComputerName ?? string.Empty, normalized.EnabledComputerName ?? string.Empty, StringComparison.Ordinal) &&
               original.RestrictAdminBackup == normalized.RestrictAdminBackup &&
               original.DeleteOldBackupsEnabled == normalized.DeleteOldBackupsEnabled &&
               original.DeleteOldBackupsAfterDays == normalized.DeleteOldBackupsAfterDays &&
               original.ScheduledBackupEnabled == normalized.ScheduledBackupEnabled &&
               original.ScheduledRunsPerDay == normalized.ScheduledRunsPerDay &&
               string.Equals(original.ScheduledStartTime ?? string.Empty, normalized.ScheduledStartTime ?? string.Empty, StringComparison.Ordinal) &&
               originalScheduledTimes.SequenceEqual(normalizedScheduledTimes, StringComparer.Ordinal) &&
               original.ScheduledOnMonday == normalized.ScheduledOnMonday &&
               original.ScheduledOnTuesday == normalized.ScheduledOnTuesday &&
               original.ScheduledOnWednesday == normalized.ScheduledOnWednesday &&
               original.ScheduledOnThursday == normalized.ScheduledOnThursday &&
               original.ScheduledOnFriday == normalized.ScheduledOnFriday &&
               original.ScheduledOnSaturday == normalized.ScheduledOnSaturday &&
               original.ScheduledOnSunday == normalized.ScheduledOnSunday &&
               original.IncludeDatabase == normalized.IncludeDatabase &&
               original.IncludeAttachments == normalized.IncludeAttachments &&
               original.IncludeImages == normalized.IncludeImages &&
               original.IncludeLayouts == normalized.IncludeLayouts &&
               original.IncludeReports == normalized.IncludeReports &&
               string.Equals(original.DefaultComment ?? string.Empty, normalized.DefaultComment ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(RestoreConfigurationSettings original, RestoreConfigurationSettings normalized)
    {
        return string.Equals(original.DefaultRestoreDirectory ?? string.Empty, normalized.DefaultRestoreDirectory ?? string.Empty, StringComparison.Ordinal) &&
               original.AutoSelectLatestBackup == normalized.AutoSelectLatestBackup;
    }

    private static GestionaleDatabaseSettings NormalizeGestionaleDatabase(GestionaleDatabaseSettings? settings)
    {
        settings ??= new GestionaleDatabaseSettings();

        return new GestionaleDatabaseSettings
        {
            Host = string.IsNullOrWhiteSpace(settings.Host) ? "127.0.0.1" : settings.Host,
            Port = settings.Port <= 0 ? 3306 : settings.Port,
            Database = string.IsNullOrWhiteSpace(settings.Database) ? "db_diltech" : settings.Database,
            Username = string.IsNullOrWhiteSpace(settings.Username) ? "root" : settings.Username,
            Password = string.IsNullOrWhiteSpace(settings.Password) ? "Root2000$$" : settings.Password,
            CharacterSet = settings.CharacterSet ?? string.Empty
        };
    }

    private static PosIntegrationSettings NormalizePosIntegration(PosIntegrationSettings? settings)
    {
        settings ??= new PosIntegrationSettings();

        return new PosIntegrationSettings
        {
            Enabled = true,
            DeviceName = string.IsNullOrWhiteSpace(settings.DeviceName) ? "Nexi SmartPOS P61B" : settings.DeviceName,
            ManagementMode = string.IsNullOrWhiteSpace(settings.ManagementMode) ? "Client" : settings.ManagementMode,
            ProtocolType = string.IsNullOrWhiteSpace(settings.ProtocolType) ? "Generico protocollo 17" : settings.ProtocolType,
            ConnectionType = string.IsNullOrWhiteSpace(settings.ConnectionType) ? "ETH" : settings.ConnectionType,
            PosIpAddress = string.IsNullOrWhiteSpace(settings.PosIpAddress) ? "192.168.1.233" : settings.PosIpAddress,
            PosPort = settings.PosPort <= 0 ? 8081 : settings.PosPort,
            TerminalId = settings.TerminalId ?? string.Empty,
            CashRegisterId = string.IsNullOrWhiteSpace(settings.CashRegisterId) ? "00000001" : settings.CashRegisterId,
            CashRegisterIpAddress = string.IsNullOrWhiteSpace(settings.CashRegisterIpAddress) ? "192.168.1.231" : settings.CashRegisterIpAddress,
            CashRegisterPort = settings.CashRegisterPort <= 0 ? 1470 : settings.CashRegisterPort,
            AmountExchangeRequired = true,
            ConfirmAmountFromEcr = true,
            PrintTicketOnEcr = true,
            CheckTerminalId = settings.CheckTerminalId,
            ReceiptFooterText = settings.ReceiptFooterText ?? string.Empty,
            Notes = string.IsNullOrWhiteSpace(settings.Notes)
                ? "Porta POS confermata su app Scambio Importo: 8081. Il Codice cassa deve essere un ID a 8 cifre, non l'IP del PC."
                : settings.Notes
        };
    }

    private static WinEcrIntegrationSettings NormalizeWinEcrIntegration(WinEcrIntegrationSettings? settings)
    {
        settings ??= new WinEcrIntegrationSettings();

        return new WinEcrIntegrationSettings
        {
            Enabled = true,
            DriverName = string.IsNullOrWhiteSpace(settings.DriverName) ? "Ditron" : settings.DriverName,
            DeviceSerialNumber = string.IsNullOrWhiteSpace(settings.DeviceSerialNumber) ? "2CMSG003710" : settings.DeviceSerialNumber,
            ReceiptXmlPath = string.IsNullOrWhiteSpace(settings.ReceiptXmlPath) ? @"C:\tmp\scontrino.xml" : settings.ReceiptXmlPath,
            AutoRunCommandFilePath = string.IsNullOrWhiteSpace(settings.AutoRunCommandFilePath) ? @"C:\tmp\AutoRun.txt" : settings.AutoRunCommandFilePath,
            AutoRunErrorFilePath = string.IsNullOrWhiteSpace(settings.AutoRunErrorFilePath) ? @"C:\tmp\AutoRunErr.txt" : settings.AutoRunErrorFilePath,
            DitronType = string.IsNullOrWhiteSpace(settings.DitronType) ? "Upgrade" : settings.DitronType,
            DriverType = settings.DriverType <= 0 ? 1 : settings.DriverType,
            StandardRowCharacters = settings.StandardRowCharacters <= 0 ? 25 : settings.StandardRowCharacters,
            PrecontoRowCharacters = settings.PrecontoRowCharacters <= 0 ? 25 : settings.PrecontoRowCharacters,
            AutoRunPollingMilliseconds = settings.AutoRunPollingMilliseconds <= 0 ? 300 : settings.AutoRunPollingMilliseconds,
            ReceiptFooterText = settings.ReceiptFooterText ?? string.Empty,
            Notes = string.IsNullOrWhiteSpace(settings.Notes)
                ? "Configurazione allineata alla Fiscal Suite Driver e a WinEcrCom 3.0 con file AutoRun."
                : settings.Notes
        };
    }

    private static FmContentSettings NormalizeFmContent(FmContentSettings? settings)
    {
        settings ??= new FmContentSettings();

        var rootDirectory = string.IsNullOrWhiteSpace(settings.RootDirectory)
            ? @"C:\Facile Manager\DILTECH"
            : settings.RootDirectory.Trim();

        var articleImagesDirectory = string.IsNullOrWhiteSpace(settings.ArticleImagesDirectory)
            ? Path.Combine(rootDirectory, "Immagini")
            : settings.ArticleImagesDirectory.Trim();

        return new FmContentSettings
        {
            RootDirectory = rootDirectory,
            ArticleImagesDirectory = articleImagesDirectory
        };
    }

    private static BackupConfigurationSettings NormalizeBackupConfiguration(BackupConfigurationSettings? settings)
    {
        settings ??= new BackupConfigurationSettings();

        return new BackupConfigurationSettings
        {
            DefaultBackupDirectory = string.IsNullOrWhiteSpace(settings.DefaultBackupDirectory)
                ? @"C:\Facile Manager"
                : settings.DefaultBackupDirectory.Trim(),
            RememberLastOptions = settings.RememberLastOptions,
            DisableBackupPrompt = settings.DisableBackupPrompt,
            BackupSuggestionDays = settings.BackupSuggestionDays <= 0 ? 7 : settings.BackupSuggestionDays,
            LastBackupAt = settings.LastBackupAt,
            UseDifferentialBackupNaming = settings.UseDifferentialBackupNaming,
            EnabledComputerName = settings.EnabledComputerName?.Trim() ?? string.Empty,
            RestrictAdminBackup = settings.RestrictAdminBackup,
            DeleteOldBackupsEnabled = settings.DeleteOldBackupsEnabled,
            DeleteOldBackupsAfterDays = settings.DeleteOldBackupsAfterDays <= 0 ? 30 : settings.DeleteOldBackupsAfterDays,
            ScheduledBackupEnabled = settings.ScheduledBackupEnabled,
            ScheduledRunsPerDay = settings.ScheduledRunsPerDay <= 0 ? 1 : settings.ScheduledRunsPerDay,
            ScheduledStartTime = NormalizeScheduledStartTime(settings.ScheduledStartTime),
            ScheduledAdditionalTimes = NormalizeScheduledTimes(settings.ScheduledAdditionalTimes),
            ScheduledOnMonday = settings.ScheduledOnMonday,
            ScheduledOnTuesday = settings.ScheduledOnTuesday,
            ScheduledOnWednesday = settings.ScheduledOnWednesday,
            ScheduledOnThursday = settings.ScheduledOnThursday,
            ScheduledOnFriday = settings.ScheduledOnFriday,
            ScheduledOnSaturday = settings.ScheduledOnSaturday,
            ScheduledOnSunday = settings.ScheduledOnSunday,
            IncludeDatabase = settings.IncludeDatabase,
            IncludeAttachments = settings.IncludeAttachments,
            IncludeImages = settings.IncludeImages,
            IncludeLayouts = settings.IncludeLayouts,
            IncludeReports = settings.IncludeReports,
            DefaultComment = settings.DefaultComment ?? string.Empty
        };
    }

    private static string NormalizeScheduledStartTime(string? scheduledStartTime)
    {
        if (string.IsNullOrWhiteSpace(scheduledStartTime))
        {
            return "13:00";
        }

        return TimeSpan.TryParse(scheduledStartTime, out var time)
            ? $"{time.Hours:00}:{time.Minutes:00}"
            : "13:00";
    }

    private static List<string> NormalizeScheduledTimes(IEnumerable<string>? scheduledTimes)
    {
        if (scheduledTimes is null)
        {
            return [];
        }

        return scheduledTimes
            .Select(NormalizeScheduledStartTime)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static RestoreConfigurationSettings NormalizeRestoreConfiguration(RestoreConfigurationSettings? settings)
    {
        settings ??= new RestoreConfigurationSettings();

        return new RestoreConfigurationSettings
        {
            DefaultRestoreDirectory = string.IsNullOrWhiteSpace(settings.DefaultRestoreDirectory)
                ? @"C:\Facile Manager"
                : settings.DefaultRestoreDirectory.Trim(),
            AutoSelectLatestBackup = settings.AutoSelectLatestBackup
        };
    }

    private bool ShouldUseProgramLocalStore(string? configuredLocalStoreDirectory)
    {
        if (!_isServerScopedConfiguration)
        {
            return false;
        }

        var normalizedConfiguredDirectory = NormalizePath(configuredLocalStoreDirectory ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedConfiguredDirectory))
        {
            return true;
        }

        var configDirectory = NormalizePath(Path.GetDirectoryName(_settingsFilePath) ?? string.Empty);
        var normalizedLegacyUserBaseDirectory = NormalizePath(_legacyUserBaseDirectory);

        return normalizedConfiguredDirectory.Equals(configDirectory, StringComparison.OrdinalIgnoreCase) ||
               normalizedConfiguredDirectory.StartsWith(configDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               normalizedConfiguredDirectory.Equals(normalizedLegacyUserBaseDirectory, StringComparison.OrdinalIgnoreCase) ||
               normalizedConfiguredDirectory.StartsWith(normalizedLegacyUserBaseDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveServerConfigDirectory(string? configuredDirectory)
    {
        var expandedPath = ExpandPath(configuredDirectory);
        if (string.IsNullOrWhiteSpace(expandedPath))
        {
            return Path.Combine(AppContext.BaseDirectory, "Config");
        }

        return Path.IsPathRooted(expandedPath)
            ? expandedPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expandedPath));
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
