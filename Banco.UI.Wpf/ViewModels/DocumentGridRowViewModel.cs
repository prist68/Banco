using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Banco.Vendita.Documents;

namespace Banco.UI.Wpf.ViewModels;

public sealed class DocumentGridRowViewModel
{
    public BancoDocumentoAccessMode AccessMode { get; init; } = BancoDocumentoAccessMode.LocaleNonPubblicato;

    public bool IsLocal { get; init; }

    public string SourceLabel => IsLocal ? "Supporto" : "Legacy";

    public string Modello => "Banco";

    public int? GestionaleOid { get; init; }

    public Guid? LocalDocumentId { get; init; }

    public string Operatore { get; init; } = string.Empty;

    public string NumeroDocumento { get; init; } = string.Empty;

    public DateTime Data { get; init; }

    public string DataLabel => Data.ToString("dd/MM/yyyy");

    public string Cliente { get; init; } = string.Empty;

    public decimal Totale { get; init; }

    public decimal? CustomerPoints { get; init; }

    public decimal PagContanti { get; init; }

    public decimal PagCarta { get; init; }

    public decimal PagWeb { get; init; }

    public decimal PagBuoni { get; init; }

    public decimal PagSospeso { get; init; }

    public decimal TotalePagato { get; init; }

    public decimal ResiduoPagamento { get; init; }

    public decimal DaFiscalizzare { get; init; }

    public CategoriaDocumentoBanco CategoriaDocumentoBanco { get; init; } = CategoriaDocumentoBanco.Indeterminata;

    public ModalitaChiusuraDocumento ModalitaChiusuraDocumento { get; init; } = ModalitaChiusuraDocumento.BozzaLocale;

    public StatoFiscaleBanco StatoFiscaleBanco { get; init; } = StatoFiscaleBanco.Nessuno;

    public bool HasSospesoComponent { get; init; }

    public bool HasLegacyFiscalSignal { get; init; }

    public bool HasLegacyNonScontrinatoSignal { get; init; }

    public bool HasLegacyScontrinatoPaymentSignal { get; init; }

    public bool IsScontrinato => ResolveIsScontrinato(
        ModalitaChiusuraDocumento,
        CategoriaDocumentoBanco,
        StatoFiscaleBanco,
        HasLegacyFiscalSignal,
        HasLegacyScontrinatoPaymentSignal,
        ScontrinoLabel);

    public bool IsCortesia => ResolveIsCortesia(CategoriaDocumentoBanco, HasLegacyNonScontrinatoSignal, IsScontrinato);

    public bool IsNonScontrinato => ResolveIsNonScontrinato(AccessMode, IsCortesia, IsScontrinato);

    public bool IsPubblicatoBanco => !IsScontrinato &&
                                     !IsCortesia &&
                                     ModalitaChiusuraDocumento == ModalitaChiusuraDocumento.PubblicazioneUfficiale &&
                                     CategoriaDocumentoBanco == CategoriaDocumentoBanco.Indeterminata &&
                                     StatoFiscaleBanco == StatoFiscaleBanco.PubblicatoLegacyNonFiscalizzato;

    public decimal TotaleContantiCartaScontrinato => IsScontrinato
        ? PagContanti + PagCarta
        : 0;

    public decimal TotaleContantiCortesiaONonScontrinato => IsNonScontrinato
        ? PagContanti + PagBuoni
        : 0;

    private bool HasDeleteTarget => LocalDocumentId.HasValue || GestionaleOid.HasValue;

    public bool CanDeleteFromList => HasDeleteTarget &&
                                     AccessMode != BancoDocumentoAccessMode.UfficialeConsultazione &&
                                     IsNonScontrinato &&
                                     StatoFiscaleBanco != StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata &&
                                     StatoFiscaleBanco != StatoFiscaleBanco.FiscalizzazioneWinEcrFallita;

    public bool HasClassificazioneFiscale => CategoriaDocumentoBanco != CategoriaDocumentoBanco.Indeterminata;

    public string CategoriaDocumentoLabel => CategoriaDocumentoBanco switch
    {
        _ when IsCortesia => "Cortesia",
        _ when IsScontrinato => "Scontrino",
        _ when IsNonScontrinato => "Non scontrinato",
        _ when IsPubblicatoBanco => "Pubblicato Banco",
        _ => "Legacy senza audit fiscale"
    };

    public string StatoDocumento { get; init; } = string.Empty;

    public string ScontrinoLabel { get; init; } = string.Empty;

    public string OpenDocumentActionLabel { get; init; } = "Apri nel Banco";

    public string OpenDocumentActionTooltip { get; init; } = "Apre il documento selezionato nel Banco.";

