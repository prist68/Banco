using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Banco.Core.Domain.Entities;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Customers;
using Banco.Vendita.Fiscal;
using Banco.Vendita.Points;

namespace Banco.Core.Infrastructure;

public sealed class WinEcrAutoRunService : IWinEcrAutoRunService
{
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;
    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IGestionalePointsReadService _pointsReadService;
    private readonly IPointsRewardRuleService _rewardRuleService;
    private readonly IPointsCustomerBalanceService _pointsCustomerBalanceService;

    public WinEcrAutoRunService(
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService,
        IGestionaleCustomerReadService customerReadService,
        IGestionalePointsReadService pointsReadService,
        IPointsRewardRuleService rewardRuleService,
        IPointsCustomerBalanceService pointsCustomerBalanceService)
    {
        _configurationService = configurationService;
        _logService = logService;
        _customerReadService = customerReadService;
        _pointsReadService = pointsReadService;
        _rewardRuleService = rewardRuleService;
        _pointsCustomerBalanceService = pointsCustomerBalanceService;
    }

    public async Task<WinEcrAutoRunResult> GenerateReceiptAsync(DocumentoLocale documento, CancellationToken cancellationToken = default)
    {
        var settings = (await _configurationService.LoadAsync(cancellationToken)).WinEcrIntegration;
        var loyaltyFooterLines = await BuildLoyaltyFooterLinesAsync(documento, cancellationToken);
        var content = BuildCommandContent(documento, settings.ReceiptFooterText, loyaltyFooterLines);
        return await ExecuteAutoRunCommandAsync(
            settings,
            content,
            "Generazione AutoRun scontrino",
            cancellationToken);
    }

    public async Task<WinEcrAutoRunResult> ExecuteCashRegisterOperationAsync(
        CashRegisterOptionSelection selection,
        DocumentoLocale? documento = null,
        CancellationToken cancellationToken = default)
    {
        var settings = (await _configurationService.LoadAsync(cancellationToken)).WinEcrIntegration;
        var failureMessage = selection.Action switch
        {
            CashRegisterOptionAction.ReceiptReprint => "Numero o data scontrino non validi per la ristampa.",
            CashRegisterOptionAction.ReceiptCancellation => "Annullo scontrino non disponibile: servono scheda fiscalizzata corrente, numero, data e righe documento.",
            _ => "Operazione cassa non riconosciuta."
        };
        var content = selection.Action switch
        {
            CashRegisterOptionAction.DailyJournal => BuildDailyJournalCommandContent(selection.JournalMode),
            CashRegisterOptionAction.CloseCashAndTransmit => BuildCloseCashCommandContent(selection.JournalMode),
            CashRegisterOptionAction.ReceiptReprint => BuildReceiptReprintCommandContent(selection),
            CashRegisterOptionAction.ReceiptCancellation => BuildReceiptCancellationCommandContent(selection, documento),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(content))
        {
            return new WinEcrAutoRunResult
            {
                IsSuccess = false,
                Message = failureMessage,
                ErrorFilePath = settings.AutoRunErrorFilePath
            };
        }

        var operationLabel = selection.Action switch
        {
            CashRegisterOptionAction.DailyJournal => $"Stampa giornale giornaliero {selection.JournalModeLabel}",
            CashRegisterOptionAction.CloseCashAndTransmit => $"Stampa e chiusura cassa {selection.JournalModeLabel}",
            CashRegisterOptionAction.ReceiptReprint => $"Ristampa scontrino {selection.ReceiptReferenceLabel}",
            CashRegisterOptionAction.ReceiptCancellation => $"Annullo scontrino {selection.ReceiptReferenceLabel}",
            _ => "Operazione cassa"
        };

        return await ExecuteAutoRunCommandAsync(settings, content, operationLabel, cancellationToken);
    }

