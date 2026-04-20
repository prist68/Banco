using System.Net.Sockets;
using System.Text;

namespace Banco.Vendita.Pos;

public static class NexiEcrProtocol
{
    public const byte SOH = 0x01;
    public const byte STX = 0x02;
    public const byte ETX = 0x03;
    public const byte EOT = 0x04;
    public const byte ACK = 0x06;
    public const byte NAK = 0x15;

    public static byte ComputeLrc(byte[] data, int offset, int count)
    {
        byte lrc = 0x7F;
        for (var i = offset; i < offset + count; i++)
        {
            lrc ^= data[i];
        }

        return lrc;
    }

    public static bool ValidateLrc(byte[] packetWithoutLastByte, byte expectedLrc)
    {
        return ComputeLrc(packetWithoutLastByte, 0, packetWithoutLastByte.Length) == expectedLrc;
    }

    public static byte[] BuildAck()
    {
        byte[] tmp = [ACK, ETX];
        byte lrc = ComputeLrc(tmp, 0, tmp.Length);
        return [ACK, ETX, lrc];
    }

    public static byte[] BuildNak()
    {
        byte[] tmp = [NAK, ETX];
        byte lrc = ComputeLrc(tmp, 0, tmp.Length);
        return [NAK, ETX, lrc];
    }

    public static byte[] WrapApplication(string asciiMessage)
    {
        ArgumentNullException.ThrowIfNull(asciiMessage);

        byte[] msg = Encoding.ASCII.GetBytes(asciiMessage);
        byte[] packetWithoutLrc = new byte[msg.Length + 2];
        packetWithoutLrc[0] = STX;
        Buffer.BlockCopy(msg, 0, packetWithoutLrc, 1, msg.Length);
        packetWithoutLrc[packetWithoutLrc.Length - 1] = ETX;

        byte lrc = ComputeLrc(packetWithoutLrc, 0, packetWithoutLrc.Length);

        byte[] packet = new byte[packetWithoutLrc.Length + 1];
        Buffer.BlockCopy(packetWithoutLrc, 0, packet, 0, packetWithoutLrc.Length);
        packet[packet.Length - 1] = lrc;
        return packet;
    }

    public static string BuildPaymentRequest(
        string terminalId,
        string cashRegisterId,
        long amountCents,
        bool additionalDataPresent = false,
        bool cardAlreadyPresent = false,
        int paymentType = 0,
        string contractText = "")
    {
        string tid = DigitsFixed(terminalId, 8);
        string cash = DigitsFixed(cashRegisterId, 8);
        string gt = additionalDataPresent ? "1" : "0";
        string already = cardAlreadyPresent ? "1" : "0";
        string payType = paymentType.ToString();
        string amount = DigitsFixed(amountCents.ToString(), 8);
        string text = RightAlignedText(contractText, 128);

        return tid + "0" + "P" + cash + gt + "00" + already + payType + amount + text + "00000000";
    }

    public static string BuildExtendedPaymentRequest(
        string terminalId,
        string cashRegisterId,
        long amountCents,
        bool additionalDataPresent = false,
        bool cardAlreadyPresent = false,
        int paymentType = 0,
        string contractText = "")
    {
        string tid = DigitsFixed(terminalId, 8);
        string cash = DigitsFixed(cashRegisterId, 8);
        string gt = additionalDataPresent ? "1" : "0";
        string already = cardAlreadyPresent ? "1" : "0";
        string payType = paymentType.ToString();
        string amount = DigitsFixed(amountCents.ToString(), 8);
        string text = RightAlignedText(contractText, 128);

        return tid + "0" + "X" + cash + gt + "00" + already + payType + amount + text + "00000000";
    }

    public static string BuildEnableReceiptOnEcr(string terminalId, bool enable)
    {
        string tid = DigitsFixed(terminalId, 8);
        return tid + "0" + "E" + (enable ? "1" : "0");
    }

    public static string BuildSendLastResult(string terminalId, string cashRegisterId, bool additionalDataPresent = false)
    {
        string tid = DigitsFixed(terminalId, 8);
        string cash = DigitsFixed(cashRegisterId, 8);
        string gt = additionalDataPresent ? "1" : "0";
        return tid + "0" + "G" + cash + gt + "000";
    }

    private static string DigitsFixed(string value, int len)
    {
        string digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length > len)
        {
            digits = digits.Substring(digits.Length - len, len);
        }