    public string NumeroSorgenteLabel => IsLocal
        ? NumeroDocumento
        : (GestionaleOid?.ToString() ?? string.Empty);

    public static DocumentGridRowViewModel FromGestionale(
        GestionaleDocumentSummary document,
        DocumentoLocale? localMetadata)
    {
        var categoria = localMetadata?.CategoriaDocumentoBanco ?? CategoriaDocumentoBanco.Indeterminata;
        var modalitaChiusura = localMetadata?.ModalitaChiusura ?? ModalitaChiusuraDocumento.BozzaLocale;
        var statoFiscale = localMetadata?.StatoFiscaleBanco ?? StatoFiscaleBanco.Nessuno;
        var hasSospeso = document.Pagatosospeso > 0 || (localMetadata?.HasComponenteSospeso ?? false);
        var hasReliableBancoScontrinoSignal = HasReliableBancoScontrinoSignal(localMetadata);
        var scontrinoLabel = ResolveScontrinoLabel(document.ScontrinoLabel, hasReliableBancoScontrinoSignal);
        var hasLegacyScontrinatoPaymentSignal = HasLegacyScontrinatoPayments(document.PagatoContanti, document.PagatoCarta, document.PagatoWeb);
        var accessResolution = BancoDocumentoAccessResolver.Resolve(
            localMetadata,
            document.Oid,
            document.DocumentoLabel,
            hasLegacyScontrinatoPaymentSignal,
            document.HasLegacyFiscalSignal || hasReliableBancoScontrinoSignal);
        var hasLegacyFiscalSignal = document.HasLegacyFiscalSignal || hasReliableBancoScontrinoSignal;
        var hasLegacyNonScontrinatoSignal = IsLegacyNonScontrinatoLabel(scontrinoLabel) || document.PagatoBuoni > 0;
        var isScontrinato = ResolveIsScontrinato(
            modalitaChiusura,
            categoria,
            statoFiscale,
            hasLegacyFiscalSignal,
            hasLegacyScontrinatoPaymentSignal,
            scontrinoLabel);
        var isCortesia = ResolveIsCortesia(categoria, hasLegacyNonScontrinatoSignal, isScontrinato);
        var isPubblicatoBanco = ResolveIsPubblicatoBanco(modalitaChiusura, categoria, statoFiscale, isScontrinato, isCortesia);

        return new DocumentGridRowViewModel
        {
            IsLocal = false,
            AccessMode = accessResolution.Mode,
            GestionaleOid = document.Oid,
            LocalDocumentId = localMetadata?.Id,
            Operatore = document.Operatore,
            NumeroDocumento = document.DocumentoLabel,
            Data = document.Data,
            Cliente = document.ClienteLabel,
            Totale = document.TotaleDocumento,
            CustomerPoints = document.CustomerPoints,
            PagContanti = document.PagatoContanti,
            PagCarta = document.PagatoCarta,
            PagWeb = document.PagatoWeb,
            PagBuoni = document.PagatoBuoni,
            PagSospeso = document.Pagatosospeso,
            TotalePagato = document.TotalePagatoUfficiale,
            ResiduoPagamento = document.ResiduoPagamento,
            DaFiscalizzare = isScontrinato ? 0 : document.TotaleDocumento,
            CategoriaDocumentoBanco = categoria,
            ModalitaChiusuraDocumento = modalitaChiusura,
            StatoFiscaleBanco = statoFiscale,
            HasSospesoComponent = hasSospeso,
            HasLegacyFiscalSignal = hasLegacyFiscalSignal,
            HasLegacyNonScontrinatoSignal = hasLegacyNonScontrinatoSignal,
            HasLegacyScontrinatoPaymentSignal = hasLegacyScontrinatoPaymentSignal,
            StatoDocumento = BuildStatoDocumentoLabel(accessResolution, isCortesia, isScontrinato, isPubblicatoBanco, statoFiscale, hasSospeso),
            ScontrinoLabel = scontrinoLabel,
            OpenDocumentActionLabel = accessResolution.AzioneLista,
            OpenDocumentActionTooltip = accessResolution.TooltipAzioneLista
        };
    }

