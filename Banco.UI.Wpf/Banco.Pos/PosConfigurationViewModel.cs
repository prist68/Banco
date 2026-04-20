using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.PosModule;

public sealed class PosConfigurationViewModel : ViewModelBase
{
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;

    private bool _enabled;
    private string _deviceName = string.Empty;
    private string _managementMode = string.Empty;
    private string _protocolType = string.Empty;
    private string _connectionType = string.Empty;
    private string _posIpAddress = string.Empty;
    private int _posPort;
    private string _terminalId = string.Empty;
    private string _cashRegisterId = string.Empty;
    private string _cashRegisterIpAddress = string.Empty;
    private int _cashRegisterPort;
    private bool _amountExchangeRequired;
    private bool _confirmAmountFromEcr;
    private bool _printTicketOnEcr;
    private bool _checkTerminalId;
    private string _receiptFooterText = string.Empty;
    private string _notes = string.Empty;
    private string _testAmount = "1,00";
    private string _testResultMessage = "Nessun test eseguito.";
    private string _statusMessage = "Configurazione POS non ancora caricata.";

    public PosConfigurationViewModel(
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _logService = logService;

        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        TestConnectionCommand = new RelayCommand(() => _ = TestConnectionAsync());
        TestAmountCommand = new RelayCommand(() => _ = TestAmountAsync());

        _ = LoadAsync();
    }

    public string Titolo => "Configurazione POS";

    public string Descrizione =>
        "Modulo dedicato ai parametri variabili del collegamento POS. Le impostazioni vengono salvate in locale nel file appsettings.user.json.";

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    public string ManagementMode
    {
        get => _managementMode;
        set => SetProperty(ref _managementMode, value);
    }

    public string ProtocolType
    {
        get => _protocolType;
        set => SetProperty(ref _protocolType, value);
    }

    public string ConnectionType
    {
        get => _connectionType;
        set => SetProperty(ref _connectionType, value);
    }

    public string PosIpAddress
    {
        get => _posIpAddress;
        set => SetProperty(ref _posIpAddress, value);
    }

    public int PosPort
    {
        get => _posPort;
        set => SetProperty(ref _posPort, value);
    }

    public string TerminalId
    {
        get => _terminalId;
        set => SetProperty(ref _terminalId, value);
    }

    public string CashRegisterId
    {
        get => _cashRegisterId;
        set => SetProperty(ref _cashRegisterId, value);
    }

    public string CashRegisterIpAddress
    {
        get => _cashRegisterIpAddress;
        set => SetProperty(ref _cashRegisterIpAddress, value);
    }

    public int CashRegisterPort
    {
        get => _cashRegisterPort;
        set => SetProperty(ref _cashRegisterPort, value);
    }

    public bool AmountExchangeRequired
    {
        get => _amountExchangeRequired;
        set => SetProperty(ref _amountExchangeRequired, value);
    }

    public bool ConfirmAmountFromEcr
    {
        get => _confirmAmountFromEcr;
        set => SetProperty(ref _confirmAmountFromEcr, value);
    }

    public bool PrintTicketOnEcr
    {
        get => _printTicketOnEcr;
        set => SetProperty(ref _printTicketOnEcr, value);
    }

