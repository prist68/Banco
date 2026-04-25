using System.Text.Json;
using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Points;

namespace Banco.Vendita.Fiscal;

public sealed class BancoDocumentWorkflowService : IBancoDocumentWorkflowService
{
    private const int ModellodocumentoBanco = 27;
    private const int SoggettoFallbackOid = 1;
    private const int PagamentoFallbackOid = 10;
    private const int MagazzinoFallbackOid = 1;
    private const int CausaleMagazzinoFallbackOid = 21;

    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IGestionaleDocumentWriter _documentWriter;
    private readonly IGestionaleDocumentDeleteService _documentDeleteService;
    private readonly ILocalAuditRepository _localAuditRepository;
    private readonly IWinEcrAutoRunService _winEcrAutoRunService;
    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IGestionalePointsReadService _pointsReadService;
    private readonly IPointsRewardRuleService _rewardRuleService;
    private readonly IPointsCustomerBalanceService _pointsCustomerBalanceService;
    private readonly IGestionaleFidelityHistoryService _fidelityHistoryService;

    public BancoDocumentWorkflowService(
        IGestionaleDocumentReadService documentReadService,
        IGestionaleDocumentWriter documentWriter,
        IGestionaleDocumentDeleteService documentDeleteService,
        ILocalAuditRepository localAuditRepository,
        IWinEcrAutoRunService winEcrAutoRunService,
        IGestionaleCustomerReadService customerReadService,
        IGestionalePointsReadService pointsReadService,
        IPointsRewardRuleService rewardRuleService,
        IPointsCustomerBalanceService pointsCustomerBalanceService,
        IGestionaleFidelityHistoryService fidelityHistoryService)
    {
        _documentReadService = documentReadService;
        _documentWriter = documentWriter;
        _documentDeleteService = documentDeleteService;
        _localAuditRepository = localAuditRepository;
        _winEcrAutoRunService = winEcrAutoRunService;
        _customerReadService = customerReadService;
        _pointsReadService = pointsReadService;
        _rewardRuleService = rewardRuleService;
        _pointsCustomerBalanceService = pointsCustomerBalanceService;
        _fidelityHistoryService = fidelityHistoryService;
    }