    private async Task<WinEcrAutoRunResult> ExecuteAutoRunCommandAsync(
        Banco.Vendita.Configuration.WinEcrIntegrationSettings settings,
        string content,
        string operationLabel,
        CancellationToken cancellationToken)
    {
        var commandFilePath = settings.AutoRunCommandFilePath;
        var errorFilePath = settings.AutoRunErrorFilePath;
        _logService.Info(nameof(WinEcrAutoRunService), $"{operationLabel} avviata. File comando: {commandFilePath}, file errori: {errorFilePath}.");

        if (string.IsNullOrWhiteSpace(commandFilePath))
        {
            _logService.Warning(nameof(WinEcrAutoRunService), "Percorso del file AutoRun non configurato.");
            return new WinEcrAutoRunResult
            {
                IsSuccess = false,
                Message = "Percorso del file AutoRun non configurato.",
                ErrorFilePath = errorFilePath
            };
        }

        if (File.Exists(commandFilePath))
        {
            var existingContent = await File.ReadAllTextAsync(commandFilePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(existingContent))
            {
                _logService.Warning(nameof(WinEcrAutoRunService), $"File AutoRun occupato: {commandFilePath}.");
                return new WinEcrAutoRunResult
                {
                    IsSuccess = false,
                    Message = $"Il file AutoRun {commandFilePath} contiene gia` un comando in attesa.",
                    CommandFilePath = commandFilePath,
                    ErrorFilePath = errorFilePath
                };
            }
        }

        if (!string.IsNullOrWhiteSpace(errorFilePath))
        {
            var errorDirectory = Path.GetDirectoryName(errorFilePath);
            if (!string.IsNullOrWhiteSpace(errorDirectory))
            {
                Directory.CreateDirectory(errorDirectory);
            }

            await File.WriteAllTextAsync(errorFilePath, string.Empty, Encoding.Default, cancellationToken);
            _logService.Info(nameof(WinEcrAutoRunService), $"File errori azzerato: {errorFilePath}.");
        }

        _logService.Info(nameof(WinEcrAutoRunService), $"Contenuto AutoRun generato per {operationLabel}:{Environment.NewLine}{content.TrimEnd()}");
        var directory = Path.GetDirectoryName(commandFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = $"{commandFilePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempFilePath, content, Encoding.Default, cancellationToken);

        if (File.Exists(commandFilePath))
        {
            File.Delete(commandFilePath);
        }

        File.Move(tempFilePath, commandFilePath);
        _logService.Info(nameof(WinEcrAutoRunService), $"File AutoRun scritto: {commandFilePath}.");

        var waitResult = await WaitForExecutionAsync(
            commandFilePath,
            errorFilePath,
            content,
            settings.AutoRunPollingMilliseconds,
            cancellationToken);

        if (waitResult.isSuccess)
        {
            _logService.Info(nameof(WinEcrAutoRunService), waitResult.message);
        }
        else
        {
            _logService.Warning(nameof(WinEcrAutoRunService), waitResult.message);
            if (!string.IsNullOrWhiteSpace(waitResult.errorDetails))
            {
                _logService.Warning(nameof(WinEcrAutoRunService), $"Dettaglio errore registratore: {waitResult.errorDetails}");
            }
        }

        return new WinEcrAutoRunResult
        {
            IsSuccess = waitResult.isSuccess,
            Message = waitResult.message,
            ErrorDetails = waitResult.errorDetails,
            EcrErrorCode = waitResult.ecrErrorCode,
            CommandFilePath = commandFilePath,
            GeneratedContent = content,
            ErrorFilePath = errorFilePath
        };
    }

    private static async Task<(bool isSuccess, string message, string? errorDetails, int? ecrErrorCode)> WaitForExecutionAsync(
        string commandFilePath,
        string errorFilePath,
        string expectedContent,
        int pollingMilliseconds,
        CancellationToken cancellationToken)
    {
        var poll = pollingMilliseconds <= 0 ? 300 : pollingMilliseconds;
        var timeoutAt = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(errorFilePath) && File.Exists(errorFilePath))
            {
                var errorContent = (await File.ReadAllTextAsync(errorFilePath, cancellationToken)).Trim();
                if (!string.IsNullOrWhiteSpace(errorContent))
                {
                    var errorCode = ExtractEcrErrorCode(errorContent);
                    var userMessage = errorCode.HasValue
                        ? $"WinEcr ha segnalato un errore del registratore (codice {errorCode.Value}). Controllare {errorFilePath}."
                        : $"WinEcr ha segnalato un errore del registratore. Controllare {errorFilePath}.";
                    return (false, userMessage, errorContent, errorCode);
                }
            }

            if (!File.Exists(commandFilePath))
            {
                return (true, $"WinEcr ha preso in carico il comando fiscale {commandFilePath}.", null, null);
            }

            var currentContent = (await File.ReadAllTextAsync(commandFilePath, cancellationToken)).Trim();
            if (string.IsNullOrWhiteSpace(currentContent))
            {
                return (true, $"WinEcr ha elaborato il comando fiscale {commandFilePath}.", null, null);
            }

            if (!string.Equals(currentContent, expectedContent.Trim(), StringComparison.Ordinal))
            {
                return (true, $"WinEcr ha modificato il comando fiscale {commandFilePath}, presa in carico confermata.", null, null);
            }

            await Task.Delay(poll, cancellationToken);
        }