        return digits.PadLeft(len, '0');
    }

    private static string RightAlignedText(string value, int len)
    {
        value ??= string.Empty;
        if (value.Length > len)
        {
            value = value.Substring(value.Length - len, len);
        }

        return value.PadLeft(len, ' ');
    }
}

public abstract class NexiPacket
{
    protected NexiPacket(byte[] rawBytes)
    {
        RawBytes = rawBytes ?? [];
    }

    public byte[] RawBytes { get; }
}

public sealed class NexiAckPacket : NexiPacket
{
    public NexiAckPacket(byte[] rawBytes, bool lrcValid)
        : base(rawBytes)
    {
        LrcValid = lrcValid;
    }

    public bool LrcValid { get; }
}

public sealed class NexiNakPacket : NexiPacket
{
    public NexiNakPacket(byte[] rawBytes, bool lrcValid)
        : base(rawBytes)
    {
        LrcValid = lrcValid;
    }

    public bool LrcValid { get; }
}

public sealed class NexiProgressPacket : NexiPacket
{
    public NexiProgressPacket(byte[] rawBytes, string message)
        : base(rawBytes)
    {
        Message = message;
    }

    public string Message { get; }
}

public sealed class NexiApplicationPacket : NexiPacket
{
    public NexiApplicationPacket(byte[] rawBytes, string message, bool lrcValid)
        : base(rawBytes)
    {
        Message = message ?? string.Empty;
        LrcValid = lrcValid;
    }

    public string Message { get; }

    public bool LrcValid { get; }

    public string TerminalId => SafeSub(Message, 0, 8);

    public char MessageCode => Message.Length >= 10 ? Message[9] : '\0';

    public bool IsPaymentResult => (MessageCode is 'E' or 'V') && Message.Length >= 12;

    public bool IsReceiptChunk => MessageCode == 'S';

    public bool IsEnableReceiptCommandEcho => MessageCode == 'E' && Message.Length == 11;

    public string RawField11 => Message.Length > 10 ? Message[10..] : string.Empty;

    public bool IsTicketEnd => IsReceiptChunk && RawField11.IndexOf((char)0x1B) >= 0;

    public string TicketText => IsReceiptChunk ? DecodeTicketPayload(RawField11) : string.Empty;

    public NexiPaymentResult ToPaymentResult()
    {
        if (!IsPaymentResult)
        {
            throw new InvalidOperationException("Il pacchetto non contiene un risultato pagamento.");
        }

        return NexiPaymentResult.FromApplicationPacket(this);
    }

    private static string SafeSub(string value, int start, int length)
    {
        if (string.IsNullOrEmpty(value) || start >= value.Length)
        {
            return string.Empty;
        }

        int safeLength = Math.Min(length, value.Length - start);
        return value.Substring(start, safeLength);
    }