    public static DocumentGridRowViewModel FromLocal(DocumentoLocale document)
    {
        var pagamenti = NormalizePagamenti(document.Pagamenti);
        var totalePagato = Math.Max(0, pagamenti.Contanti + pagamenti.Carta + pagamenti.Web + pagamenti.Buoni);
        var totaleDaIncassare = Math.Max(0, document.TotaleDocumento - document.ScontoDocumento);
        var hasSospeso = document.HasComponenteSospeso || pagamenti.Sospeso > 0;
        var hasReliableBancoScontrinoSignal = HasReliableBancoScontrinoSignal(document);
        var accessResolution = BancoDocumentoAccessResolver.Resolve(
            document,
            document.DocumentoGestionaleOid,
            FormatDocumentNumber(document),
            pagamenti.Contanti > 0 || pagamenti.Carta > 0 || pagamenti.Web > 0,
            hasReliableBancoScontrinoSignal);
        var hasLegacyFiscalSignal = hasReliableBancoScontrinoSignal;
        var hasLegacyScontrinatoPaymentSignal = pagamenti.Contanti > 0 || pagamenti.Carta > 0 || pagamenti.Web > 0;
        var scontrinoLabel = ResolveScontrinoLabel("No", hasReliableBancoScontrinoSignal);
        var isScontrinato = ResolveIsScontrinato(
            document.ModalitaChiusura,
            document.CategoriaDocumentoBanco,
            document.StatoFiscaleBanco,
            hasLegacyFiscalSignal,
            hasLegacyScontrinatoPaymentSignal,
            scontrinoLabel);
        var isCortesia = ResolveIsCortesia(document.CategoriaDocumentoBanco, false, isScontrinato);
        var isPubblicatoBanco = ResolveIsPubblicatoBanco(document.ModalitaChiusura, document.CategoriaDocumentoBanco, document.StatoFiscaleBanco, isScontrinato, isCortesia);

        return new DocumentGridRowViewModel
        {
            IsLocal = true,
            AccessMode = accessResolution.Mode,
            GestionaleOid = document.DocumentoGestionaleOid,
            LocalDocumentId = document.Id,
            Operatore = document.Operatore,
            NumeroDocumento = FormatDocumentNumber(document),
            Data = document.DataUltimaModifica.LocalDateTime,
            Cliente = document.Cliente,
            Totale = document.TotaleDocumento,
            CustomerPoints = null,
            PagContanti = pagamenti.Contanti,
            PagCarta = pagamenti.Carta,
            PagWeb = pagamenti.Web,
            PagBuoni = pagamenti.Buoni,
            PagSospeso = pagamenti.Sospeso,
            TotalePagato = totalePagato,
            ResiduoPagamento = Math.Max(0, totaleDaIncassare - totalePagato - pagamenti.Sospeso),
            DaFiscalizzare = isScontrinato ? 0 : totaleDaIncassare,
            CategoriaDocumentoBanco = document.CategoriaDocumentoBanco,
            ModalitaChiusuraDocumento = document.ModalitaChiusura,
            StatoFiscaleBanco = document.StatoFiscaleBanco,
            HasSospesoComponent = hasSospeso,
            HasLegacyFiscalSignal = hasLegacyFiscalSignal,
            HasLegacyNonScontrinatoSignal = false,
            HasLegacyScontrinatoPaymentSignal = hasLegacyScontrinatoPaymentSignal,
            StatoDocumento = BuildStatoDocumentoLabel(accessResolution, isCortesia, isScontrinato, isPubblicatoBanco, document.StatoFiscaleBanco, hasSospeso),
            ScontrinoLabel = scontrinoLabel,
            OpenDocumentActionLabel = accessResolution.AzioneLista,
            OpenDocumentActionTooltip = accessResolution.TooltipAzioneLista
        };
    }

