using Banco.Core.Domain.Entities;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Customers;

namespace Banco.Stampa;

public sealed class BancoPosPrintService : IBancoPosPrintService
{
    private readonly IFastReportRuntimeService _runtimeService;
    private readonly IPrintLayoutCatalogService _layoutCatalogService;
    private readonly IGestionalePointsReadService _pointsReadService;
    private readonly IPointsRewardRuleService _rewardRuleService;
    private readonly IPointsCustomerBalanceService _pointsCustomerBalanceService;

    public BancoPosPrintService(
        IFastReportRuntimeService runtimeService,
        IPrintLayoutCatalogService layoutCatalogService,
        IGestionalePointsReadService pointsReadService,
        IPointsRewardRuleService rewardRuleService,
        IPointsCustomerBalanceService pointsCustomerBalanceService)
    {
        _runtimeService = runtimeService;
        _layoutCatalogService = layoutCatalogService;
        _pointsReadService = pointsReadService;
        _rewardRuleService = rewardRuleService;
        _pointsCustomerBalanceService = pointsCustomerBalanceService;
    }

    public async Task<FastReportRuntimeActionResult> PreviewCortesiaAsync(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer = null,
        CancellationToken cancellationToken = default)
    {
        if (documento is null)
        {
            return new FastReportRuntimeActionResult
            {
                IsSupported = false,
                Succeeded = false,
                Message = "Nessun documento Banco disponibile."
            };
        }

        var layout = await ResolveReceiptLayoutAsync(cancellationToken);
        var previewDocument = await BuildPreviewDocumentAsync(documento, customer, cancellationToken);
        return await _runtimeService.PreviewDocumentAsync(layout.TemplateFileName!, previewDocument, cancellationToken);
    }

    public async Task<FastReportRuntimeActionResult> PrintCortesiaAsync(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer = null,
        CancellationToken cancellationToken = default)
    {
        if (documento is null)
        {
            return new FastReportRuntimeActionResult
            {
                IsSupported = false,
                Succeeded = false,
                Message = "Nessun documento Banco disponibile."
            };
        }

        var layout = await ResolveReceiptLayoutAsync(cancellationToken);
        var previewDocument = await BuildPreviewDocumentAsync(documento, customer, cancellationToken);
        return await _runtimeService.PrintDocumentAsync(
            layout.TemplateFileName!,
            previewDocument,
            layout.AssignedPrinterName,
            cancellationToken);
    }

    private async Task<PrintLayoutDefinition> ResolveReceiptLayoutAsync(CancellationToken cancellationToken)
    {
        var layouts = await _layoutCatalogService.GetLayoutsAsync(cancellationToken);
        var selectedLayout = layouts
            .Where(layout => layout.IsEnabled &&
                             string.Equals(layout.DocumentKey, "receipt-80-db", StringComparison.OrdinalIgnoreCase) &&
                             layout.Engine == PrintEngineKind.FastReport &&
                             !string.IsNullOrWhiteSpace(layout.TemplateFileName))
            .OrderByDescending(layout => layout.IsDefault)
            .ThenBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return selectedLayout ?? new PrintLayoutDefinition
        {
            Id = "fastreport-pos-80",
            DocumentKey = "receipt-80-db",
            DisplayName = "FastReport POS 80 mm",
            Engine = PrintEngineKind.FastReport,
            TemplateFileName = "Pos.frx",
            IsDefault = true,
            IsEnabled = true
        };
    }