    public async Task<FiscalizationResult> PublishAsync(
        DocumentoLocale documento,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        BancoPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var publishOptions = options ?? BancoPublishOptions.Default;
        ValidateDocument(documento, categoriaDocumentoBanco, publishOptions);
        var warningMessages = new List<string>();

        var paymentBreakdown = BuildPaymentBreakdown(documento);
        var hasComponenteSospeso = paymentBreakdown.Sospeso > 0;
        var dataPagamentoFinale = ResolveDataPagamentoFinale(documento);

        await TrySaveAuditAsync(new EventoAudit
        {
            EntityType = "DocumentoLocale",
            EntityId = documento.Id.ToString("D"),
            EventType = "BancoWorkflowAvviato",
            Operatore = documento.Operatore,
            PayloadSinteticoJson = JsonSerializer.Serialize(new
            {
                DocumentoId = documento.Id,
                Categoria = categoriaDocumentoBanco.ToString(),
                documento.ClienteOid,
                Righe = documento.Righe.Count,
                Totale = documento.TotaleDocumento,
                documento.DocumentoGestionaleOid
            }),
            Esito = "Avvio"
        }, warningMessages, "Audit avvio workflow non disponibile", cancellationToken);

        try
        {
            if (publishOptions.DeleteExistingNonFiscalizedLegacyDocument &&
                documento.DocumentoGestionaleOid.HasValue)
            {
                await _documentDeleteService.DeleteNonFiscalizedDocumentAsync(
                    documento.DocumentoGestionaleOid.Value,
                    cancellationToken);
            }

            var (numeroDocumento, annoDocumento, dataDocumento) =
                await ResolveLegacyNumberingAsync(documento, publishOptions, cancellationToken);
            var pointsSettlement = await BuildPointsSettlementAsync(documento, cancellationToken);

            var request = new FiscalizationRequest
            {
                LocalDocumentId = documento.Id,
                ModellodocumentoOid = ModellodocumentoBanco,
                Numero = numeroDocumento,
                Anno = annoDocumento,
                DataDocumento = dataDocumento,
                SoggettoOid = documento.ClienteOid.GetValueOrDefault(SoggettoFallbackOid),
                ListinoOid = documento.ListinoOid,
                Operatore = string.IsNullOrWhiteSpace(documento.Operatore) ? "Admin Locale" : documento.Operatore.Trim(),
                MagazzinoOid = MagazzinoFallbackOid,
                PagamentoOid = PagamentoFallbackOid,
                CausaleMagazzinoOid = CausaleMagazzinoFallbackOid,
                TotaleDocumento = documento.TotaleDocumento,
                Pagamenti = paymentBreakdown,
                PointsSettlement = pointsSettlement,
                Righe = documento.Righe
                    .OrderBy(row => row.OrdineRiga)
                    .Select(row => new FiscalizationRow
                    {
                        OrdineRiga = row.OrdineRiga,
                        ArticoloOid = row.FlagManuale ? null : row.ArticoloOid,
                        CodiceArticolo = row.FlagManuale ? null : row.CodiceArticolo,
                        BarcodeArticolo = row.FlagManuale ? null : row.BarcodeArticolo,
                        UnitaMisura = string.IsNullOrWhiteSpace(row.UnitaMisura) ? "PZ" : row.UnitaMisura,
                        VarianteDettaglioOid1 = row.FlagManuale ? null : row.VarianteDettaglioOid1,
                        VarianteDettaglioOid2 = row.FlagManuale ? null : row.VarianteDettaglioOid2,
                        Descrizione = row.Descrizione,
                        Quantita = row.Quantita,
                        ValoreUnitario = row.PrezzoUnitario,
                        ImportoRiga = row.ImportoRiga,
                        IvaOid = row.IvaOid,
                        Sconto1 = row.Sconto1,
                        Sconto2 = row.Sconto2,
                        Sconto3 = row.Sconto3,
                        Sconto4 = row.Sconto4,
                        FlagManuale = row.FlagManuale
                    })
                    .ToList()
            };

            var result = await _documentWriter.UpsertFiscalDocumentAsync(
                request,
                publishOptions.ForceNewLegacyDocument ? null : documento.DocumentoGestionaleOid,
                cancellationToken);

            await TryRecalculateFidelityBalanceAsync(
                documento,
                result,
                warningMessages,
                cancellationToken);

            if (pointsSettlement is not null)
            {
                await TrySaveAuditAsync(new EventoAudit
                {
                    EntityType = "DocumentoLocale",
                    EntityId = documento.Id.ToString("D"),
                    EventType = "BancoWorkflowPuntiSettlement",
                    Operatore = documento.Operatore,
                    PayloadSinteticoJson = JsonSerializer.Serialize(new
                    {
                        documento.ClienteOid,
                        result.DocumentoGestionaleOid,
                        result.NumeroDocumentoGestionale,
                        result.AnnoDocumentoGestionale,
                        pointsSettlement.CampaignOid,
                        pointsSettlement.StartingCurrentPoints,
                        pointsSettlement.EarnedPoints,
                        pointsSettlement.SpentPoints,
                        pointsSettlement.DeltaPoints,
                        FinalCurrentPoints = pointsSettlement.StartingCurrentPoints + pointsSettlement.DeltaPoints
                    }),
                    Esito = "Ok"
                }, warningMessages, "Audit settlement punti non disponibile", cancellationToken);
            }

            var winEcrMessage = string.Empty;
            var winEcrExecuted = false;
            string? winEcrErrorDetails = null;
            int? winEcrErrorCode = null;
            if (categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino && publishOptions.SkipWinEcr)
            {
                documento.SegnaAggiornatoLegacySenzaNuovaFiscalizzazione(
                    result.DocumentoGestionaleOid,
                    result.NumeroDocumentoGestionale,
                    result.AnnoDocumentoGestionale,
                    result.DataDocumentoGestionale,
                    hasComponenteSospeso,
                    dataPagamentoFinale);
                winEcrExecuted = true;
                winEcrMessage = "Documento scontrino aggiornato senza nuova stampa fiscale.";
            }
            else
            {
                documento.SegnaPubblicatoLegacy(
                    result.DocumentoGestionaleOid,
                    result.NumeroDocumentoGestionale,
                    result.AnnoDocumentoGestionale,
                    result.DataDocumentoGestionale,
                    categoriaDocumentoBanco,
                    hasComponenteSospeso,
                    dataPagamentoFinale);
            }

            if (categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino && !publishOptions.SkipWinEcr)
            {
                try
                {
                    var winEcrResult = await _winEcrAutoRunService.GenerateReceiptAsync(documento, cancellationToken);
                    winEcrExecuted = winEcrResult.IsSuccess;
                    winEcrMessage = winEcrResult.Message;
                    winEcrErrorDetails = winEcrResult.ErrorDetails;
                    winEcrErrorCode = winEcrResult.EcrErrorCode;
                }
                catch (Exception ex)
                {
                    winEcrExecuted = false;
                    winEcrMessage = $"WinEcr non completato: {ex.Message}";
                }

                if (winEcrExecuted)
                {
                    documento.SegnaFiscalizzazioneWinEcrCompletata(DateTimeOffset.Now);

                    try
                    {
                        await _documentWriter.MarkLegacyReceiptCompletedAsync(
                            result.DocumentoGestionaleOid,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        warningMessages.Add($"Allineamento stato scontrino legacy non disponibile: {ex.Message}.");
                    }
                }
                else
                {
                    documento.SegnaFiscalizzazioneWinEcrFallita(DateTimeOffset.Now);
                }
            }

            await TrySaveAuditAsync(new EventoAudit
            {
                EntityType = "DocumentoLocale",
                EntityId = documento.Id.ToString("D"),
                EventType = "BancoWorkflowCompletato",
                Operatore = documento.Operatore,
                PayloadSinteticoJson = JsonSerializer.Serialize(new
                {
                    Categoria = categoriaDocumentoBanco.ToString(),
                    result.DocumentoGestionaleOid,
                    result.NumeroDocumentoGestionale,
                    result.AnnoDocumentoGestionale,
                    result.DataDocumentoGestionale,
                    WinEcrMessage = winEcrMessage,
                    WinEcrErrorCode = winEcrErrorCode,
                    WinEcrErrorDetails = winEcrErrorDetails
                }),
                Esito = "Ok"
            }, warningMessages, "Audit finale workflow non disponibile", cancellationToken);

            var technicalWarningMessage = warningMessages.Count == 0
                ? null
                : string.Join(" ", warningMessages.Distinct());
            var outcomeKind = categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino && !winEcrExecuted
                ? LegacyPublishOutcomeKind.LegacyPublishedWinEcrIncomplete
                : string.IsNullOrWhiteSpace(technicalWarningMessage)
                    ? LegacyPublishOutcomeKind.LegacyPublished
                    : LegacyPublishOutcomeKind.LegacyPublishedWithTechnicalWarning;

            return new FiscalizationResult
            {
                DocumentoGestionaleOid = result.DocumentoGestionaleOid,
                NumeroDocumentoGestionale = result.NumeroDocumentoGestionale,
                AnnoDocumentoGestionale = result.AnnoDocumentoGestionale,
                DataDocumentoGestionale = result.DataDocumentoGestionale,
                Message = result.Message,
                WinEcrExecuted = winEcrExecuted,
                WinEcrMessage = winEcrMessage,
                WinEcrErrorDetails = winEcrErrorDetails,
                WinEcrErrorCode = winEcrErrorCode,
                OutcomeKind = outcomeKind,
                TechnicalWarningMessage = technicalWarningMessage
            };
        }
        catch (Exception ex)
        {
            await TrySaveAuditAsync(new EventoAudit
            {
                EntityType = "DocumentoLocale",
                EntityId = documento.Id.ToString("D"),
                EventType = "BancoWorkflowFallito",
                Operatore = documento.Operatore,
                PayloadSinteticoJson = JsonSerializer.Serialize(new
                {
                    DocumentoId = documento.Id,
                    Categoria = categoriaDocumentoBanco.ToString(),
                    Errore = ex.Message
                }),
                Esito = "Errore"
            }, warningMessages, "Audit errore workflow non disponibile", cancellationToken);

            throw;
        }
    }

    private async Task TryRecalculateFidelityBalanceAsync(
        DocumentoLocale documento,
        FiscalizationResult result,
        ICollection<string> warningMessages,
        CancellationToken cancellationToken)
    {
        var customerOid = documento.ClienteOid.GetValueOrDefault();
        if (customerOid <= 0)
        {
            return;
        }

        var customer = await _customerReadService.GetCustomerByOidAsync(customerOid, cancellationToken);
        if (customer?.HaRaccoltaPunti != true)
        {
            return;
        }

        try
        {
            var recalculation = await _fidelityHistoryService.RecalculateCustomerBalanceAsync(
                customerOid,
                persistToLegacy: true,
                operatore: string.IsNullOrWhiteSpace(documento.Operatore) ? "Admin Locale" : documento.Operatore.Trim(),
                cancellationToken: cancellationToken);

            if (recalculation is null)
            {
                return;
            }

            await TrySaveAuditAsync(new EventoAudit
            {
                EntityType = "DocumentoLocale",
                EntityId = documento.Id.ToString("D"),
                EventType = "BancoWorkflowPuntiRicalcolati",
                Operatore = documento.Operatore,
                PayloadSinteticoJson = JsonSerializer.Serialize(new
                {
                    documento.ClienteOid,
                    result.DocumentoGestionaleOid,
                    result.NumeroDocumentoGestionale,
                    result.AnnoDocumentoGestionale,
                    recalculation.PreviousLegacyCurrentPoints,
                    recalculation.ComputedCurrentPoints,
                    recalculation.DeltaPoints,
                    recalculation.LegacyUpdated
                }),
                Esito = "Ok"
            }, warningMessages, "Audit ricalcolo punti non disponibile", cancellationToken);
        }
        catch (Exception ex)
        {
            warningMessages.Add($"Ricalcolo saldo fidelity non disponibile: {ex.Message}.");
        }
    }

    private async Task TrySaveAuditAsync(
        EventoAudit evento,
        ICollection<string> warningMessages,
        string warningPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            await _localAuditRepository.SaveAsync(evento, cancellationToken);
        }
        catch (Exception ex)
        {
            warningMessages.Add($"{warningPrefix}: {ex.Message}.");
        }
    }