    private static string DecodeTicketPayload(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (char c in payload)
        {
            switch (c)
            {
                case (char)0x1B:
                    return sb.ToString();
                case (char)0x7D:
                    sb.AppendLine();
                    break;
                case (char)0x7E:
                case (char)0x7F:
                case (char)0x7B:
                case (char)0x7C:
                case (char)0x5E:
                    break;
                default:
                    if (!char.IsControl(c))
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}

public sealed class NexiPaymentResult
{
    public string TerminalId { get; set; } = string.Empty;

    public char MessageCode { get; set; }

    public string ResultCode { get; set; } = string.Empty;

    public bool Approved => ResultCode == "00";

    public bool Declined => ResultCode == "01";

    public string ErrorDescription { get; set; } = string.Empty;

    public string CardPan { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public string AuthorizationCode { get; set; } = string.Empty;

    public string HostDateTimeRaw { get; set; } = string.Empty;

    public string CardType { get; set; } = string.Empty;

    public string AcquirerId { get; set; } = string.Empty;

    public string Stan { get; set; } = string.Empty;

    public string OnlineId { get; set; } = string.Empty;

    public string ActionCode { get; set; } = string.Empty;

    public string HostAmountCentsRaw { get; set; } = string.Empty;

    public override string ToString()
    {
        if (Approved)
        {
            return $"APPROVATO [{ResultCode}] Auth={AuthorizationCode} STAN={Stan} OnlineId={OnlineId}";
        }

        return $"NEGATO/ERRORE [{ResultCode}] {ErrorDescription}";
    }

    internal static NexiPaymentResult FromApplicationPacket(NexiApplicationPacket packet)
    {
        string msg = packet.Message;

        var result = new NexiPaymentResult
        {
            TerminalId = Sub(msg, 0, 8),
            MessageCode = packet.MessageCode,
            ResultCode = Sub(msg, 10, 2),
            CardType = Sub(msg, 47, 1).Trim(),
            AcquirerId = Sub(msg, 48, 11).Trim(),
            Stan = Sub(msg, 59, 6).Trim(),
            OnlineId = Sub(msg, 65, 6).Trim()
        };

        if (result.ResultCode == "00")
        {
            result.CardPan = Sub(msg, 12, 19).Trim();
            result.TransactionType = Sub(msg, 31, 3).Trim();
            result.AuthorizationCode = Sub(msg, 34, 6).Trim();
            result.HostDateTimeRaw = Sub(msg, 40, 7).Trim();
        }
        else if (result.ResultCode == "01")
        {
            result.ErrorDescription = Sub(msg, 12, 24).TrimEnd();
        }

        if (msg.Length >= 82)
        {
            result.ActionCode = Sub(msg, 71, 3).Trim();
            result.HostAmountCentsRaw = Sub(msg, 74, 8).Trim();
        }

        return result;
    }

    private static string Sub(string value, int start, int length)
    {
        if (string.IsNullOrEmpty(value) || start >= value.Length)
        {
            return string.Empty;
        }

        int safeLength = Math.Min(length, value.Length - start);
        return value.Substring(start, safeLength);
    }
}

public sealed class NexiPaymentSessionResult
{
    public List<string> ProgressMessages { get; } = [];

    public List<string> ReceiptChunks { get; } = [];

    public NexiPaymentResult? PaymentResult { get; set; }

    public bool ReceiptCompleted { get; set; }

    public string ReceiptText => string.Concat(ReceiptChunks);

    public bool HasFinalResult => PaymentResult != null;
}

public sealed class NexiEcrStreamParser
{
    private readonly List<byte> _buffer = [];

    public IReadOnlyList<NexiPacket> Feed(byte[] chunk, int count)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (count < 0 || count > chunk.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        for (var i = 0; i < count; i++)
        {
            _buffer.Add(chunk[i]);
        }

        var packets = new List<NexiPacket>();

        while (TryReadPacket(out var packet))
        {
            packets.Add(packet!);
        }

        return packets;
    }

    private bool TryReadPacket(out NexiPacket? packet)
    {
        packet = null;

        while (_buffer.Count > 0 && !IsKnownStartByte(_buffer[0]))
        {
            _buffer.RemoveAt(0);
        }

        if (_buffer.Count == 0)
        {
            return false;
        }

        byte first = _buffer[0];

        if (first == NexiEcrProtocol.ACK || first == NexiEcrProtocol.NAK)
        {
            if (_buffer.Count < 3)
            {
                return false;
            }

            byte[] raw = _buffer.Take(3).ToArray();
            _buffer.RemoveRange(0, 3);

            bool valid = raw[1] == NexiEcrProtocol.ETX &&
                         NexiEcrProtocol.ComputeLrc(raw, 0, 2) == raw[2];

            packet = first == NexiEcrProtocol.ACK
                ? new NexiAckPacket(raw, valid)
                : new NexiNakPacket(raw, valid);

            return true;
        }

        if (first == NexiEcrProtocol.SOH)
        {
            if (_buffer.Count < 22)
            {
                return false;
            }

            byte[] raw = _buffer.Take(22).ToArray();
            if (raw[21] != NexiEcrProtocol.EOT)
            {
                _buffer.RemoveAt(0);
                return false;
            }

            _buffer.RemoveRange(0, 22);
            string message = Encoding.ASCII.GetString(raw, 1, 20).TrimEnd('\0', ' ');
            packet = new NexiProgressPacket(raw, message);
            return true;
        }

        if (first == NexiEcrProtocol.STX)
        {
            var etxIndex = -1;
            for (var i = 1; i < _buffer.Count; i++)
            {
                if (_buffer[i] == NexiEcrProtocol.ETX)
                {
                    etxIndex = i;
                    break;
                }
            }

            if (etxIndex < 0)
            {
                return false;
            }

            if (_buffer.Count < etxIndex + 2)
            {
                return false;
            }

            byte[] raw = _buffer.Take(etxIndex + 2).ToArray();
            _buffer.RemoveRange(0, etxIndex + 2);

            byte[] withoutLrc = raw.Take(raw.Length - 1).ToArray();
            bool valid = NexiEcrProtocol.ValidateLrc(withoutLrc, raw[raw.Length - 1]);

            string message = Encoding.ASCII.GetString(raw, 1, raw.Length - 3);
            packet = new NexiApplicationPacket(raw, message, valid);
            return true;
        }

        return false;
    }

    private static bool IsKnownStartByte(byte b)
    {
        return b == NexiEcrProtocol.ACK ||
               b == NexiEcrProtocol.NAK ||
               b == NexiEcrProtocol.SOH ||
               b == NexiEcrProtocol.STX;
    }
}

public static class NexiEcrRunner
{
    public static async Task<NexiPaymentSessionResult> ExecutePaymentAsync(
        NetworkStream stream,
        string terminalId,
        string cashRegisterId,
        long amountCents,
        bool useExtendedRequest = false,
        bool printReceiptOnEcr = false,
        int paymentType = 0,
        string contractText = "",
        Action<string>? trace = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanWrite)
        {
            throw new InvalidOperationException("Lo stream deve essere leggibile e scrivibile.");
        }

        var parser = new NexiEcrStreamParser();
        var session = new NexiPaymentSessionResult();
        trace?.Invoke($"SESSIONE POS avviata. Extended={useExtendedRequest}, PrintReceiptOnEcr={printReceiptOnEcr}, PaymentType={paymentType}, AmountCents={amountCents}.");

        if (printReceiptOnEcr)
        {
            string enable = NexiEcrProtocol.BuildEnableReceiptOnEcr(terminalId, true);
            byte[] enablePacket = NexiEcrProtocol.WrapApplication(enable);
            trace?.Invoke($"TX E: {FormatPacket(enablePacket)}");
            await stream.WriteAsync(enablePacket, 0, enablePacket.Length, cancellationToken).ConfigureAwait(false);

            await WaitForAckOnlyAsync(stream, parser, trace, cancellationToken).ConfigureAwait(false);
        }

        string request = useExtendedRequest
            ? NexiEcrProtocol.BuildExtendedPaymentRequest(terminalId, cashRegisterId, amountCents, false, false, paymentType, contractText)
            : NexiEcrProtocol.BuildPaymentRequest(terminalId, cashRegisterId, amountCents, false, false, paymentType, contractText);

        byte[] paymentPacket = NexiEcrProtocol.WrapApplication(request);
        trace?.Invoke($"TX PAYMENT: {FormatPacket(paymentPacket)}");
        await stream.WriteAsync(paymentPacket, 0, paymentPacket.Length, cancellationToken).ConfigureAwait(false);

        byte[] buffer = new byte[4096];

        while (true)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                trace?.Invoke("RX STREAM CLOSED");
                throw new IOException("Connessione chiusa dal terminale.");
            }

            trace?.Invoke($"RX RAW[{read}]: {BitConverter.ToString(buffer, 0, read)}");
            foreach (var packet in parser.Feed(buffer, read))
            {
                trace?.Invoke($"RX PACKET: {DescribePacket(packet)}");
                switch (packet)
                {
                    case NexiAckPacket ack:
                        if (!ack.LrcValid)
                        {
                            throw new IOException("ACK con LRC non valido.");
                        }

                        break;

                    case NexiNakPacket nak:
                        throw new IOException($"NAK dal terminale. LRC valido: {nak.LrcValid}");

                    case NexiProgressPacket progress:
                        session.ProgressMessages.Add(progress.Message);
                        break;

                    case NexiApplicationPacket app:
                        if (!app.LrcValid)
                        {
                            byte[] nakBytes = NexiEcrProtocol.BuildNak();
                            await stream.WriteAsync(nakBytes, 0, nakBytes.Length, cancellationToken).ConfigureAwait(false);
                            throw new IOException("Pacchetto applicativo con LRC non valido.");
                        }

                        byte[] ackBytes = NexiEcrProtocol.BuildAck();
                        await stream.WriteAsync(ackBytes, 0, ackBytes.Length, cancellationToken).ConfigureAwait(false);

                        if (app.IsReceiptChunk)
                        {
                            session.ReceiptChunks.Add(app.TicketText);
                            if (app.IsTicketEnd)
                            {
                                session.ReceiptCompleted = true;
                            }
                        }
                        else if (app.IsPaymentResult)
                        {
                            session.PaymentResult = app.ToPaymentResult();

                            if (!printReceiptOnEcr)
                            {
                                return session;
                            }
                        }

                        break;
                }
            }

            if (session.PaymentResult is not null)
            {
                if (!printReceiptOnEcr)
                {
                    return session;
                }

                if (!session.PaymentResult.Approved)
                {
                    return session;
                }

                if (session.ReceiptCompleted)
                {
                    return session;
                }
            }
        }
    }

    public static async Task<NexiPaymentResult?> RequestLastResultAsync(
        NetworkStream stream,
        string terminalId,
        string cashRegisterId,
        Action<string>? trace = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var parser = new NexiEcrStreamParser();
        string command = NexiEcrProtocol.BuildSendLastResult(terminalId, cashRegisterId);
        byte[] packet = NexiEcrProtocol.WrapApplication(command);
        trace?.Invoke($"TX G: {FormatPacket(packet)}");
        await stream.WriteAsync(packet, 0, packet.Length, cancellationToken).ConfigureAwait(false);

        byte[] buffer = new byte[4096];

        while (true)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                trace?.Invoke("RX STREAM CLOSED (G)");
                return null;
            }

            trace?.Invoke($"RX RAW G[{read}]: {BitConverter.ToString(buffer, 0, read)}");
            foreach (var item in parser.Feed(buffer, read))
            {
                trace?.Invoke($"RX PACKET G: {DescribePacket(item)}");
                switch (item)
                {
                    case NexiNakPacket:
                        throw new IOException("NAK sul comando G.");

                    case NexiApplicationPacket app:
                        if (!app.LrcValid)
                        {
                            byte[] nakBytes = NexiEcrProtocol.BuildNak();
                            await stream.WriteAsync(nakBytes, 0, nakBytes.Length, cancellationToken).ConfigureAwait(false);
                            throw new IOException("Risposta al comando G con LRC non valido.");
                        }

                        byte[] ackBytes = NexiEcrProtocol.BuildAck();
                        await stream.WriteAsync(ackBytes, 0, ackBytes.Length, cancellationToken).ConfigureAwait(false);

                        if (app.IsPaymentResult)
                        {
                            return app.ToPaymentResult();
                        }

                        break;
                }
            }
        }
    }

    private static async Task WaitForAckOnlyAsync(
        NetworkStream stream,
        NexiEcrStreamParser parser,
        Action<string>? trace,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];

        while (true)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                trace?.Invoke("RX STREAM CLOSED (ACK)");
                throw new IOException("Connessione chiusa mentre attendevo ACK.");
            }

            trace?.Invoke($"RX RAW ACK[{read}]: {BitConverter.ToString(buffer, 0, read)}");
            foreach (var packet in parser.Feed(buffer, read))
            {
                trace?.Invoke($"RX PACKET ACK: {DescribePacket(packet)}");
                if (packet is NexiNakPacket)
                {
                    throw new IOException("NAK ricevuto invece di ACK.");
                }

                if (packet is NexiAckPacket ack)
                {
                    if (!ack.LrcValid)
                    {
                        throw new IOException("ACK con LRC non valido.");
                    }

                    return;
                }

                if (packet is NexiApplicationPacket app && app.LrcValid)
                {
                    byte[] ackBytes = NexiEcrProtocol.BuildAck();
                    trace?.Invoke($"TX ACK: {BitConverter.ToString(ackBytes)}");
                    await stream.WriteAsync(ackBytes, 0, ackBytes.Length, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static string DescribePacket(NexiPacket packet)
    {
        return packet switch
        {
            NexiAckPacket ack => $"ACK LRC={(ack.LrcValid ? "OK" : "KO")} RAW={BitConverter.ToString(ack.RawBytes)}",
            NexiNakPacket nak => $"NAK LRC={(nak.LrcValid ? "OK" : "KO")} RAW={BitConverter.ToString(nak.RawBytes)}",
            NexiProgressPacket progress => $"SOH MSG='{progress.Message}' RAW={BitConverter.ToString(progress.RawBytes)}",
            NexiApplicationPacket app => $"APP Code={app.MessageCode} LRC={(app.LrcValid ? "OK" : "KO")} RAW={BitConverter.ToString(app.RawBytes)} MSG='{app.Message}'",
            _ => $"PACKET RAW={BitConverter.ToString(packet.RawBytes)}"
        };
    }

    private static string FormatPacket(byte[] packet)
    {
        var ascii = Encoding.ASCII.GetString(packet).Replace("\r", "\\r").Replace("\n", "\\n");
        return $"{BitConverter.ToString(packet)} | ASCII='{ascii}'";
    }
}