    private async Task<FastReportPreviewDocument> BuildPreviewDocumentAsync(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer,
        CancellationToken cancellationToken)
    {
        var righe = documento.Righe
            .OrderBy(riga => riga.OrdineRiga)
            .Select(MapRow)
            .ToArray();

        var pagamenti = documento.Pagamenti
            .OrderBy(pagamento => pagamento.DataOra)
            .Select(MapPayment)
            .ToArray();

        var totalPaid = documento.TotalePagatoLocale;
        var totalDiscount = documento.TotaleScontoLocale;
        var cardAmount = SumPayments(documento, "carta", "bancomat", "pos");
        var cashAmount = SumPayments(documento, "contanti", "contante");

        return new FastReportPreviewDocument
        {
            Testata = new FastReportPreviewHeader
            {
                DocumentoOid = documento.DocumentoGestionaleOid ?? 0,
                Numero = (int)(documento.NumeroDocumentoGestionale ?? 0),
                Anno = documento.AnnoDocumentoGestionale ?? DateTime.Today.Year,
                Data = documento.DataDocumentoGestionale ?? documento.DataCreazione.Date,
                EtichettaDocumento = "Cortesia",
                ModelloDocumento = "Banco 80 mm",
                Operatore = documento.Operatore,
                StatoRuntime = documento.Stato.ToString(),
                NumeroCompleto = ResolveDisplayDocumentLabel(documento),
                DataTesto = (documento.DataDocumentoGestionale ?? documento.DataCreazione.Date).ToString("dd/MM/yyyy"),
                PagamentoLabel = ResolvePaymentLabel(documento),
                DocumentoLabel = "Vendita al banco",
                AnnoVisuale = (documento.AnnoDocumentoGestionale ?? DateTime.Today.Year).ToString(),
                ProgressivoVenditaLabel = BuildProgressivoLabel(documento)
            },
            Cliente = await BuildCustomerAsync(customer, documento, cancellationToken),
            Righe = righe,
            Pagamenti = pagamenti,
            Totali = new FastReportPreviewTotals
            {
                TotaleDocumento = documento.TotaleDocumento,
                TotaleImponibile = documento.TotaleDocumento,
                TotaleIva = 0,
                TotalePagato = totalPaid,
                TotaleDocumentoVisuale = FormatMoney(documento.TotaleDocumento),
                TotalePagatoVisuale = FormatMoney(totalPaid),
                ScontoPercentualeVisuale = totalDiscount > 0 ? "0" : "0",
                ScontoImportoVisuale = FormatMoneyWithCurrency(totalDiscount),
                ContantiVisuale = FormatMoneyWithCurrency(cashAmount),
                PagatoCartaVisuale = FormatMoneyWithCurrency(cardAmount),
                PagamentoPrincipaleLabel = ResolvePaymentLabel(documento),
                PagamentoPrincipaleImportoVisuale = FormatMoneyWithCurrency(ResolvePrimaryPaymentAmount(documento)),
                RestoVisuale = FormatMoneyWithCurrency(documento.Resto)
            }
        };
    }

    private async Task<FastReportPreviewCustomer> BuildCustomerAsync(
        GestionaleCustomerSummary? customer,
        DocumentoLocale documento,
        CancellationToken cancellationToken)
    {
        if (customer is null)
        {
            return new FastReportPreviewCustomer
            {
                ClienteOid = documento.ClienteOid ?? 0,
                Nominativo = string.IsNullOrWhiteSpace(documento.Cliente) ? "Cliente generico" : documento.Cliente,
                PuntiPrecedentiVisuale = "n.d.",
                PuntiAttualiVisuale = "0"
            };
        }

        var puntiPrima = customer.PuntiDisponibili ?? 0m;
        var puntiDopo = puntiPrima;
        if (customer.HaRaccoltaPunti)
        {
            var campaigns = await _pointsReadService.GetCampaignsAsync(cancellationToken);
            var campaign = campaigns.FirstOrDefault(c => c.Attiva == true) ?? campaigns.FirstOrDefault();
            var rewardRules = campaign is null
                ? []
                : await _rewardRuleService.GetAsync(campaign.Oid, cancellationToken);
            var summary = _pointsCustomerBalanceService.BuildSummary(customer, campaign, rewardRules, documento);
            puntiDopo = Math.Max(0m, summary.TotalAvailablePoints - ResolveSpentPoints(documento, rewardRules));
        }

        return new FastReportPreviewCustomer
        {
            ClienteOid = customer.Oid,
            Nominativo = string.IsNullOrWhiteSpace(customer.DisplayName) ? customer.DisplayLabel : customer.DisplayName,
            CodiceCartaFedelta = customer.CodiceCartaFedelta ?? string.Empty,
            PuntiPrecedentiVisuale = FormatPoints(puntiPrima),
            PuntiAttualiVisuale = FormatPoints(puntiDopo)
        };
    }

    private static decimal ResolveSpentPoints(
        DocumentoLocale documento,
        IReadOnlyList<Banco.Vendita.Points.PointsRewardRule> rewardRules)
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

