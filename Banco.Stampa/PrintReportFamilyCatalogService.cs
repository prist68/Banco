namespace Banco.Stampa;

public sealed class PrintReportFamilyCatalogService : IPrintReportFamilyCatalogService
{
    private static readonly IReadOnlyList<PrintReportFamilyDefinition> Families =
    [
        new()
        {
            FamilyKey = "pos-vendita",
            DisplayName = "POS / Vendita banco",
            DomainContext = "Famiglia Banco compatta per cortesia, ristampa e ultimo scontrino su modellodocumento 27.",
            RuntimeModelSummary = "Blocchi previsti: Negozio, Testata, Righe, Totali, Cliente, Punti, Footer.",
            SupportedDocumentKeys = ["receipt-80-db"],
            FieldGroups =
            [
                G("negozio", "Negozio", "Testata", "Dati anagrafici del punto vendita letti dal legacy config.",
                    F("Negozio.RagioneSociale", "Ragione sociale", "[Negozio.RagioneSociale]", "config.KEY_Ragionesociale", "string", PrintContractConfidence.Certain),
                    F("Negozio.IndirizzoCompleto", "Indirizzo completo", "[Negozio.IndirizzoCompleto]", "config.KEY_Indirizzo + KEY_Citta + lookup citta", "string", PrintContractConfidence.Certain),
                    F("Negozio.ContattiCompleti", "Contatti completi", "[Negozio.ContattiCompleti]", "config.KEY_Telefono + KEY_Email", "string", PrintContractConfidence.Certain),
                    F("Negozio.PartitaIvaVisuale", "Partita IVA", "[Negozio.PartitaIvaVisuale]", "config.KEY_Partitaiva", "string", PrintContractConfidence.Certain)),
                G("testata", "Testata documento", "Testata", "Campi principali del documento Banco corrente.",
                    F("Testata.PagamentoLabel", "Pagamento label", "[Testata.PagamentoLabel]", "documento + regole Banco", "string", PrintContractConfidence.Certain),
                    F("Testata.DocumentoLabel", "Etichetta documento", "[Testata.DocumentoLabel]", "documento / modello documento", "string", PrintContractConfidence.Certain),
                    F("Testata.AnnoVisuale", "Anno", "[Testata.AnnoVisuale]", "documento.Anno", "string", PrintContractConfidence.Certain),
                    F("Testata.DataTesto", "Data", "[Testata.DataTesto]", "documento.Data", "string", PrintContractConfidence.Certain),
                    F("Testata.NumeroVisuale", "Numero documento", "[Testata.NumeroVisuale]", "documento.Numero", "string", PrintContractConfidence.Certain),
                    F("Testata.ProgressivoVenditaLabel", "Progressivo vendita", "[Testata.ProgressivoVenditaLabel]", "documento.Numero + documento.Anno", "string", PrintContractConfidence.Certain)),
                G("righe", "Righe vendita", "Corpo", "Campi per la banda dettagli articolo.",
                    F("Righe.RigaOid", "Oid riga", "[Righe.RigaOid]", "documentoriga.OID", "int", PrintContractConfidence.Certain),
                    F("Righe.CodiceArticolo", "Codice articolo", "[Righe.CodiceArticolo]", "documentoriga.Codicearticolo", "string", PrintContractConfidence.Certain),
                    F("Righe.Barcode", "Barcode", "[Righe.Barcode]", "articolo/barcode da dettaglio riga", "string", PrintContractConfidence.Certain),
                    F("Righe.QuantitaVisuale", "Quantita` visuale", "[Righe.QuantitaVisuale]", "documentoriga.Quantita", "string", PrintContractConfidence.Certain),
                    F("Righe.Quantita", "Quantita` numerica", "[Righe.Quantita]", "documentoriga.Quantita", "decimal", PrintContractConfidence.Certain),
                    F("Righe.Descrizione", "Descrizione", "[Righe.Descrizione]", "documentoriga.Descrizione", "string", PrintContractConfidence.Certain),
                    F("Righe.OrdineRiga", "Ordine riga", "[Righe.OrdineRiga]", "documentoriga.Ordineriga", "int", PrintContractConfidence.Certain),
                    F("Righe.UnitaMisura", "Unita` di misura", "[Righe.UnitaMisura]", "documentoriga.Unitadimisura", "string", PrintContractConfidence.Certain),
                    F("Righe.PrezzoUnitarioVisuale", "Prezzo unitario visuale", "[Righe.PrezzoUnitarioVisuale]", "documentoriga.Valoreunitario", "string", PrintContractConfidence.Certain),
                    F("Righe.PrezzoUnitario", "Prezzo unitario numerico", "[Righe.PrezzoUnitario]", "documentoriga.Valoreunitario", "decimal", PrintContractConfidence.Certain),
                    F("Righe.ScontoVisuale", "Sconto visuale", "[Righe.ScontoVisuale]", "documentoriga.Sconto1", "string", PrintContractConfidence.Certain),
                    F("Righe.ScontoPercentuale", "Sconto percentuale numerico", "[Righe.ScontoPercentuale]", "documentoriga.Sconto1", "decimal", PrintContractConfidence.Certain),
                    F("Righe.Sconto2Visuale", "Secondo sconto visuale", "[Righe.Sconto2Visuale]", "documentoriga.Sconto2", "string", PrintContractConfidence.Certain),
                    F("Righe.Sconto2", "Secondo sconto numerico", "[Righe.Sconto2]", "documentoriga.Sconto2", "decimal", PrintContractConfidence.Certain),
                    F("Righe.ImportoRigaVisuale", "Totale riga", "[Righe.ImportoRigaVisuale]", "documentoriga.Importoriga", "string", PrintContractConfidence.Certain),
                    F("Righe.ImportoRiga", "Totale riga numerico", "[Righe.ImportoRiga]", "documentoriga.Importoriga", "decimal", PrintContractConfidence.Certain),
                    F("Righe.AliquotaIva", "Aliquota IVA", "[Righe.AliquotaIva]", "iva/documentoriga da consolidare", "decimal", PrintContractConfidence.StrongInference)),
                G("totali", "Totali e chiusura", "Sommario", "Valori riepilogativi del documento.",
                    F("Totali.TotaleDocumentoVisuale", "Totale documento", "[Totali.TotaleDocumentoVisuale]", "documento.Totaledocumento", "string", PrintContractConfidence.Certain),
                    F("Totali.TotalePagatoVisuale", "Totale pagato", "[Totali.TotalePagatoVisuale]", "documento.Pagato", "string", PrintContractConfidence.Certain),
                    F("Totali.ContantiVisuale", "Contanti", "[Totali.ContantiVisuale]", "documento.Pagato", "string", PrintContractConfidence.Certain),
                    F("Totali.PagatoCartaVisuale", "Carta di credito", "[Totali.PagatoCartaVisuale]", "documento.Pagatocartacredito", "string", PrintContractConfidence.Certain),
                    F("Totali.PagamentoPrincipaleLabel", "Pagamento principale label", "[Totali.PagamentoPrincipaleLabel]", "documento / regole Banco", "string", PrintContractConfidence.Certain),
                    F("Totali.PagamentoPrincipaleImportoVisuale", "Pagamento principale importo", "[Totali.PagamentoPrincipaleImportoVisuale]", "documento campi pagato*", "string", PrintContractConfidence.Certain),
                    F("Totali.RestoVisuale", "Resto", "[Totali.RestoVisuale]", "regole Banco / pagamento", "string", PrintContractConfidence.StrongInference)),
                G("pagamenti", "Pagamenti", "Sommario", "Righe pagamenti disponibili come collezione separata nel designer POS.",
                    F("Pagamenti.Tipo", "Tipo pagamento", "[Pagamenti.Tipo]", "documento campi pagato* / regole Banco", "string", PrintContractConfidence.Certain),
                    F("Pagamenti.Importo", "Importo numerico", "[Pagamenti.Importo]", "documento campi pagato*", "decimal", PrintContractConfidence.Certain),
                    F("Pagamenti.ImportoVisuale", "Importo visuale", "[Pagamenti.ImportoVisuale]", "documento campi pagato*", "string", PrintContractConfidence.Certain)),
                G("cliente", "Cliente", "Sommario", "Dati sintetici cliente usati nel footer POS.",
                    F("Cliente.Nominativo", "Nominativo cliente", "[Cliente.Nominativo]", "soggetto.Ragionesociale1 / nominativo", "string", PrintContractConfidence.Certain),
                    F("Cliente.CodiceCartaFedelta", "Codice carta fedelta`", "[Cliente.CodiceCartaFedelta]", "soggetto.Codicecartafedelta", "string", PrintContractConfidence.Certain),
                    F("Cliente.PuntiPrecedentiLabel", "Punti precedenti label", "[Cliente.PuntiPrecedentiLabel]", "soggetto.Punticartafedeltainiziali", "string", PrintContractConfidence.Certain),
                    F("Cliente.PuntiAttualiLabel", "Punti attuali label", "[Cliente.PuntiAttualiLabel]", "soggetto.Punticartafedelta", "string", PrintContractConfidence.Certain)),
                G("punti", "Punti fedelta`", "Sommario", "Campi punti da consolidare per l'uso nel layout POS.",
                    F("Punti.PrecedentiLabel", "Punti precedenti", "[Punti.PrecedentiLabel]", "soggetto.Punticartafedeltainiziali", "string", PrintContractConfidence.Certain),
                    F("Punti.AttualiLabel", "Punti attuali", "[Punti.AttualiLabel]", "soggetto.Punticartafedelta", "string", PrintContractConfidence.Certain),
                    F("Punti.MaturatiVendita", "Punti maturati alla vendita", "[Punti.MaturatiVendita]", "Regole FM/Banco punti da verificare", "decimal", PrintContractConfidence.ToVerify, "Campo richiesto per stampe vendita ma non ancora mappato con certezza."),
                    F("Punti.UtilizzatiVendita", "Punti utilizzati in vendita", "[Punti.UtilizzatiVendita]", "Documento riga premio / regole punti da verificare", "decimal", PrintContractConfidence.StrongInference)),
                G("footer", "Footer e note", "Sommario", "Testi finali e riferimenti negozio.",
                    F("Footer.Website", "Sito / riferimento scontrino", "[Footer.Website]", "config.KEY_Riferimento_Su_Scontrino_Predefinito", "string", PrintContractConfidence.Certain),
                    F("Footer.GestionaleLabel", "Etichetta gestionale", "[Footer.GestionaleLabel]", "Regola runtime Banco", "string", PrintContractConfidence.Certain),
                    F("Footer.NoteFinali", "Note finali", "[Footer.NoteFinali]", "Template Banco.Stampa", "string", PrintContractConfidence.StrongInference))
            ]
        },
        new()
        {
            FamilyKey = "elenco-clienti",
            DisplayName = "Elenco clienti",
            DomainContext = "Stampa tabellare anagrafiche cliente, con eventuale uso commerciale o amministrativo.",
            RuntimeModelSummary = "Blocchi previsti: Testata report, Cliente, Indirizzo, Contatti, Punti.",
            SupportedDocumentKeys = ["customers-list"],
            FieldGroups =
            [
                G("testata-report", "Testata report", "Testata", "Informazioni generali della stampa elenco clienti.",
                    F("Report.Titolo", "Titolo report", "[ReportTitle]", "Parametro runtime report", "string", PrintContractConfidence.Certain),
                    F("Report.DataStampa", "Data stampa", "[ReportDate]", "Parametro runtime report", "date", PrintContractConfidence.Certain),
                    F("Report.FiltroAttivo", "Filtro attivo", "[ReportFilterSummary]", "Parametro runtime report", "string", PrintContractConfidence.Certain)),
                G("cliente", "Cliente", "Corpo", "Campi riga anagrafica cliente.",
                    F("Cliente.Oid", "Oid cliente", "[Clienti.Oid]", "soggetto.OID", "int", PrintContractConfidence.Certain),
                    F("Cliente.Nominativo", "Nominativo", "[Clienti.Nominativo]", "soggetto.Ragionesociale1 / nominativo", "string", PrintContractConfidence.Certain),
                    F("Cliente.Codice", "Codice cliente", "[Clienti.Codice]", "soggetto.Codice da verificare", "string", PrintContractConfidence.ToVerify)),
                G("indirizzo", "Indirizzo", "Corpo", "Campi localizzazione cliente.",
                    F("Cliente.Indirizzo", "Indirizzo", "[Clienti.Indirizzo]", "soggetto.Indirizzo", "string", PrintContractConfidence.Certain),
                    F("Cliente.Cap", "CAP", "[Clienti.Cap]", "soggetto.Cap", "string", PrintContractConfidence.Certain),
                    F("Cliente.Citta", "Citta`", "[Clienti.Citta]", "soggetto.Citta / lookup citta", "string", PrintContractConfidence.Certain),
                    F("Cliente.Provincia", "Provincia", "[Clienti.Provincia]", "lookup citta.Provincia", "string", PrintContractConfidence.Certain)),
                G("contatti", "Contatti e fiscale", "Corpo", "Contatti commerciali e dati fiscali cliente.",
                    F("Cliente.Telefono", "Telefono", "[Clienti.Telefono]", "soggetto.Telefono", "string", PrintContractConfidence.Certain),
                    F("Cliente.Email", "Email", "[Clienti.Email]", "soggetto.Email", "string", PrintContractConfidence.Certain),
                    F("Cliente.PartitaIva", "Partita IVA", "[Clienti.PartitaIva]", "soggetto.Partitaiva", "string", PrintContractConfidence.Certain),
                    F("Cliente.CodiceFiscale", "Codice fiscale", "[Clienti.CodiceFiscale]", "soggetto.Codicefiscale", "string", PrintContractConfidence.Certain)),
                G("punti", "Punti", "Corpo", "Stato fedelta` cliente in elenco.",
                    F("Cliente.PuntiAttuali", "Punti attuali", "[Clienti.PuntiAttuali]", "soggetto.Punticartafedelta", "decimal", PrintContractConfidence.Certain),
                    F("Cliente.PuntiPrecedenti", "Punti precedenti", "[Clienti.PuntiPrecedenti]", "soggetto.Punticartafedeltainiziali", "decimal", PrintContractConfidence.Certain),
                    F("Cliente.PuntiMaturatiPeriodo", "Punti maturati nel periodo", "[Clienti.PuntiMaturatiPeriodo]", "Storico punti / regole Banco da verificare", "decimal", PrintContractConfidence.ToVerify))
            ]
        },
        new()
        {
            FamilyKey = "lista-articoli",
            DisplayName = "Lista articoli",
            DomainContext = "Stampa gestionale orientata a elenco articoli operativo, non marketing.",
            RuntimeModelSummary = "Blocchi previsti: Testata report, Articolo, Prezzi, Giacenze, Barcode/Fornitore.",
            SupportedDocumentKeys = ["articles-list", "articles-catalog"],
            FieldGroups =
            [
                G("testata-report", "Testata report", "Testata", "Informazioni generali del report articoli.",
                    F("Report.Titolo", "Titolo report", "[ReportTitle]", "Parametro runtime report", "string", PrintContractConfidence.Certain),
                    F("Report.DataStampa", "Data stampa", "[ReportDate]", "Parametro runtime report", "date", PrintContractConfidence.Certain),
                    F("Report.FiltroCategoria", "Filtro categoria", "[ReportCategoryFilter]", "Parametro runtime report", "string", PrintContractConfidence.Certain)),
                G("articolo", "Articolo", "Corpo", "Campi base articolo.",
                    F("Articolo.Oid", "Oid articolo", "[Articoli.Oid]", "articolo.OID", "int", PrintContractConfidence.Certain),
                    F("Articolo.Codice", "Codice articolo", "[Articoli.Codice]", "articolo.Codice", "string", PrintContractConfidence.Certain),
                    F("Articolo.Descrizione", "Descrizione", "[Articoli.Descrizione]", "articolo.Descrizione", "string", PrintContractConfidence.Certain),
                    F("Articolo.UnitaMisura", "Unita` di misura", "[Articoli.UnitaMisura]", "unitadimisura/articolo", "string", PrintContractConfidence.StrongInference)),
                G("prezzi", "Prezzi", "Corpo", "Prezzi base e commerciali.",
                    F("Articolo.PrezzoVendita", "Prezzo vendita", "[Articoli.PrezzoVendita]", "articolo / listino", "decimal", PrintContractConfidence.ToVerify),
                    F("Articolo.PrezzoAcquisto", "Prezzo acquisto", "[Articoli.PrezzoAcquisto]", "articolo / ultimo costo", "decimal", PrintContractConfidence.ToVerify),
                    F("Articolo.AliquotaIva", "Aliquota IVA", "[Articoli.AliquotaIva]", "iva/articolo", "decimal", PrintContractConfidence.StrongInference)),
                G("giacenze", "Giacenze", "Corpo", "Disponibilita` magazzino e quantita` operative.",
                    F("Articolo.Giacenza", "Giacenza", "[Articoli.Giacenza]", "movimenti / giacenze FM", "decimal", PrintContractConfidence.ToVerify),
                    F("Articolo.Impegnato", "Impegnato", "[Articoli.Impegnato]", "ordini / disponibilita` FM", "decimal", PrintContractConfidence.ToVerify)),
                G("barcode", "Barcode e fornitore", "Corpo", "Codici a barre e relazione fornitore principale.",
                    F("Articolo.Barcode", "Barcode", "[Articoli.Barcode]", "articolo / varianti", "string", PrintContractConfidence.StrongInference),
                    F("Articolo.FornitorePrincipale", "Fornitore principale", "[Articoli.FornitorePrincipale]", "articolofornitore / fornitore", "string", PrintContractConfidence.ToVerify))
            ]
        }
    ];

    public Task<IReadOnlyList<PrintReportFamilyDefinition>> GetFamiliesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Families);
    }

    public Task<PrintReportFamilyDefinition?> GetFamilyAsync(string familyKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Families.FirstOrDefault(item =>
            string.Equals(item.FamilyKey, familyKey, StringComparison.OrdinalIgnoreCase)));
    }

    private static PrintReportFieldGroupDefinition G(string key, string name, string area, string description, params PrintReportAvailableFieldDefinition[] fields) =>
        new()
        {
            GroupKey = key,
            DisplayName = name,
            ReportArea = area,
            Description = description,
            Fields = fields
        };

    private static PrintReportAvailableFieldDefinition F(string technical, string name, string binding, string source, string dataType, PrintContractConfidence confidence, string? notes = null) =>
        new()
        {
            TechnicalName = technical,
            DisplayName = name,
            BindingPath = binding,
            SourceContext = source,
            DataType = dataType,
            Confidence = confidence,
            Notes = notes
        };
}
