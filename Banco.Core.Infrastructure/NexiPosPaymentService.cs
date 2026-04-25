using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Pos;

namespace Banco.Core.Infrastructure;

public sealed class NexiPosPaymentService : IPosPaymentService
{
    private static readonly TimeSpan CooldownDopoAnnullaOperatore = TimeSpan.FromSeconds(2);

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;
    private long _lastUserCancelUtcTicks;

    public NexiPosPaymentService(
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _logService = logService;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var settings = (await _configurationService.LoadAsync(cancellationToken)).PosIntegration;
        _logService.Info(nameof(NexiPosPaymentService), $"Test connessione POS verso {settings.PosIpAddress}:{settings.PosPort}.");
        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(settings.PosIpAddress, settings.PosPort);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        var completedTask = await Task.WhenAny(connectTask, timeoutTask);
        var connected = completedTask == connectTask && client.Connected;
        _logService.Info(nameof(NexiPosPaymentService), connected
            ? $"Test connessione POS riuscito su {settings.PosIpAddress}:{settings.PosPort}."
            : $"Test connessione POS fallito su {settings.PosIpAddress}:{settings.PosPort}.");
        return connected;
    }

    public async Task<PosPaymentResult> ExecutePaymentAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        var settings = (await _configurationService.LoadAsync(cancellationToken)).PosIntegration;
        await EnsureTerminalReadyAfterRecentUserCancellationAsync(cancellationToken).ConfigureAwait(false);
        var payload = BuildExtendedPaymentMessage(
            settings.TerminalId,
            settings.CashRegisterId,
            amount,
            settings.ReceiptFooterText);
        var packetBytes = BuildApplicationPacket(payload);
        var endpoint = $"{settings.PosIpAddress}:{settings.PosPort}";
        var transactionLogFilePath = BuildTransactionLogFilePath(endpoint, amount);
        var transactionTrace = new List<string>();
        void CaptureTrace(string message) => transactionTrace.Add(
            $"[{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)}] {message}");

        CaptureTrace($"Avvio pagamento POS su {endpoint} per importo {amount:N2}.");
        CaptureTrace($"Messaggio ECR-17 (Payment X): {payload}");

