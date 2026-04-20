using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.WinEcrModule;

public sealed class WinEcrConfigurationViewModel : ViewModelBase
{
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;

    private bool _enabled;
    private string _driverName = string.Empty;
    private string _deviceSerialNumber = string.Empty;
    private string _receiptXmlPath = string.Empty;
    private string _autoRunCommandFilePath = string.Empty;
    private string _autoRunErrorFilePath = string.Empty;
    private string _ditronType = string.Empty;
    private int _driverType;
    private int _standardRowCharacters;
    private int _precontoRowCharacters;
    private int _autoRunPollingMilliseconds;
    private string _receiptFooterText = string.Empty;
    private string _notes = string.Empty;
    private string _statusMessage = "Configurazione fiscale non ancora caricata.";

    public WinEcrConfigurationViewModel(
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _logService = logService;
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        _ = LoadAsync();
    }

    public string Titolo => "Configurazione fiscale";

    public string Descrizione =>
        "Modulo dedicato a WinEcrCom 3.0 e Fiscal Suite Driver. Le impostazioni vengono salvate in locale nel file appsettings.user.json.";

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string DriverName
    {
        get => _driverName;
        set => SetProperty(ref _driverName, value);
    }

    public string DeviceSerialNumber
    {
        get => _deviceSerialNumber;
        set => SetProperty(ref _deviceSerialNumber, value);
    }

    public string ReceiptXmlPath
    {
        get => _receiptXmlPath;
        set => SetProperty(ref _receiptXmlPath, value);
    }

    public string AutoRunCommandFilePath
    {
        get => _autoRunCommandFilePath;
        set => SetProperty(ref _autoRunCommandFilePath, value);
    }

    public string AutoRunErrorFilePath
    {
        get => _autoRunErrorFilePath;
        set => SetProperty(ref _autoRunErrorFilePath, value);
    }

    public string DitronType
    {
        get => _ditronType;
        set => SetProperty(ref _ditronType, value);
    }

    public int DriverType
    {
        get => _driverType;
        set => SetProperty(ref _driverType, value);
    }

    public int StandardRowCharacters
    {
        get => _standardRowCharacters;
        set => SetProperty(ref _standardRowCharacters, value);
    }

    public int PrecontoRowCharacters
    {
        get => _precontoRowCharacters;
        set => SetProperty(ref _precontoRowCharacters, value);
    }

    public int AutoRunPollingMilliseconds
    {
        get => _autoRunPollingMilliseconds;
        set => SetProperty(ref _autoRunPollingMilliseconds, value);
    }

    public string ReceiptFooterText
    {
        get => _receiptFooterText;
        set => SetProperty(ref _receiptFooterText, value);
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

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var normalized = Normalize(settings.WinEcrIntegration);

        Enabled = normalized.Enabled;
        DriverName = normalized.DriverName;
        DeviceSerialNumber = normalized.DeviceSerialNumber;
        ReceiptXmlPath = normalized.ReceiptXmlPath;
        AutoRunCommandFilePath = normalized.AutoRunCommandFilePath;
        AutoRunErrorFilePath = normalized.AutoRunErrorFilePath;
        DitronType = normalized.DitronType;
        DriverType = normalized.DriverType;
        StandardRowCharacters = normalized.StandardRowCharacters;
        PrecontoRowCharacters = normalized.PrecontoRowCharacters;
        AutoRunPollingMilliseconds = normalized.AutoRunPollingMilliseconds;
        ReceiptFooterText = normalized.ReceiptFooterText;
        Notes = normalized.Notes;

        if (ShouldPersistDefaults(settings.WinEcrIntegration, normalized))
        {
            settings.WinEcrIntegration = normalized;
            await _configurationService.SaveAsync(settings);
            StatusMessage = "Configurazione fiscale predefinita salvata automaticamente in appsettings.user.json.";
            _logService.Info(nameof(WinEcrConfigurationViewModel), $"Configurazione fiscale normalizzata e salvata automaticamente. Driver={DriverName}, AutoRun={AutoRunCommandFilePath}.");
            return;
        }

        StatusMessage = "Configurazione fiscale caricata da appsettings.user.json.";
        _logService.Info(nameof(WinEcrConfigurationViewModel), $"Configurazione fiscale caricata. Driver={DriverName}, AutoRun={AutoRunCommandFilePath}, Errori={AutoRunErrorFilePath}.");
    }

    private async Task SaveAsync()
    {
        var settings = await _configurationService.LoadAsync();
        settings.WinEcrIntegration = BuildSettings();
        await _configurationService.SaveAsync(settings);
        StatusMessage = "Configurazione fiscale salvata in appsettings.user.json.";
        _logService.Info(nameof(WinEcrConfigurationViewModel), $"Configurazione fiscale salvata. Driver={DriverName}, AutoRun={AutoRunCommandFilePath}, Errori={AutoRunErrorFilePath}.");
    }

    private WinEcrIntegrationSettings BuildSettings()
    {
        return new WinEcrIntegrationSettings
        {
            Enabled = Enabled,
            DriverName = DriverName,
            DeviceSerialNumber = DeviceSerialNumber,
            ReceiptXmlPath = ReceiptXmlPath,
            AutoRunCommandFilePath = AutoRunCommandFilePath,
            AutoRunErrorFilePath = AutoRunErrorFilePath,
            DitronType = DitronType,
            DriverType = DriverType,
            StandardRowCharacters = StandardRowCharacters,
            PrecontoRowCharacters = PrecontoRowCharacters,
            AutoRunPollingMilliseconds = AutoRunPollingMilliseconds,
            ReceiptFooterText = ReceiptFooterText,
            Notes = Notes
        };
    }

    private static WinEcrIntegrationSettings Normalize(WinEcrIntegrationSettings settings)
    {
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

    private static bool ShouldPersistDefaults(WinEcrIntegrationSettings original, WinEcrIntegrationSettings normalized)
    {
        return original.Enabled != normalized.Enabled ||
               !string.Equals(original.DriverName, normalized.DriverName, StringComparison.Ordinal) ||
               !string.Equals(original.DeviceSerialNumber, normalized.DeviceSerialNumber, StringComparison.Ordinal) ||
               !string.Equals(original.ReceiptXmlPath, normalized.ReceiptXmlPath, StringComparison.Ordinal) ||
               !string.Equals(original.AutoRunCommandFilePath, normalized.AutoRunCommandFilePath, StringComparison.Ordinal) ||
               !string.Equals(original.AutoRunErrorFilePath, normalized.AutoRunErrorFilePath, StringComparison.Ordinal) ||
               !string.Equals(original.DitronType, normalized.DitronType, StringComparison.Ordinal) ||
               original.DriverType != normalized.DriverType ||
               original.StandardRowCharacters != normalized.StandardRowCharacters ||
               original.PrecontoRowCharacters != normalized.PrecontoRowCharacters ||
               original.AutoRunPollingMilliseconds != normalized.AutoRunPollingMilliseconds ||
               !string.Equals(original.ReceiptFooterText, normalized.ReceiptFooterText, StringComparison.Ordinal) ||
               !string.Equals(original.Notes, normalized.Notes, StringComparison.Ordinal);
    }
}
