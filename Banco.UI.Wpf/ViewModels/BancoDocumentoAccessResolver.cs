using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;

namespace Banco.UI.Wpf.ViewModels;

public enum BancoDocumentoAccessMode
{
    LocaleNonPubblicato = 0,
    UfficialeRecuperabile = 1,
    UfficialeConsultazione = 2
}

public sealed record BancoDocumentoAccessResolution(
    BancoDocumentoAccessMode Mode,
    string StatoScheda,
    string MessaggioScheda,
    string BannerScheda,
    string AzioneLista,
    string TooltipAzioneLista)
{
    public bool IsReadOnly => Mode == BancoDocumentoAccessMode.UfficialeConsultazione;

    public bool IsOfficialDocument => Mode != BancoDocumentoAccessMode.LocaleNonPubblicato;

    public bool IsRecoverableOfficialDocument => Mode == BancoDocumentoAccessMode.UfficialeRecuperabile;

    public bool ShowBanner => IsOfficialDocument;
}

public static class BancoDocumentoAccessResolver
{
    public static BancoDocumentoAccessResolution Resolve(
        DocumentoLocale? localMetadata,
        int? legacyDocumentOid,
        string? legacyDocumentLabel,
        bool legacyHasScontrinatoPayments,
        bool legacyHasReliableFiscalClosureSignal = false)
    {
        var normalizedLegacyLabel = string.IsNullOrWhiteSpace(legacyDocumentLabel)
            ? "documento Banco"
            : legacyDocumentLabel.Trim();
        var localIsCoherent = IsLocalMetadataCoherent(localMetadata, legacyDocumentOid);

        if (localMetadata is not null && !localMetadata.DocumentoGestionaleOid.HasValue)
        {
            return BuildLocaleNonPubblicato();
        }

        if (localIsCoherent)
        {
            return ResolveFromLocalMetadata(localMetadata!, normalizedLegacyLabel, legacyHasReliableFiscalClosureSignal);
        }

        if (legacyDocumentOid.HasValue)
        {
            return ResolveFromLegacySignals(
                normalizedLegacyLabel,
                legacyHasScontrinatoPayments,
                legacyHasReliableFiscalClosureSignal);
        }

        return BuildLocaleNonPubblicato();
    }

    private static BancoDocumentoAccessResolution ResolveFromLocalMetadata(
        DocumentoLocale localMetadata,
        string legacyDocumentLabel,
        bool legacyHasReliableFiscalClosureSignal)
    {
        // Il blocco pieno vale solo quando esiste un segnale fiscale affidabile.
        if (localMetadata.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata &&
            (!localMetadata.DocumentoGestionaleOid.HasValue || legacyHasReliableFiscalClosureSignal))
        {
            return BuildUfficialeConsultazione(
                "Documento fiscalizzato - consultazione",
                "Documento Banco fiscalizzato aperto in consultazione bloccata.",
                "Documento fiscalizzato/scontrinato: consultazione bloccata. Per correggere serve il flusso fiscale previsto.",
                "Apri in consultazione",
                "Apre il documento Banco in consultazione bloccata.");
        }

        if (localMetadata.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Scontrino)
        {
            return BuildUfficialeRecuperabile(
                "Documento scontrinato recuperabile",
                $"Documento Banco {legacyDocumentLabel} aperto in scheda operativa. I pagamenti restano scontrinati per lista e riepiloghi.",
                "Documento ufficiale con incasso scontrinato: apertura operativa consentita in scheda Banco. La cancellazione resta prudenziale e si gestisce dalla scheda.");
        }

        if (localMetadata.DocumentoGestionaleOid.HasValue)
        {
            var stato = localMetadata.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Cortesia
                ? "Documento cortesia recuperabile"
                : "Documento Banco recuperabile";
            var messaggio = localMetadata.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Cortesia
                ? $"Documento cortesia {legacyDocumentLabel} recuperato sullo stesso OID legacy."
                : $"Documento Banco {legacyDocumentLabel} recuperato sullo stesso OID legacy.";
            var banner = localMetadata.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Cortesia
                ? "Documento ufficiale non fiscalizzato recuperabile: le variazioni aggiornano la stessa cortesia legacy."
                : "Documento ufficiale non fiscalizzato recuperabile: le variazioni aggiornano lo stesso documento legacy.";

            return BuildUfficialeRecuperabile(stato, messaggio, banner);
        }

        return ResolveFromLegacySignals(legacyDocumentLabel, false);
    }

    private static BancoDocumentoAccessResolution ResolveFromLegacySignals(
        string legacyDocumentLabel,
        bool legacyHasScontrinatoPayments,
        bool legacyHasReliableFiscalClosureSignal = false)
    {
        if (legacyHasReliableFiscalClosureSignal)
        {
            return BuildUfficialeConsultazione(
                "Documento fiscalizzato - consultazione",
                $"Documento Banco {legacyDocumentLabel} aperto in consultazione bloccata per segnale fiscale legacy.",
                "Documento fiscalizzato/scontrinato: consultazione bloccata. Per correggere serve il flusso fiscale previsto.",
                "Apri in consultazione",
                "Apre il documento Banco in consultazione bloccata.");
        }

        if (legacyHasScontrinatoPayments)
        {
            return BuildUfficialeRecuperabile(
                "Documento legacy scontrinato recuperabile",
                $"Documento Banco legacy {legacyDocumentLabel} aperto in scheda operativa con incasso scontrinato rilevato dal DB.",
                "Documento ufficiale con pagamenti scontrinati: scheda Banco recuperabile. In lista il `Del` resta nascosto per prudenza.");
        }

        return BuildUfficialeRecuperabile(
            "Documento legacy non scontrinato recuperabile",
            $"Documento Banco legacy {legacyDocumentLabel} non scontrinato: recupero operativo consentito sullo stesso OID legacy.",
            "Documento ufficiale Banco non scontrinato: recupero e cancellazione consentiti fino alla fiscalizzazione.");
    }

    private static bool IsLocalMetadataCoherent(DocumentoLocale? localMetadata, int? legacyDocumentOid)
    {
        if (localMetadata is null)
        {
            return false;
        }

        if (!localMetadata.DocumentoGestionaleOid.HasValue)
        {
            return true;
        }

        return !legacyDocumentOid.HasValue || localMetadata.DocumentoGestionaleOid.Value == legacyDocumentOid.Value;
    }

    private static BancoDocumentoAccessResolution BuildLocaleNonPubblicato()
    {
        return new BancoDocumentoAccessResolution(
            BancoDocumentoAccessMode.LocaleNonPubblicato,
            "Documento Banco operativo",
            "Documento Banco pronto per la gestione operativa.",
            string.Empty,
            "Apri nel Banco",
            "Apre il documento Banco operativo.");
    }

    private static BancoDocumentoAccessResolution BuildUfficialeRecuperabile(
        string stato,
        string messaggio,
        string banner)
    {
        return new BancoDocumentoAccessResolution(
            BancoDocumentoAccessMode.UfficialeRecuperabile,
            stato,
            messaggio,
            banner,
            "Recupera nel Banco",
            "Recupera il documento ufficiale non fiscalizzato in una scheda Banco operativa.");
    }

    private static BancoDocumentoAccessResolution BuildUfficialeConsultazione(
        string stato,
        string messaggio,
        string banner,
        string azioneLista,
        string tooltipAzioneLista)
    {
        return new BancoDocumentoAccessResolution(
            BancoDocumentoAccessMode.UfficialeConsultazione,
            stato,
            messaggio,
            banner,
            azioneLista,
            tooltipAzioneLista);
    }
}