    public static DocumentGridRowViewModel FromPublishedLocalFallback(DocumentoLocale document)
    {
        var pagamenti = NormalizePagamenti(document.Pagamenti);
        var totalePagato = Math.Max(0, pagamenti.Contanti + pagamenti.Carta + pagamenti.Web + pagamenti.Buoni);
        var totaleDaIncassare = Math.Max(0, document.TotaleDocumento - document.ScontoDocumento);
        var hasSospeso = document.HasComponenteSospeso || pagamenti.Sospeso > 0;
        var hasReliableBancoScontrinoSignal = HasReliableBancoScontrinoSignal(document);
        var accessResolution = BancoDocumentoAccessResolver.Resolve(
            document,
            document.DocumentoGestionaleOid,
            FormatDocumentNumber(document),
            pagamenti.Contanti > 0 || pagamenti.Carta > 0 || pagamenti.Web > 0,
            hasReliableBancoScontrinoSignal);
        var hasLegacyFiscalSignal = hasReliableBancoScontrinoSignal;
        var hasLegacyScontrinatoPaymentSignal = pagamenti.Contanti > 0 || pagamenti.Carta > 0 || pagamenti.Web > 0;
        var scontrinoLabel = ResolveScontrinoLabel("No", hasReliableBancoScontrinoSignal);
        var isScontrinato = ResolveIsScontrinato(
            document.ModalitaChiusura,
            document.CategoriaDocumentoBanco,
            document.StatoFiscaleBanco,
            hasLegacyFiscalSignal,
            hasLegacyScontrinatoPaymentSignal,
            scontrinoLabel);
        var isCortesia = ResolveIsCortesia(document.CategoriaDocumentoBanco, false, isScontrinato);
        var isPubblicatoBanco = ResolveIsPubblicatoBanco(document.ModalitaChiusura, document.CategoriaDocumentoBanco, document.StatoFiscaleBanco, isScontrinato, isCortesia);

        return new DocumentGridRowViewModel
        {
            IsLocal = false,
            AccessMode = accessResolution.Mode,
            GestionaleOid = document.DocumentoGestionaleOid,
            LocalDocumentId = document.Id,
            Operatore = document.Operatore,
            NumeroDocumento = FormatDocumentNumber(document),
            Data = document.DataDocumentoGestionale ?? document.DataUltimaModifica.LocalDateTime,
            Cliente = document.Cliente,
            Totale = document.TotaleDocumento,
            CustomerPoints = null,
            PagContanti = pagamenti.Contanti,
            PagCarta = pagamenti.Carta,
            PagWeb = pagamenti.Web,
            PagBuoni = pagamenti.Buoni,
            PagSospeso = pagamenti.Sospeso,
            TotalePagato = totalePagato,
            ResiduoPagamento = Math.Max(0, totaleDaIncassare - totalePagato - pagamenti.Sospeso),
            DaFiscalizzare = isScontrinato ? 0 : totaleDaIncassare,
            CategoriaDocumentoBanco = document.CategoriaDocumentoBanco,
            ModalitaChiusuraDocumento = document.ModalitaChiusura,
            StatoFiscaleBanco = document.StatoFiscaleBanco,
            HasSospesoComponent = hasSospeso,
            HasLegacyFiscalSignal = hasLegacyFiscalSignal,
            HasLegacyNonScontrinatoSignal = false,
            HasLegacyScontrinatoPaymentSignal = hasLegacyScontrinatoPaymentSignal,
            StatoDocumento = BuildStatoDocumentoLabel(accessResolution, isCortesia, isScontrinato, isPubblicatoBanco, document.StatoFiscaleBanco, hasSospeso),
            ScontrinoLabel = scontrinoLabel,
            OpenDocumentActionLabel = accessResolution.AzioneLista,
            OpenDocumentActionTooltip = accessResolution.TooltipAzioneLista
        };
    }

    private static string FormatDocumentNumber(DocumentoLocale document)
    {
        if (document.NumeroDocumentoGestionale.HasValue && document.AnnoDocumentoGestionale.HasValue)
        {
            return $"{document.NumeroDocumentoGestionale}/{document.AnnoDocumentoGestionale}";
        }

        return "Scheda Banco";
    }

    private static string BuildStatoDocumentoLabel(
        BancoDocumentoAccessResolution accessResolution,
        bool isCortesia,
        bool isScontrinato,
        bool isPubblicatoBanco,
        StatoFiscaleBanco statoFiscaleBanco,
        bool hasSospeso)
    {
        if (accessResolution.Mode == BancoDocumentoAccessMode.UfficialeConsultazione)
        {
            return hasSospeso
                ? "Consultazione bloccata - documento ufficiale con sospeso"
                : "Consultazione bloccata - documento ufficiale";
        }

        if (accessResolution.Mode == BancoDocumentoAccessMode.UfficialeRecuperabile)
        {
            return hasSospeso
                ? "Recuperabile nel Banco - documento ufficiale con sospeso"
                : "Recuperabile nel Banco - documento ufficiale";
        }

        if (isPubblicatoBanco)
        {
            return hasSospeso
                ? "Pubblicato Banco con sospeso - pubblicato legacy"
                : "Pubblicato Banco - pubblicato legacy";
        }

        var categoria = isScontrinato
            ? "Scontrino"
            : isCortesia
                ? "Cortesia"
                : "Legacy senza audit";

        var statoFiscale = statoFiscaleBanco switch
        {
            StatoFiscaleBanco.PubblicatoLegacyNonFiscalizzato => "pubblicato legacy",
            StatoFiscaleBanco.FiscalizzazioneWinEcrRichiesta => "WinEcr richiesto",
            StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata => "WinEcr completato",
            StatoFiscaleBanco.FiscalizzazioneWinEcrFallita => "WinEcr fallito",
            _ => "senza audit fiscale"
        };

        return hasSospeso
            ? $"{categoria} con sospeso - {statoFiscale}"
            : $"{categoria} - {statoFiscale}";
    }

