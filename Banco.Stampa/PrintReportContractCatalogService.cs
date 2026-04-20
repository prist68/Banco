namespace Banco.Stampa;

public sealed class PrintReportContractCatalogService : IPrintReportContractCatalogService
{
    private static readonly IReadOnlyList<PrintReportContractDefinition> Contracts =
    [
        new PrintReportContractDefinition
        {
            DocumentKey = "receipt-80-db",
            DisplayName = "POS / Cortesia 80 mm",
            Family = "POS / Cortesia / Ultimo scontrino",
            DomainContext = "Famiglia POS compatta. Blueprint legacy principale: Pos.repx. Contratto FM vicino a Reportpersonalizzato + contesto vendita, non a fattura Archidoc classica.",
            RuntimeParametersSummary = "Parametri runtime forti: Etichettadocumento, Ragionesociale, Indirizzo, Cap, Citta, Provincia, Telefono, Email, Partitaiva, Codicefiscale, Bancanome, Bancaiban, EnumTiporeport/preview, eventuale report personalizzato e contesto tabellare.",
            FieldMappings =
            [
                new PrintContractFieldMapping
                {
                    Zone = "Testata",
                    TargetField = "Testata.EtichettaDocumento",
                    SourceContext = "Parametro runtime / Pos.repx",
                    SourceField = "Etichettadocumento",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Parametro presente nel repx e usato in intestazione."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Testata",
                    TargetField = "Cliente.IndirizzoCompleto",
                    SourceContext = "Parametri runtime composti",
                    SourceField = "Indirizzo + Cap + Citta + Provincia",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel repx e` calcolato da Parametro_Indirizzo_Cap_Citta."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Testata",
                    TargetField = "Cliente.ContattiCompleti",
                    SourceContext = "Parametri runtime composti",
                    SourceField = "Telefono + Email",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel repx e` calcolato da Parametro_Telefono_Email."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Testata",
                    TargetField = "Cliente.FiscaleCompleto",
                    SourceContext = "Parametri runtime composti",
                    SourceField = "Partitaiva + Codicefiscale",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel repx e` calcolato da Parametro_PartitaIva_CodiceFiscale."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Righe",
                    TargetField = "Righe[].Quantita",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Documentoriga.Quantita",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel legacy viene nascosta quando vale zero."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Righe",
                    TargetField = "Righe[].Descrizione",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Documentoriga.Descrizione",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Campo descrittivo principale della banda righe."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Righe",
                    TargetField = "Righe[].PrezzoUnitario",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Documentoriga.Valoreunitario",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel legacy viene mostrato solo quando la quantita` e` significativa."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Righe",
                    TargetField = "Righe[].ScontoPercentuale",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Documentoriga.Sconto1",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel legacy viene nascosto quando vale 0,00."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Righe",
                    TargetField = "Righe[].Sconto2",
                    SourceContext = "Datasource Banco / recordset FM",
                    SourceField = "Documentoriga.Sconto2",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Secondo sconto disponibile nel dettaglio Banco; utile quando il layout vuole distinguere lo sconto base da quello aggiuntivo."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Righe",
                    TargetField = "Righe[].ImportoRiga",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Documentoriga.Importoriga",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel legacy viene nascosto quando la quantita` e` a zero."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Piede",
                    TargetField = "Totali.TotalePagato",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Pagato",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Presente nel footer POS del report legacy."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Piede",
                    TargetField = "Totali.PagatoCarta",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Pagatocartacredito",
                    Confidence = PrintContractConfidence.StrongInference,
                    Notes = "Campo trovato nel repx; da verificare nel contratto finale Banco se va esposto separatamente."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Piede",
                    TargetField = "Cliente.CodiceCartaFedelta",
                    SourceContext = "Datasource repx / recordset FM",
                    SourceField = "Soggetto.Codicecartafedelta",
                    Confidence = PrintContractConfidence.Certain,
                    Notes = "Nel layout legacy alimenta il barcode della fidelity card nel footer POS."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Runtime",
                    TargetField = "Stampante / output",
                    SourceContext = "Contesto esecuzione FM",
                    SourceField = "EnumTiporeport + stampante POS + eventuale report personalizzato",
                    Confidence = PrintContractConfidence.StrongInference,
                    Notes = "La nota tecnica FM mostra che il layout dipende anche dal contratto runtime, non solo dai campi."
                }
            ]
        },
        new PrintReportContractDefinition
        {
            DocumentKey = "customers-list",
            DisplayName = "Elenco clienti",
            Family = "Elenco clienti",
            DomainContext = "Famiglia tabellare anagrafiche cliente. Pensata per liste filtrate, stampe commerciali e controlli amministrativi.",
            RuntimeParametersSummary = "Parametri runtime forti: titolo report, data stampa, filtro attivo, eventuale ordinamento, note di testata.",
            FieldMappings =
            [
                new PrintContractFieldMapping
                {
                    Zone = "Testata",
                    TargetField = "Report.Title",
                    SourceContext = "Runtime report",
                    SourceField = "Titolo report",
                    Confidence = PrintContractConfidence.Certain
                },
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Clienti[].Nominativo",
                    SourceContext = "soggetto",
                    SourceField = "Ragionesociale1 / nominativo",
                    Confidence = PrintContractConfidence.Certain
                },
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Clienti[].Indirizzo / Cap / Citta / Provincia",
                    SourceContext = "soggetto + lookup citta",
                    SourceField = "Indirizzo + Cap + citta/provincia",
                    Confidence = PrintContractConfidence.Certain
                },
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Clienti[].PuntiAttuali",
                    SourceContext = "soggetto",
                    SourceField = "Punticartafedelta",
                    Confidence = PrintContractConfidence.Certain
                }
            ]
        },
        new PrintReportContractDefinition
        {
            DocumentKey = "articles-list",
            DisplayName = "Lista articoli",
            Family = "Lista articoli",
            DomainContext = "Famiglia tabellare articoli operativa, distinta dal catalogo marketing.",
            RuntimeParametersSummary = "Parametri runtime forti: titolo report, data stampa, filtro categoria/listino, eventuale ordinamento e note testata.",
            FieldMappings =
            [
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Articoli[].Codice",
                    SourceContext = "articolo",
                    SourceField = "Codice",
                    Confidence = PrintContractConfidence.Certain
                },
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Articoli[].Descrizione",
                    SourceContext = "articolo",
                    SourceField = "Descrizione",
                    Confidence = PrintContractConfidence.Certain
                },
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Articoli[].PrezzoVendita",
                    SourceContext = "articolo / listino",
                    SourceField = "Prezzo vendita",
                    Confidence = PrintContractConfidence.ToVerify,
                    Notes = "Sorgente FM da chiudere con verifica reale del listino usato."
                },
                new PrintContractFieldMapping
                {
                    Zone = "Corpo",
                    TargetField = "Articoli[].Giacenza",
                    SourceContext = "Giacenze FM",
                    SourceField = "Quantita` disponibile",
                    Confidence = PrintContractConfidence.ToVerify,
                    Notes = "Sorgente reale da allineare al dump FM."
                }
            ]
        }
    ];

    public Task<IReadOnlyList<PrintReportContractDefinition>> GetContractsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Contracts);
    }

    public Task<PrintReportContractDefinition?> GetContractAsync(string documentKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contract = Contracts.FirstOrDefault(item =>
            string.Equals(item.DocumentKey, documentKey, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(contract);
    }
}