    public bool CheckTerminalId
    {
        get => _checkTerminalId;
        set => SetProperty(ref _checkTerminalId, value);
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

    public string TestAmount
    {
        get => _testAmount;
        set => SetProperty(ref _testAmount, value);
    }

    public string TestResultMessage
    {
        get => _testResultMessage;
        set => SetProperty(ref _testResultMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }

    public RelayCommand TestConnectionCommand { get; }

    public RelayCommand TestAmountCommand { get; }

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var normalized = Normalize(settings.PosIntegration);

        Enabled = normalized.Enabled;
        DeviceName = normalized.DeviceName;
        ManagementMode = normalized.ManagementMode;
        ProtocolType = normalized.ProtocolType;
        ConnectionType = normalized.ConnectionType;
        PosIpAddress = normalized.PosIpAddress;
        PosPort = normalized.PosPort;
        TerminalId = normalized.TerminalId;
        CashRegisterId = normalized.CashRegisterId;
        CashRegisterIpAddress = normalized.CashRegisterIpAddress;
        CashRegisterPort = normalized.CashRegisterPort;
        AmountExchangeRequired = normalized.AmountExchangeRequired;
        ConfirmAmountFromEcr = normalized.ConfirmAmountFromEcr;
        PrintTicketOnEcr = normalized.PrintTicketOnEcr;
        CheckTerminalId = normalized.CheckTerminalId;
        ReceiptFooterText = normalized.ReceiptFooterText;
        Notes = normalized.Notes;

        if (ShouldPersistDefaults(settings.PosIntegration, normalized))
        {
            settings.PosIntegration = normalized;
            await _configurationService.SaveAsync(settings);
            StatusMessage = "Configurazione POS predefinita salvata automaticamente in appsettings.user.json.";
            _logService.Info(nameof(PosConfigurationViewModel), $"Configurazione POS normalizzata e salvata automaticamente. Endpoint={PosIpAddress}:{PosPort}.");
            return;
        }

        StatusMessage = "Configurazione POS caricata da appsettings.user.json.";
        _logService.Info(nameof(PosConfigurationViewModel), $"Configurazione POS caricata. Endpoint={PosIpAddress}:{PosPort}, Cassa={CashRegisterIpAddress}:{CashRegisterPort}.");
    }

    private async Task SaveAsync()
    {
        var settings = await _configurationService.LoadAsync();
        settings.PosIntegration = BuildSettings();

        await _configurationService.SaveAsync(settings);
        StatusMessage = "Configurazione POS salvata in appsettings.user.json.";
        _logService.Info(nameof(PosConfigurationViewModel), $"Configurazione POS salvata. Endpoint={PosIpAddress}:{PosPort}, Cassa={CashRegisterIpAddress}:{CashRegisterPort}.");
    }

    private async Task TestConnectionAsync()
    {
        var endpoint = $"{PosIpAddress}:{PosPort}";

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(PosIpAddress, PosPort);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask || !client.Connected)
            {
                TestResultMessage = $"Connessione POS fallita su {endpoint}.";
                _logService.Warning(nameof(PosConfigurationViewModel), $"Test connessione POS fallito su {endpoint}.");
                return;
            }

            TestResultMessage = $"Connessione TCP riuscita verso POS {endpoint}.";
            _logService.Info(nameof(PosConfigurationViewModel), $"Test connessione POS riuscito su {endpoint}.");
        }
        catch (Exception ex)
        {
            TestResultMessage = $"Connessione POS fallita su {endpoint}: {ex.Message}";
            _logService.Error(nameof(PosConfigurationViewModel), $"Errore durante il test connessione POS su {endpoint}.", ex);
        }
    }

    private async Task TestAmountAsync()
    {
        if (!decimal.TryParse(TestAmount, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var amount) &&
            !decimal.TryParse(TestAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            TestResultMessage = "Importo di test non valido.";
            _logService.Warning(nameof(PosConfigurationViewModel), $"Test importo POS bloccato: importo non valido '{TestAmount}'.");
            return;
        }

        var payload = BuildPaymentMessage(amount);
        var packetBytes = BuildApplicationPacket(payload);
        var endpoint = $"{PosIpAddress}:{PosPort}";

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(PosIpAddress, PosPort);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask || !client.Connected)
            {
                TestResultMessage = $"Invio importo non riuscito: il POS {endpoint} non accetta la connessione.";
                _logService.Warning(nameof(PosConfigurationViewModel), $"Test importo POS fallito per mancata connessione su {endpoint}. Importo={amount:0.00}.");
                return;
            }

            using var stream = client.GetStream();
            stream.ReadTimeout = 1200;
            stream.WriteTimeout = 1200;

            await stream.WriteAsync(packetBytes, 0, packetBytes.Length);
            await stream.FlushAsync();

            var startedAt = DateTime.UtcNow;
            var frames = new List<string>();
            var buffer = new byte[512];

            while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(8))
            {
                int bytesRead;
                try
                {
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    var readCompleted = await Task.WhenAny(readTask, Task.Delay(1200));
                    if (readCompleted != readTask)
                    {
                        continue;
                    }

                    bytesRead = readTask.Result;
                }
                catch
                {
                    break;
                }

                if (bytesRead <= 0)
                {
                    break;
                }

                var chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                frames.Add(DescribePacket(chunk));

                if (IsApplicationResponse(chunk))
                {
                    var ack = BuildAckPacket();
                    await stream.WriteAsync(ack, 0, ack.Length);
                    await stream.FlushAsync();
                    frames.Add($"ACK inviato: {BitConverter.ToString(ack)}");
                    break;
                }
            }

            TestResultMessage =
                $"Test ECR-17 eseguito verso {endpoint}. Importo inviato: {amount:0.00}. " +
                $"Messaggio ASCII: {payload}. Pacchetto HEX: {BitConverter.ToString(packetBytes)}. " +
                (frames.Count == 0
                    ? "Nessuna risposta leggibile dal POS."
                    : string.Join(" | ", frames));
            _logService.Info(nameof(PosConfigurationViewModel), $"Test importo POS completato su {endpoint}. Importo={amount:0.00}. Risposte={frames.Count}.");
        }
        catch (Exception ex)
        {
            TestResultMessage = $"Errore durante il test importo su {endpoint}: {ex.Message}";
            _logService.Error(nameof(PosConfigurationViewModel), $"Errore durante il test importo POS su {endpoint}.", ex);
        }
    }

    private PosIntegrationSettings BuildSettings()
    {
        return new PosIntegrationSettings
        {
            Enabled = Enabled,
            DeviceName = DeviceName,
            ManagementMode = ManagementMode,
            ProtocolType = ProtocolType,
            ConnectionType = ConnectionType,
            PosIpAddress = PosIpAddress,
            PosPort = PosPort,
            TerminalId = TerminalId,
            CashRegisterId = CashRegisterId,
            CashRegisterIpAddress = CashRegisterIpAddress,
            CashRegisterPort = CashRegisterPort,
            AmountExchangeRequired = AmountExchangeRequired,
            ConfirmAmountFromEcr = ConfirmAmountFromEcr,
            PrintTicketOnEcr = PrintTicketOnEcr,
            CheckTerminalId = CheckTerminalId,
            ReceiptFooterText = ReceiptFooterText,
            Notes = Notes
        };
    }

    private static PosIntegrationSettings Normalize(PosIntegrationSettings settings)
    {
        return new PosIntegrationSettings
        {
            Enabled = true,
            DeviceName = string.IsNullOrWhiteSpace(settings.DeviceName) ? "Nexi SmartPOS P61B" : settings.DeviceName,
            ManagementMode = string.IsNullOrWhiteSpace(settings.ManagementMode) ? "Client" : settings.ManagementMode,
            ProtocolType = string.IsNullOrWhiteSpace(settings.ProtocolType) ? "Generico protocollo 17" : settings.ProtocolType,
            ConnectionType = string.IsNullOrWhiteSpace(settings.ConnectionType) ? "ETH" : settings.ConnectionType,
            PosIpAddress = string.IsNullOrWhiteSpace(settings.PosIpAddress) ? "192.168.1.233" : settings.PosIpAddress,
            PosPort = settings.PosPort is <= 0 or 55555 ? 8081 : settings.PosPort,
            TerminalId = settings.TerminalId,
            CashRegisterId = string.IsNullOrWhiteSpace(settings.CashRegisterId) ? "00000001" : settings.CashRegisterId,
            CashRegisterIpAddress = string.IsNullOrWhiteSpace(settings.CashRegisterIpAddress) ? "192.168.1.231" : settings.CashRegisterIpAddress,
            CashRegisterPort = settings.CashRegisterPort <= 0 ? 1470 : settings.CashRegisterPort,
            AmountExchangeRequired = true,
            ConfirmAmountFromEcr = true,
            PrintTicketOnEcr = true,
            CheckTerminalId = settings.CheckTerminalId,
            ReceiptFooterText = NormalizeFooterText(settings.ReceiptFooterText),
            Notes = string.IsNullOrWhiteSpace(settings.Notes)
                ? "Porta POS confermata su app Scambio Importo: 8081. TID lasciabile vuoto finche' il controllo ID terminale resta disattivato."
                : settings.Notes
        };
    }

    private static bool ShouldPersistDefaults(PosIntegrationSettings original, PosIntegrationSettings normalized)
    {
        return original.Enabled != normalized.Enabled ||
               !string.Equals(original.DeviceName, normalized.DeviceName, StringComparison.Ordinal) ||
               !string.Equals(original.ManagementMode, normalized.ManagementMode, StringComparison.Ordinal) ||
               !string.Equals(original.ProtocolType, normalized.ProtocolType, StringComparison.Ordinal) ||
               !string.Equals(original.ConnectionType, normalized.ConnectionType, StringComparison.Ordinal) ||
               !string.Equals(original.PosIpAddress, normalized.PosIpAddress, StringComparison.Ordinal) ||
               original.PosPort != normalized.PosPort ||
               !string.Equals(original.TerminalId, normalized.TerminalId, StringComparison.Ordinal) ||
               !string.Equals(original.CashRegisterId, normalized.CashRegisterId, StringComparison.Ordinal) ||
               !string.Equals(original.CashRegisterIpAddress, normalized.CashRegisterIpAddress, StringComparison.Ordinal) ||
               original.CashRegisterPort != normalized.CashRegisterPort ||
               original.AmountExchangeRequired != normalized.AmountExchangeRequired ||
               original.ConfirmAmountFromEcr != normalized.ConfirmAmountFromEcr ||
               original.PrintTicketOnEcr != normalized.PrintTicketOnEcr ||
               original.CheckTerminalId != normalized.CheckTerminalId ||
               !string.Equals(original.ReceiptFooterText, normalized.ReceiptFooterText, StringComparison.Ordinal) ||
               !string.Equals(original.Notes, normalized.Notes, StringComparison.Ordinal);
    }

    private string BuildPaymentMessage(decimal amount)
    {
        var terminalId = NormalizeId(TerminalId);
        var cashRegisterId = NormalizeId(CashRegisterId);
        var amountInCents = decimal.ToInt32(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        var amountField = amountInCents.ToString("00000000", CultureInfo.InvariantCulture);
        var codeContractField = NormalizeFooterText(ReceiptFooterText).PadRight(128, ' ');

        return string.Concat(
            terminalId,
            "0",
            "P",
            cashRegisterId,
            "0",
            "00",
            "0",
            "0",
            amountField,
            codeContractField,
            "00000000");
    }

    private static string NormalizeFooterText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length > 128 ? normalized[..128] : normalized;
    }

    private static string NormalizeId(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return "00000000";
        }

        if (digits.Length > 8)
        {
            digits = digits[^8..];
        }

        return digits.PadLeft(8, '0');
    }

    private static byte[] BuildApplicationPacket(string applicationMessage)
    {
        var messageBytes = Encoding.ASCII.GetBytes(applicationMessage);
        var packet = new byte[messageBytes.Length + 3];
        packet[0] = 0x02;
        Array.Copy(messageBytes, 0, packet, 1, messageBytes.Length);
        packet[^2] = 0x03;
        packet[^1] = ComputeLrc(packet[..^1]);
        return packet;
    }

    private static byte[] BuildAckPacket()
    {
        var packet = new byte[] { 0x06, 0x03, 0x00 };
        packet[2] = ComputeLrc(packet[..2]);
        return packet;
    }

    private static byte ComputeLrc(ReadOnlySpan<byte> bytes)
    {
        byte lrc = 0x7F;
        foreach (var value in bytes)
        {
            lrc ^= value;
        }

        return lrc;
    }

    private static bool IsApplicationResponse(byte[] buffer)
    {
        return buffer.Length >= 3 && buffer[0] == 0x02 && buffer[^2] == 0x03;
    }

    private static string DescribePacket(byte[] buffer)
    {
        if (buffer.Length == 0)
        {
            return "Pacchetto vuoto";
        }

        if (buffer.Length == 3 && buffer[0] == 0x06 && buffer[1] == 0x03)
        {
            return $"ACK ricevuto: {BitConverter.ToString(buffer)}";
        }

        if (buffer.Length == 3 && buffer[0] == 0x15 && buffer[1] == 0x03)
        {
            return $"NAK ricevuto: {BitConverter.ToString(buffer)}";
        }

        if (buffer.Length == 22 && buffer[0] == 0x01 && buffer[^1] == 0x04)
        {
            var progress = Encoding.ASCII.GetString(buffer, 1, 20).Trim();
            return $"Progress: {progress} [{BitConverter.ToString(buffer)}]";
        }

        if (IsApplicationResponse(buffer))
        {
            var message = Encoding.ASCII.GetString(buffer, 1, buffer.Length - 3);
            return $"Risposta applicativa: {message} [{BitConverter.ToString(buffer)}]";
        }

        return $"Pacchetto non riconosciuto: {BitConverter.ToString(buffer)}";
    }
}