    private static bool ResolveIsScontrinato(
        ModalitaChiusuraDocumento modalitaChiusuraDocumento,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        StatoFiscaleBanco statoFiscaleBanco,
        bool hasLegacyFiscalSignal,
        bool hasLegacyScontrinatoPaymentSignal,
        string? scontrinoLabel = null)
    {
        if (categoriaDocumentoBanco == CategoriaDocumentoBanco.Cortesia ||
            modalitaChiusuraDocumento == ModalitaChiusuraDocumento.Cortesia ||
            statoFiscaleBanco == StatoFiscaleBanco.PubblicatoLegacyNonFiscalizzato)
        {
            return false;
        }

        if (TryResolveLegacyScontrinoLabel(scontrinoLabel, out var legacyIsScontrinato))
        {
            return legacyIsScontrinato;
        }

        return categoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino ||
               modalitaChiusuraDocumento == ModalitaChiusuraDocumento.Scontrino ||
               statoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata ||
               statoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrRichiesta ||
               statoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrFallita ||
               hasLegacyFiscalSignal ||
               hasLegacyScontrinatoPaymentSignal;
    }

    private static bool ResolveIsCortesia(
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        bool hasLegacyNonScontrinatoSignal,
        bool isScontrinato)
    {
        return !isScontrinato &&
               (categoriaDocumentoBanco == CategoriaDocumentoBanco.Cortesia ||
                hasLegacyNonScontrinatoSignal);
    }

    private static bool ResolveIsNonScontrinato(
        BancoDocumentoAccessMode accessMode,
        bool isCortesia,
        bool isScontrinato)
    {
        return !isScontrinato &&
               (isCortesia || accessMode == BancoDocumentoAccessMode.UfficialeRecuperabile);
    }

    private static bool IsLegacyNonScontrinatoLabel(string? scontrinoLabel)
    {
        return TryResolveLegacyScontrinoLabel(scontrinoLabel, out var legacyIsScontrinato) &&
               !legacyIsScontrinato;
    }

    private static bool TryResolveLegacyScontrinoLabel(string? scontrinoLabel, out bool isScontrinato)
    {
        switch (scontrinoLabel?.Trim())
        {
            case "No":
                isScontrinato = false;
                return true;
            case "Si":
            case "Si!":
                isScontrinato = true;
                return true;
            default:
                isScontrinato = false;
                return false;
        }
    }

    private static bool HasLegacyScontrinatoPayments(decimal pagContanti, decimal pagCarta, decimal pagWeb)
    {
        return pagContanti > 0 || pagCarta > 0 || pagWeb > 0;
    }

    private static bool HasReliableBancoScontrinoSignal(DocumentoLocale? document)
    {
        if (document is null)
        {
            return false;
        }

        return document.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino ||
               document.ModalitaChiusura == ModalitaChiusuraDocumento.Scontrino ||
               document.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrRichiesta ||
               document.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata ||
               document.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrFallita;
    }

    private static string ResolveScontrinoLabel(string legacyLabel, bool hasReliableBancoScontrinoSignal)
    {
        if (string.Equals(legacyLabel, "Si!", StringComparison.Ordinal))
        {
            return legacyLabel;
        }

        if (hasReliableBancoScontrinoSignal)
        {
            return "Si";
        }

        return string.IsNullOrWhiteSpace(legacyLabel)
            ? "No"
            : legacyLabel;
    }

    private static bool ResolveIsPubblicatoBanco(
        ModalitaChiusuraDocumento modalitaChiusuraDocumento,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        StatoFiscaleBanco statoFiscaleBanco,
        bool isScontrinato,
        bool isCortesia)
    {
        return !isScontrinato &&
               !isCortesia &&
               modalitaChiusuraDocumento == ModalitaChiusuraDocumento.PubblicazioneUfficiale &&
               categoriaDocumentoBanco == CategoriaDocumentoBanco.Indeterminata &&
               statoFiscaleBanco == StatoFiscaleBanco.PubblicatoLegacyNonFiscalizzato;
    }

    public static (decimal Contanti, decimal Carta, decimal Web, decimal Buoni, decimal Sospeso) NormalizePagamenti(IEnumerable<PagamentoLocale> pagamenti)
    {
        decimal contanti = 0;
        decimal carta = 0;
        decimal web = 0;
        decimal buoni = 0;
        decimal sospeso = 0;

        foreach (var pagamento in pagamenti)
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

        return (contanti, carta, web, buoni, sospeso);
    }
}