        PosPaymentResult? finalResult = null;
        Exception? failureException = null;

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(settings.PosIpAddress, settings.PosPort);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask != connectTask || !client.Connected)
            {
                finalResult = BuildFailure(
                    $"Il POS {endpoint} non accetta la connessione.",
                    payload,
                    packetBytes,
                    transactionLogFilePath,
                    failureKind: PosPaymentFailureKind.ConnectionUnavailable);
                return finalResult;
            }

            using var stream = client.GetStream();
            stream.ReadTimeout = 1200;
            stream.WriteTimeout = 1200;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(150));

            var session = await NexiEcrRunner.ExecutePaymentAsync(
                stream,
                settings.TerminalId,
                settings.CashRegisterId,
                decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)),
                useExtendedRequest: true,
                printReceiptOnEcr: settings.PrintTicketOnEcr,
                paymentType: 0,
                contractText: settings.ReceiptFooterText,
                trace: CaptureTrace,
                cancellationToken: timeoutCts.Token);

            CaptureTrace($"Flusso POS completato. Ricevute={session.ReceiptChunks.Count}, Progress={session.ProgressMessages.Count}, ReceiptCompleted={session.ReceiptCompleted}.");

            if (session.PaymentResult is not null)
            {
                finalResult = MapPaymentResult(session.PaymentResult, payload, packetBytes, session, transactionLogFilePath);
                return finalResult;
            }

            finalResult = BuildFailure(
                $"Il POS {endpoint} non ha restituito una risposta applicativa finale.",
                payload,
                packetBytes,
                transactionLogFilePath,
                session.ProgressMessages.Concat(session.ReceiptChunks).ToList(),
                failureKind: PosPaymentFailureKind.FinalResultNotConfirmed);
            return finalResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RegisterUserCancellation();
            CaptureTrace($"Pagamento POS annullato esplicitamente dall'operatore su {endpoint}.");

            finalResult = BuildFailure(
                "Pagamento POS annullato dall'operatore. La vendita resta aperta e puoi continuare a lavorare sulla scheda.",
                payload,
                packetBytes,
                transactionLogFilePath,
                failureKind: PosPaymentFailureKind.CancelledByUser);
            return finalResult;
        }
        catch (OperationCanceledException)
        {
            var recoveredResult = await TryRecoverTimedOutPaymentAsync(
                settings,
                amount,
                endpoint,
                payload,
                packetBytes,
                transactionLogFilePath,
                CaptureTrace,
                cancellationToken);

            if (recoveredResult is not null)
            {
                finalResult = recoveredResult;
                return finalResult;
            }

            finalResult = BuildFailure(
                $"Timeout durante il pagamento POS su {endpoint}.",
                payload,
                packetBytes,
                transactionLogFilePath,
                failureKind: PosPaymentFailureKind.FinalResultNotConfirmed);
            return finalResult;
        }
        catch (IOException ex) when (IsNakFromTerminal(ex))
        {
            failureException = ex;
            CaptureTrace($"NAK rilevato dal terminale su {endpoint}. Il pagamento corrente viene considerato non avviato in modo valido e non puo` sbloccare lo scontrino.");

            finalResult = BuildFailure(
                $"Il POS {endpoint} ha rifiutato la richiesta corrente. Lo scontrino non parte finche` il terminale non avvia e conferma davvero un nuovo pagamento.",
                payload,
                packetBytes,
                transactionLogFilePath,
                failureKind: PosPaymentFailureKind.RejectedByTerminal);
            return finalResult;
        }
        catch (Exception ex)
        {
            failureException = ex;
            finalResult = BuildFailure(
                $"Errore durante il pagamento POS su {endpoint}: {ex.Message}",
                payload,
                packetBytes,
                transactionLogFilePath,
                failureKind: PosPaymentFailureKind.TechnicalError);
            return finalResult;
        }
        finally
        {
            if (finalResult is not null)
            {
                try
                {
                    await PersistTransactionLogAsync(
                        transactionLogFilePath,
                        endpoint,
                        amount,
                        payload,
                        packetBytes,
                        transactionTrace,
                        finalResult,
                        failureException,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception logEx)
                {
                    _logService.Warning(nameof(NexiPosPaymentService), $"Impossibile scrivere il log transazione POS {endpoint}: {logEx.Message}");
                }

                _logService.Info(nameof(NexiPosPaymentService), finalResult.IsSuccess
                    ? $"Pagamento POS autorizzato su {endpoint}. Log transazione: {transactionLogFilePath}"
                    : $"Pagamento POS non autorizzato su {endpoint}. Log transazione: {transactionLogFilePath}");
            }
        }
    }

    private async Task EnsureTerminalReadyAfterRecentUserCancellationAsync(CancellationToken cancellationToken)
    {
        long lastCancellationTicks = Interlocked.Read(ref _lastUserCancelUtcTicks);
        if (lastCancellationTicks <= 0)
        {
            return;
        }

        var lastCancellationUtc = new DateTimeOffset(lastCancellationTicks, TimeSpan.Zero);
        var elapsed = DateTimeOffset.UtcNow - lastCancellationUtc;
        var remaining = CooldownDopoAnnullaOperatore - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        _logService.Info(
            nameof(NexiPosPaymentService),
            $"Attendo {remaining.TotalMilliseconds:0} ms prima del nuovo invio POS per consentire al terminale di chiudere l'annullo precedente.");

        await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
    }

    private void RegisterUserCancellation()
    {
        Interlocked.Exchange(ref _lastUserCancelUtcTicks, DateTimeOffset.UtcNow.Ticks);
    }

    private static PosPaymentResult ParseExtendedPaymentResponse(
        string responseMessage,
        string requestMessage,
        byte[] requestBytes,
        IReadOnlyList<string> frames)
    {
        if (responseMessage.Length < 83)
        {
            return BuildFailure(
                "Risposta POS troppo corta per essere interpretata.",
                requestMessage,
                requestBytes,
                frames,
                responseMessage);
        }

        var messageCode = responseMessage.Substring(9, 1);
        if (!string.Equals(messageCode, "E", StringComparison.Ordinal) &&
            !string.Equals(messageCode, "V", StringComparison.Ordinal))
        {
            return BuildFailure(
                $"Risposta POS inattesa: codice messaggio {messageCode}.",
                requestMessage,
                requestBytes,
                frames,
                responseMessage);
        }

        var resultCode = responseMessage.Substring(10, 2);
        if (string.Equals(resultCode, "00", StringComparison.Ordinal))
        {
            return new PosPaymentResult
            {
                IsSuccess = true,
                Message = "Pagamento POS autorizzato.",
                RequestMessage = requestMessage,
                RequestHex = BitConverter.ToString(requestBytes),
                ResponseMessage = responseMessage,
                AuthorizationCode = responseMessage.Substring(34, 6).Trim(),
                CardType = responseMessage.Substring(47, 1).Trim(),
                Stan = responseMessage.Substring(59, 6).Trim(),
                Frames = frames
            };
        }

        var description = resultCode switch
        {
            "01" => responseMessage.Substring(12, Math.Min(24, responseMessage.Length - 12)).Trim(),
            "05" => "Carta non presente sul terminale.",
            "09" => "Tag aggiuntivo non riconosciuto dal terminale.",
            _ => $"Esito POS {resultCode}."
        };

        return BuildFailure(
            $"Pagamento POS rifiutato: {description}",
            requestMessage,
            requestBytes,
            frames,
            responseMessage);
    }

    private static PosPaymentResult BuildFailure(
        string message,
        string requestMessage,
        byte[] requestBytes,
        string transactionLogFilePath = "",
        IReadOnlyList<string>? frames = null,
        string responseMessage = "",
        PosPaymentFailureKind failureKind = PosPaymentFailureKind.TechnicalError)
    {
        return new PosPaymentResult
        {
            IsSuccess = false,
            FailureKind = failureKind,
            Message = message,
            RequestMessage = requestMessage,
            RequestHex = BitConverter.ToString(requestBytes),
            ResponseMessage = responseMessage,
            Frames = frames ?? [],
            TransactionLogFilePath = transactionLogFilePath
        };
    }

    private static PosPaymentResult BuildFailure(
        string message,
        string requestMessage,
        byte[] requestBytes,
        IReadOnlyList<string>? frames = null,
        string responseMessage = "",
        PosPaymentFailureKind failureKind = PosPaymentFailureKind.TechnicalError)
    {
        return BuildFailure(message, requestMessage, requestBytes, string.Empty, frames, responseMessage, failureKind);
    }

    private static PosPaymentResult MapPaymentResult(
        NexiPaymentResult paymentResult,
        string requestMessage,
        byte[] requestBytes,
        NexiPaymentSessionResult session,
        string transactionLogFilePath)
    {
        return new PosPaymentResult
        {
            IsSuccess = paymentResult.Approved,
            FailureKind = paymentResult.Approved ? PosPaymentFailureKind.None : PosPaymentFailureKind.Declined,
            Message = paymentResult.Approved
                ? "Pagamento POS autorizzato."
                : $"Pagamento POS rifiutato: {paymentResult.ErrorDescription}",
            RequestMessage = requestMessage,
            RequestHex = BitConverter.ToString(requestBytes),
            ResponseMessage = paymentResult.ToString(),
            AuthorizationCode = paymentResult.AuthorizationCode,
            CardType = paymentResult.CardType,
            Stan = paymentResult.Stan,
            Frames = session.ProgressMessages.Concat(session.ReceiptChunks).ToList(),
            TransactionLogFilePath = transactionLogFilePath
        };
    }

    private static async Task<PosPaymentResult?> TryRecoverTimedOutPaymentAsync(
        PosIntegrationSettings settings,
        decimal amount,
        string endpoint,
        string requestMessage,
        byte[] requestBytes,
        string transactionLogFilePath,
        Action<string> captureTrace,
        CancellationToken cancellationToken)
    {
        try
        {
            captureTrace($"Tentativo recupero ultimo esito POS dopo timeout su {endpoint}.");

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(settings.PosIpAddress, settings.PosPort);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
            if (completedTask != connectTask || !client.Connected)
            {
                captureTrace("Recupero G non riuscito: connessione POS non disponibile.");
                return null;
            }

            using var stream = client.GetStream();
            using var recoveryTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            recoveryTimeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var recoveredPayment = await NexiEcrRunner.RequestLastResultAsync(
                stream,
                settings.TerminalId,
                settings.CashRegisterId,
                CaptureRecoveryTrace(captureTrace),
                recoveryTimeoutCts.Token);

            if (recoveredPayment is null)
            {
                captureTrace("Recupero G non riuscito: nessun esito finale restituito dal POS.");
                return null;
            }

            if (!recoveredPayment.Approved)
            {
                captureTrace($"Recupero G ignorato: esito POS finale non approvato [{recoveredPayment.ResultCode}].");
                return null;
            }

            var importoRichiesto = decimal.ToInt32(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
            if (!TryParseHostAmountCents(recoveredPayment.HostAmountCentsRaw, out var importoRecuperato) ||
                importoRecuperato != importoRichiesto)
            {
                captureTrace($"Recupero G ignorato: importo esito {recoveredPayment.HostAmountCentsRaw} non coerente con l'importo richiesto {importoRichiesto:00000000}.");
                return null;
            }

            captureTrace($"Recupero G riuscito dopo timeout. Esito finale approvato e importo coerente ({importoRecuperato} cent).");

            return new PosPaymentResult
            {
                IsSuccess = true,
                Message = "Pagamento POS autorizzato.",
                RequestMessage = requestMessage,
                RequestHex = BitConverter.ToString(requestBytes),
                ResponseMessage = recoveredPayment.ToString(),
                AuthorizationCode = recoveredPayment.AuthorizationCode,
                CardType = recoveredPayment.CardType,
                Stan = recoveredPayment.Stan,
                TransactionLogFilePath = transactionLogFilePath
            };
        }
        catch (Exception ex)
        {
            captureTrace($"Recupero G fallito dopo timeout: {ex.Message}");
            return null;
        }
    }

    private static Action<string> CaptureRecoveryTrace(Action<string> captureTrace)
    {
        return message => captureTrace($"RECOVERY {message}");
    }

    private static bool TryParseHostAmountCents(string? rawValue, out int amountCents)
    {
        amountCents = 0;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var digits = new string(rawValue.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return false;
        }

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out amountCents);
    }

    private static bool IsNakFromTerminal(IOException ex)
    {
        return ex.Message.Contains("NAK", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTransactionLogFilePath(string endpoint, decimal amount)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Log", "Pos", "Transazioni");
        var safeEndpoint = new string(endpoint.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        var safeAmount = amount.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", "", StringComparison.Ordinal);
        var fileName = $"{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}_{safeEndpoint}_{safeAmount}.log";
        return Path.Combine(directory, fileName);
    }

    private static async Task PersistTransactionLogAsync(
        string transactionLogFilePath,
        string endpoint,
        decimal amount,
        string requestMessage,
        byte[] requestBytes,
        IReadOnlyList<string> traceLines,
        PosPaymentResult result,
        Exception? failureException,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionLogFilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(transactionLogFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("=== TRANSAZIONE POS ===");
        builder.AppendLine($"Data: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
        builder.AppendLine($"Endpoint: {endpoint}");
        builder.AppendLine($"Importo: {amount:N2}");
        builder.AppendLine($"Esito: {(result.IsSuccess ? "OK" : "KO")}");
        builder.AppendLine($"Messaggio: {result.Message}");
        builder.AppendLine($"Request: {requestMessage}");
        builder.AppendLine($"RequestHex: {BitConverter.ToString(requestBytes)}");

        if (!string.IsNullOrWhiteSpace(result.ResponseMessage))
        {
            builder.AppendLine($"Response: {result.ResponseMessage}");
        }

        if (!string.IsNullOrWhiteSpace(result.AuthorizationCode))
        {
            builder.AppendLine($"AuthorizationCode: {result.AuthorizationCode}");
        }

        if (!string.IsNullOrWhiteSpace(result.CardType))
        {
            builder.AppendLine($"CardType: {result.CardType}");
        }

        if (!string.IsNullOrWhiteSpace(result.Stan))
        {
            builder.AppendLine($"STAN: {result.Stan}");
        }

        if (traceLines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("--- TRACE ---");
            foreach (var line in traceLines)
            {
                builder.AppendLine(line);
            }
        }

        if (failureException is not null)
        {
            builder.AppendLine();
            builder.AppendLine("--- ECCEZIONE ---");
            builder.AppendLine(failureException.ToString());
        }

        await File.WriteAllTextAsync(transactionLogFilePath, builder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildExtendedPaymentMessage(
        string terminalId,
        string cashRegisterIdSeed,
        decimal amount,
        string footerText)
    {
        var normalizedTerminalId = NormalizeDigits(terminalId, "00000000");
        var cashRegisterId = NormalizeDigits(cashRegisterIdSeed, "00000001");
        var amountInCents = decimal.ToInt32(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        var amountField = amountInCents.ToString("00000000", CultureInfo.InvariantCulture);
        var footerField = NormalizeFooterText(footerText).PadLeft(128, ' ');

        return string.Concat(
            normalizedTerminalId,
            "0",
            "X",
            cashRegisterId,
            "0",
            "00",
            "0",
            "0",
            amountField,
            footerField,
            "00000000");
    }

    private static async Task<string> SendReceiptManagementCommandAsync(
        NetworkStream stream,
        string terminalId,
        bool enableReceiptOnEcr,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var receiptModeMessage = BuildReceiptModeMessage(terminalId, enableReceiptOnEcr);
        var receiptModePacket = BuildApplicationPacket(receiptModeMessage);
        await stream.WriteAsync(receiptModePacket, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var buffer = new byte[32];
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(3))
        {
            int bytesRead;
            try
            {
                var readTask = stream.ReadAsync(buffer, cancellationToken).AsTask();
                var readCompleted = await Task.WhenAny(readTask, Task.Delay(1200, cancellationToken));
                if (readCompleted != readTask)
                {
                    continue;
                }

                bytesRead = readTask.Result;
            }
            catch (Exception ex)
            {
                return $"nessuna risposta valida al comando E ({ex.Message})";
            }

            if (bytesRead <= 0)
            {
                break;
            }

            var chunk = new byte[bytesRead];
            Array.Copy(buffer, chunk, bytesRead);

            if (chunk.Length == 3 && chunk[0] == 0x06)
            {
                return $"ACK ricevuto: {BitConverter.ToString(chunk)}";
            }

            if (chunk.Length == 3 && chunk[0] == 0x15)
            {
                return $"NAK ricevuto: {BitConverter.ToString(chunk)}";
            }
        }

        return "timeout in attesa dell'ACK";
    }

    private static string BuildReceiptModeMessage(string terminalId, bool enableReceiptOnEcr)
    {
        var normalizedTerminalId = NormalizeDigits(terminalId, "00000000");
        var flag = enableReceiptOnEcr ? "1" : "0";

        return string.Concat(
            normalizedTerminalId,
            "0",
            "E",
            flag);
    }

    private static string BuildLastResultMessage(string terminalId, string cashRegisterIdSeed)
    {
        var normalizedTerminalId = NormalizeDigits(terminalId, "00000000");
        var cashRegisterId = NormalizeDigits(cashRegisterIdSeed, "00000000");

        return string.Concat(
            normalizedTerminalId,
            "0",
            "G",
            cashRegisterId,
            "0",
            "000");
    }

    private static string NormalizeDigits(string? value, string fallback)
    {
        var digits = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 0)
        {
            return fallback;
        }

        if (digits.Length > 8)
        {
            digits = digits[^8..];
        }

        return digits.PadLeft(8, '0');
    }

    private static string NormalizeFooterText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length > 128 ? normalized[..128] : normalized;
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

    private static char GetMessageCode(byte[] buffer)
    {
        if (buffer.Length < 11)
        {
            return '\0';
        }

        return (char)buffer[10];
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