    private async Task<(long Numero, int Anno, DateTime DataDocumento)> ResolveLegacyNumberingAsync(
        DocumentoLocale documento,
        BancoPublishOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.ForceNewLegacyDocument &&
            documento.NumeroDocumentoGestionale.HasValue &&
            documento.AnnoDocumentoGestionale.HasValue &&
            documento.DataDocumentoGestionale.HasValue)
        {
            return (
                documento.NumeroDocumentoGestionale.Value,
                documento.AnnoDocumentoGestionale.Value,
                documento.DataDocumentoGestionale.Value);
        }

        var nextNumber = await _documentReadService.GetNextBancoDocumentNumberAsync(cancellationToken);
        return (nextNumber.Numero, nextNumber.Anno, DateTime.Today);
    }

    private async Task<FiscalizationPointsSettlement?> BuildPointsSettlementAsync(
        DocumentoLocale documento,
        CancellationToken cancellationToken)
    {
        var customerOid = documento.ClienteOid.GetValueOrDefault();
        if (customerOid <= 0)
        {
            return null;
        }

        var customer = await _customerReadService.GetCustomerByOidAsync(customerOid, cancellationToken);
        if (customer?.HaRaccoltaPunti != true)
        {
            return null;
        }

        var campaign = (await _pointsReadService.GetCampaignsAsync(cancellationToken))
            .Where(item => item.Attiva == true)
            .OrderBy(item => item.Oid)
            .FirstOrDefault();
        if (campaign is null)
        {
            return null;
        }

        var rewardRules = await _rewardRuleService.GetAsync(campaign.Oid, cancellationToken);
        var summary = _pointsCustomerBalanceService.BuildSummary(customer, campaign, rewardRules, documento);
        var startingCurrentPoints = customer.PuntiAssegnati ?? customer.PuntiTotali ?? customer.PuntiIniziali ?? 0m;
        var earnedPoints = summary.CurrentDocumentPoints;
        var spentPoints = ResolveSpentPoints(documento, rewardRules);
        if (earnedPoints == 0 && spentPoints == 0)
        {
            return null;
        }

        return new FiscalizationPointsSettlement
        {
            CustomerOid = customerOid,
            CampaignOid = campaign.Oid,
            StartingCurrentPoints = startingCurrentPoints,
            EarnedPoints = earnedPoints,
            SpentPoints = spentPoints
        };
    }

    private static DateTimeOffset? ResolveDataPagamentoFinale(DocumentoLocale documento)
    {
        if (documento.Pagamenti.Count == 0)
        {
            return DateTimeOffset.Now;
        }

        return documento.Pagamenti.MaxBy(pagamento => pagamento.DataOra)?.DataOra ?? DateTimeOffset.Now;
    }

    private static decimal ResolveSpentPoints(
        DocumentoLocale documento,
        IReadOnlyList<PointsRewardRule> rewardRules)
    {
        if (rewardRules.Count == 0)
        {
            return 0m;
        }

        decimal spentPoints = 0m;
        foreach (var row in documento.Righe.Where(item => item.IsPromoRow))
        {
            PointsRewardRule? matchedRule = null;
            if (!string.IsNullOrWhiteSpace(row.PromoRuleId) &&
                Guid.TryParse(row.PromoRuleId, out var ruleId))
            {
                matchedRule = rewardRules.FirstOrDefault(rule => rule.Id == ruleId);
            }

            matchedRule ??= rewardRules.FirstOrDefault(rule =>
                row.PromoCampaignOid.HasValue &&
                rule.CampaignOid == row.PromoCampaignOid.Value);

            if (matchedRule is null)
            {
                continue;
            }

            spentPoints += matchedRule.RequiredPoints.GetValueOrDefault();
        }

        return spentPoints;
    }

    private static void ValidateDocument(
        DocumentoLocale documento,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        BancoPublishOptions options)
    {
        if (documento.Stato == StatoDocumentoLocale.Annullato)
        {
            throw new InvalidOperationException("Il documento risulta annullato e non e` piu` utilizzabile.");
        }

        if (documento.Righe.Count == 0)
        {
            throw new InvalidOperationException("Inserire almeno una riga prima di pubblicare il documento Banco.");
        }

        if (categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino &&
            documento.Residuo > 0)
        {
            throw new InvalidOperationException($"Pagamenti incompleti. Residuo da coprire: {documento.Residuo:N2} EUR.");
        }

        if (documento.Righe.Any(row => row.IvaOid <= 0))
        {
            throw new InvalidOperationException("Ogni riga fiscale deve avere una IVA valida.");
        }

        if (categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino &&
            !options.SkipWinEcr &&
            documento.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata)
        {
            throw new InvalidOperationException("Il documento risulta gia` fiscalizzato via WinEcr.");
        }
    }

    private static FiscalizationPaymentBreakdown BuildPaymentBreakdown(DocumentoLocale documento)
    {
        decimal contanti = 0;
        decimal carta = 0;
        decimal web = 0;
        decimal buoni = 0;
        decimal sospeso = 0;

        foreach (var pagamento in documento.Pagamenti)
        {
            var tipo = pagamento.TipoPagamento?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (tipo)
            {
                case "contanti":
                case "contante":
                    contanti += pagamento.Importo;
                    break;
                case "carta":
                case "bancomat":
                case "pos":
                    carta += pagamento.Importo;
                    break;
                case "web":
                case "online":
                    web += pagamento.Importo;
                    break;
                case "buoni":
                case "ticket":
                case "buonipasto":
                    buoni += pagamento.Importo;
                    break;
                case "sospeso":
                    sospeso += pagamento.Importo;
                    break;
            }
        }

        return new FiscalizationPaymentBreakdown
        {
            Contanti = contanti,
            Carta = carta,
            Web = web,
            Buoni = buoni,
            Sospeso = sospeso
        };
    }
}