    private static FastReportPreviewRow MapRow(RigaDocumentoLocale row)
    {
        var quantitaVisuale = row.Quantita == 0 ? string.Empty : row.Quantita.ToString("0.##");
        var prezzoVisuale = row.Quantita == 0 || row.PrezzoUnitario == 0 ? string.Empty : FormatMoney(row.PrezzoUnitario);
        var scontoVisuale = row.ScontoPercentuale <= 0 ? string.Empty : row.ScontoPercentuale.ToString("0.##");
        var sconto2Visuale = row.Sconto2 <= 0 ? string.Empty : row.Sconto2.ToString("0.##");
        var importoVisuale = row.Quantita == 0 || row.ImportoRiga == 0 ? string.Empty : FormatMoney(row.ImportoRiga);

        return new FastReportPreviewRow
        {
            RigaOid = row.Id.GetHashCode(),
            CodiceArticolo = row.CodiceArticolo ?? string.Empty,
            Barcode = row.BarcodeArticolo ?? string.Empty,
            Descrizione = row.Descrizione,
            OrdineRiga = row.OrdineRiga,
            Quantita = row.Quantita,
            UnitaMisura = row.UnitaMisura,
            PrezzoUnitario = row.PrezzoUnitario,
            ScontoPercentuale = row.ScontoPercentuale,
            Sconto2 = row.Sconto2,
            ImportoRiga = row.ImportoRiga,
            AliquotaIva = row.AliquotaIva,
            QuantitaVisuale = quantitaVisuale,
            PrezzoUnitarioVisuale = prezzoVisuale,
            ScontoVisuale = scontoVisuale,
            Sconto2Visuale = sconto2Visuale,
            ImportoRigaVisuale = importoVisuale
        };
    }

    private static FastReportPreviewPayment MapPayment(PagamentoLocale payment)
    {
        return new FastReportPreviewPayment
        {
            Tipo = ResolvePaymentTypeLabel(payment.TipoPagamento),
            Importo = payment.Importo,
            ImportoVisuale = FormatMoney(payment.Importo)
        };
    }

    private static decimal SumPayments(DocumentoLocale documento, params string[] acceptedTypes)
    {
        return documento.Pagamenti
            .Where(payment => acceptedTypes.Any(type =>
                string.Equals(payment.TipoPagamento?.Trim(), type, StringComparison.OrdinalIgnoreCase)))
            .Sum(payment => payment.Importo);
    }

    private static string ResolvePaymentLabel(DocumentoLocale documento)
    {
        if (documento.Pagamenti.Count == 0)
        {
            return "Nessuno";
        }

        var primary = documento.Pagamenti
            .OrderByDescending(payment => payment.Importo)
            .ThenBy(payment => payment.DataOra)
            .FirstOrDefault();

        return primary is null ? "Nessuno" : ResolvePaymentTypeLabel(primary.TipoPagamento);
    }

    private static decimal ResolvePrimaryPaymentAmount(DocumentoLocale documento)
    {
        return documento.Pagamenti
            .OrderByDescending(payment => payment.Importo)
            .ThenBy(payment => payment.DataOra)
            .Select(payment => payment.Importo)
            .FirstOrDefault();
    }

    private static string ResolvePaymentTypeLabel(string? paymentType)
    {
        var normalized = paymentType?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "contanti" or "contante" => "Contanti",
            "carta" or "bancomat" or "pos" => "Carta",
            "buoni" or "buonipasto" or "ticket" => "Buoni",
            "sospeso" => "Sospeso",
            "web" => "Web",
            _ => string.IsNullOrWhiteSpace(paymentType) ? "Altro" : paymentType.Trim()
        };
    }

    private static string BuildProgressivoLabel(DocumentoLocale documento)
    {
        if (documento.NumeroDocumentoGestionale.HasValue && documento.AnnoDocumentoGestionale.HasValue)
        {
            return $"Progressivo vendita {documento.NumeroDocumentoGestionale}/{documento.AnnoDocumentoGestionale}";
        }

        return $"Progressivo vendita {documento.Id.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string ResolveDisplayDocumentLabel(DocumentoLocale documento)
    {
        if (documento.NumeroDocumentoGestionale.HasValue && documento.AnnoDocumentoGestionale.HasValue)
        {
            return $"{documento.NumeroDocumentoGestionale}/{documento.AnnoDocumentoGestionale}";
        }

        return "Anteprima Banco";
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N2");
    }

    private static string FormatMoneyWithCurrency(decimal value)
    {
        return $"{FormatMoney(value)} EUR";
    }

    private static string FormatPoints(decimal value)
    {
        return value.ToString("0.##");
    }
}