        return (false, $"Timeout in attesa della presa in carico di {commandFilePath} da parte di WinEcr.", null, null);
    }

    private async Task<IReadOnlyList<string>> BuildLoyaltyFooterLinesAsync(
        DocumentoLocale documento,
        CancellationToken cancellationToken)
    {
        if (!documento.ClienteOid.HasValue || documento.ClienteOid.Value <= 1)
        {
            return [];
        }

        var customer = await _customerReadService.GetCustomerByOidAsync(documento.ClienteOid.Value, cancellationToken);
        if (customer is null)
        {
            return [];
        }

        var lines = new List<string>();
        foreach (var wrappedLine in WrapReceiptLine($"Cliente: {customer.DisplayName}"))
        {
            lines.Add(wrappedLine);
        }

        if (customer.HaRaccoltaPunti != true)
        {
            return lines;
        }

        var campaigns = await _pointsReadService.GetCampaignsAsync(cancellationToken);
        var campaign = campaigns.FirstOrDefault(c => c.Attiva == true) ?? campaigns.FirstOrDefault();
        var rewardRules = campaign is null
            ? []
            : await _rewardRuleService.GetAsync(campaign.Oid, cancellationToken);
        var summary = _pointsCustomerBalanceService.BuildSummary(customer, campaign, rewardRules, documento);
        var puntiPrima = customer.PuntiDisponibili ?? 0m;
        var puntiMaturati = summary.CurrentDocumentPoints;
        var puntiSpesi = ResolveSpentPoints(documento, rewardRules);
        var puntiDopo = puntiPrima + puntiMaturati - puntiSpesi;

        lines.Add($"Punti precedenti: {FormatPoints(puntiPrima)}");
        lines.Add($"Punti maturati : {FormatPoints(puntiMaturati)}");
        if (puntiSpesi > 0)
        {
            lines.Add($"Punti spesi    : {FormatPoints(puntiSpesi)}");
        }

        lines.Add($"Saldo punti    : {FormatPoints(puntiDopo)}");
        return lines;
    }

    private static decimal ResolveSpentPoints(
        DocumentoLocale documento,
        IReadOnlyList<PointsRewardRule> rewardRules)
    {
        var promoRow = documento.Righe.FirstOrDefault(riga =>
            riga.IsPromoRow &&
            riga.PromoCampaignOid.HasValue &&
            !string.IsNullOrWhiteSpace(riga.PromoRuleId));
        if (promoRow is null || !Guid.TryParse(promoRow.PromoRuleId, out var ruleId))
        {
            return 0m;
        }

        return rewardRules
            .FirstOrDefault(rule => rule.Id == ruleId)?
            .RequiredPoints
            .GetValueOrDefault() ?? 0m;
    }

    private static string BuildCommandContent(
        DocumentoLocale documento,
        string footerText,
        IReadOnlyList<string> loyaltyFooterLines)
    {
        var builder = new StringBuilder();
        var footerLines = SplitFooterLines(footerText)
            .Concat(loyaltyFooterLines)
            .ToList();
        var contanti = SommaPagamenti(documento, "contanti", "contante");
        var carta = SommaPagamenti(documento, "carta", "bancomat", "pos");
        var sospeso = SommaPagamenti(documento, "sospeso");
        var totale = documento.TotaleDaIncassareLocale;
        var resto = documento.Resto;
        var usaTerminale = carta > 0;

        builder.AppendLine("CLEAR");
        builder.AppendLine("CLEAR");
        builder.AppendLine("CHIAVE REG");

        if (usaTerminale)
        {
            builder.AppendLine("inp term=178");
        }

        foreach (var riga in documento.Righe.OrderBy(item => item.OrdineRiga))
        {
            builder.AppendLine(BuildVendLine(riga));
        }

        builder.AppendLine(BuildSubtotalCommand(documento));

        if (footerLines.Count > 0)
        {
            // L'allegato va aperto prima della chiusura fiscale, come negli esempi ufficiali WinEcr 3.0.
            builder.AppendLine("alleg on");
        }

        if (contanti > 0)
        {
            builder.AppendLine($"chius t=1, imp={FormatAmount(contanti)}");
        }

        if (carta > 0)
        {
            builder.AppendLine($"chius t=5, imp={FormatAmount(carta)}");
        }

        if (sospeso > 0)
        {
            builder.AppendLine($"chius t=2, imp={FormatAmount(sospeso)}");
        }

        if (usaTerminale)
        {
            builder.AppendLine("inp term=179");
        }

        if (footerLines.Count > 0)
        {
            foreach (var footerLine in footerLines)
            {
                builder.AppendLine($"alleg riga='{EscapeText(footerLine)}'");
            }

            builder.AppendLine("alleg fine");
        }

        builder.AppendLine($"vis cli1='T0TALE: {FormatDisplayAmount(totale)}'");
        builder.AppendLine($"vis ope1='T0TALE: {FormatDisplayAmount(totale)}'");
        builder.AppendLine($"vis cli2='Resto : {FormatDisplayAmount(resto)}'");
        builder.AppendLine($"vis ope2='Resto : {FormatDisplayAmount(resto)}'");

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildSubtotalCommand(DocumentoLocale documento)
    {
        var scontoDocumento = documento.TotaleScontoLocale;
        if (scontoDocumento <= 0)
        {
            return "SUBT";
        }

        // Lo sconto documento va tradotto in abbuono fiscale di subtotale verso WinEcr.
        return $"SCONTO VAL={FormatAmount(scontoDocumento)},SUBTOT";
    }

    private static string BuildDailyJournalCommandContent(CashJournalMode journalMode)
    {
        var reportCode = journalMode switch
        {
            CashJournalMode.Long => 3,
            CashJournalMode.Medium => 4,
            _ => 2
        };

        var builder = new StringBuilder();
        builder.AppendLine("CLEAR");
        builder.AppendLine($"report num={reportCode}, modo=0");
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildCloseCashCommandContent(CashJournalMode journalMode)
    {
        var closeType = journalMode switch
        {
            CashJournalMode.Long => 1,
            CashJournalMode.Medium => 3,
            _ => 2
        };

        var builder = new StringBuilder();
        builder.AppendLine("CLEAR");
        builder.AppendLine($"azzgio tipo={closeType}");
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildReceiptReprintCommandContent(CashRegisterOptionSelection selection)
    {
        if (!selection.ReceiptNumber.HasValue || !selection.ReceiptDate.HasValue)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("CLEAR");
        builder.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"dgfe datai={FormatFiscalDate(selection.ReceiptDate.Value)}, dataf={FormatFiscalDate(selection.ReceiptDate.Value)}, numscoi={FormatReceiptNumber(selection.ReceiptNumber.Value)}, numscof={FormatReceiptNumber(selection.ReceiptNumber.Value)}, stampa=si"));
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildReceiptCancellationCommandContent(
        CashRegisterOptionSelection selection,
        DocumentoLocale? documento)
    {
        if (documento is null ||
            !selection.ReceiptNumber.HasValue ||
            !selection.ReceiptDate.HasValue ||
            documento.Righe.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("RESPRN");
        builder.AppendLine("SETP ABORTFILE=SI");

        var docAnnullo = string.Create(
            CultureInfo.InvariantCulture,
            $"DOCANNULLO NUMSCO={FormatReceiptNumber(selection.ReceiptNumber.Value)}, DATASCO={FormatFiscalDate(selection.ReceiptDate.Value)}");

        var machineId = string.IsNullOrWhiteSpace(selection.ReceiptMachineId)
            ? "ND"
            : selection.ReceiptMachineId.Trim();
        docAnnullo += string.Create(CultureInfo.InvariantCulture, $", MATR='{EscapeText(machineId)}'");
        builder.AppendLine(docAnnullo);

        foreach (var riga in documento.Righe.OrderBy(item => item.OrdineRiga))
        {
            builder.AppendLine(BuildVendLine(riga));
        }

        builder.AppendLine("CHIUS");
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildVendLine(RigaDocumentoLocale riga)
    {
        var reparto = Math.Max(1, riga.IvaOid);
        var quantity = riga.Quantita <= 0 ? 1m : riga.Quantita;
        var effectiveUnitPrice = quantity == 0
            ? 0
            : Math.Round(riga.ImportoRiga / quantity, 2, MidpointRounding.AwayFromZero);

        return string.Create(CultureInfo.InvariantCulture, $"vend rep={reparto}, pre={FormatAmount(effectiveUnitPrice)}, qty={FormatQuantity(quantity)}, des='{EscapeText(riga.Descrizione)}'");
    }

    private static decimal SommaPagamenti(DocumentoLocale documento, params string[] tipi)
    {
        return documento.Pagamenti
            .Where(pagamento => tipi.Any(tipo =>
                string.Equals(pagamento.TipoPagamento?.Trim(), tipo, StringComparison.OrdinalIgnoreCase)))
            .Sum(pagamento => pagamento.Importo);
    }

    private static string FormatAmount(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatDisplayAmount(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
    }

    private static string FormatFiscalDate(DateTime value)
    {
        return value.ToString("ddMMyy", CultureInfo.InvariantCulture);
    }

    private static string FormatReceiptNumber(int value)
    {
        return value.ToString("D4", CultureInfo.InvariantCulture);
    }

    private static string FormatPoints(decimal value)
    {
        return value.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));
    }

    private static string FormatQuantity(decimal quantity)
    {
        return quantity.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string EscapeText(string? value)
    {
        return (value ?? string.Empty).Replace("'", "''").Trim();
    }

    private static IReadOnlyList<string> SplitFooterLines(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static IReadOnlyList<string> WrapReceiptLine(string value, int maxLength = 38)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = Regex.Replace(value.Trim(), "\\s+", " ");
        if (normalized.Length <= maxLength)
        {
            return [normalized];
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= maxLength)
            {
                current.Append(' ').Append(word);
                continue;
            }

            lines.Add(current.ToString());
            current.Clear();
            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    private static int? ExtractEcrErrorCode(string errorContent)
    {
        if (string.IsNullOrWhiteSpace(errorContent))
        {
            return null;
        }

        var match = Regex.Match(errorContent, @"L'ECR\s+SEGNALA\s+ERRORE\s+(?<code>\d+)", RegexOptions.IgnoreCase);
        if (match.Success &&
            int.TryParse(match.Groups["code"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var errorCode))
        {
            return errorCode;
        }

        return null;
    }
}
