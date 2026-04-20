namespace Banco.Stampa;

public sealed class FastReportDocumentSchemaService : IFastReportDocumentSchemaService
{
    private static readonly IReadOnlyList<FastReportDocumentSchema> Schemas =
    [
        new FastReportDocumentSchema
        {
            DocumentKey = "receipt-80-db",
            DisplayName = "Cortesia 80 mm",
            RootObjectName = "DocumentoBanco",
            DataSources =
            [
                new FastReportDataSourceDefinition
                {
                    Key = "testata",
                    DisplayName = "Testata documento",
                    IsCollection = false,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "documento_oid", DisplayName = "Oid documento", DataPath = "Testata.DocumentoOid", SourceTable = "documento", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "numero", DisplayName = "Numero documento", DataPath = "Testata.Numero", SourceTable = "documento", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "anno", DisplayName = "Anno documento", DataPath = "Testata.Anno", SourceTable = "documento", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "data", DisplayName = "Data documento", DataPath = "Testata.Data", SourceTable = "documento", DataType = "date" },
                        new FastReportDataFieldDefinition { Key = "etichetta_documento", DisplayName = "Etichetta documento", DataPath = "Testata.EtichettaDocumento", SourceTable = "modellodocumento/documento", DataType = "string", Notes = "Serve a replicare il parametro Etichettadocumento di Pos.repx." },
                        new FastReportDataFieldDefinition { Key = "modello", DisplayName = "Modello documento", DataPath = "Testata.ModelloDocumento", SourceTable = "modellodocumento", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "operatore", DisplayName = "Operatore", DataPath = "Testata.Operatore", SourceTable = "operatore/documento", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "stato_runtime", DisplayName = "Stato runtime Banco", DataPath = "Testata.StatoRuntime", SourceTable = "documento + regole Banco", DataType = "string", Notes = "Campo derivato dal comportamento reale, non da persistenza parallela." }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "cliente",
                    DisplayName = "Cliente",
                    IsCollection = false,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "cliente_oid", DisplayName = "Oid cliente", DataPath = "Cliente.ClienteOid", SourceTable = "soggetto", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "nominativo", DisplayName = "Nominativo", DataPath = "Cliente.Nominativo", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "indirizzo", DisplayName = "Indirizzo", DataPath = "Cliente.Indirizzo", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "cap", DisplayName = "CAP", DataPath = "Cliente.Cap", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "citta", DisplayName = "Citta`", DataPath = "Cliente.Citta", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "provincia", DisplayName = "Provincia", DataPath = "Cliente.Provincia", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "partita_iva", DisplayName = "Partita IVA", DataPath = "Cliente.PartitaIva", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "codice_fiscale", DisplayName = "Codice fiscale", DataPath = "Cliente.CodiceFiscale", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "telefono", DisplayName = "Telefono", DataPath = "Cliente.Telefono", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "email", DisplayName = "Email", DataPath = "Cliente.Email", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "codice_carta_fedelta", DisplayName = "Codice carta fedelta`", DataPath = "Cliente.CodiceCartaFedelta", SourceTable = "soggetto", DataType = "string" }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "righe",
                    DisplayName = "Righe documento",
                    IsCollection = true,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "riga_oid", DisplayName = "Oid riga", DataPath = "Righe[].RigaOid", SourceTable = "documentoriga", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "codice_articolo", DisplayName = "Codice articolo", DataPath = "Righe[].CodiceArticolo", SourceTable = "documentoriga/articolo", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "barcode", DisplayName = "Barcode", DataPath = "Righe[].Barcode", SourceTable = "documentoriga/documentorigacombinazionevarianti", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "descrizione", DisplayName = "Descrizione", DataPath = "Righe[].Descrizione", SourceTable = "documentoriga", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "ordine_riga", DisplayName = "Ordine riga", DataPath = "Righe[].OrdineRiga", SourceTable = "documentoriga", DataType = "int", Notes = "Usato dal layout legacy Pos.repx per l'ordinamento righe." },
                        new FastReportDataFieldDefinition { Key = "quantita", DisplayName = "Quantita`", DataPath = "Righe[].Quantita", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "unita_misura", DisplayName = "Unita` di misura", DataPath = "Righe[].UnitaMisura", SourceTable = "documentoriga/unitadimisura", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "prezzo_unitario", DisplayName = "Prezzo unitario", DataPath = "Righe[].PrezzoUnitario", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "sconto_percentuale", DisplayName = "Sconto %", DataPath = "Righe[].ScontoPercentuale", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "sconto2", DisplayName = "Sconto 2", DataPath = "Righe[].Sconto2", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "importo_riga", DisplayName = "Importo riga", DataPath = "Righe[].ImportoRiga", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "aliquota_iva", DisplayName = "Aliquota IVA", DataPath = "Righe[].AliquotaIva", SourceTable = "documentoriga/iva", DataType = "decimal" }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "articolo",
                    DisplayName = "Articolo",
                    IsCollection = true,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "riga_oid", DisplayName = "Oid riga", DataPath = "Articolo[].RigaOid", SourceTable = "documentoriga", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "codice_articolo", DisplayName = "Codice articolo", DataPath = "Articolo[].CodiceArticolo", SourceTable = "documentoriga/articolo", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "barcode", DisplayName = "Barcode", DataPath = "Articolo[].Barcode", SourceTable = "documentoriga/documentorigacombinazionevarianti", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "descrizione", DisplayName = "Descrizione", DataPath = "Articolo[].Descrizione", SourceTable = "documentoriga", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "ordine_riga", DisplayName = "Ordine riga", DataPath = "Articolo[].OrdineRiga", SourceTable = "documentoriga", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "quantita", DisplayName = "Quantita`", DataPath = "Articolo[].Quantita", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "unita_misura", DisplayName = "Unita` di misura", DataPath = "Articolo[].UnitaMisura", SourceTable = "documentoriga/unitadimisura", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "prezzo_unitario", DisplayName = "Prezzo unitario", DataPath = "Articolo[].PrezzoUnitario", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "sconto_percentuale", DisplayName = "Sconto %", DataPath = "Articolo[].ScontoPercentuale", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "sconto2", DisplayName = "Sconto 2", DataPath = "Articolo[].Sconto2", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "importo_riga", DisplayName = "Importo riga", DataPath = "Articolo[].ImportoRiga", SourceTable = "documentoriga", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "aliquota_iva", DisplayName = "Aliquota IVA", DataPath = "Articolo[].AliquotaIva", SourceTable = "documentoriga/iva", DataType = "decimal" }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "pagamenti",
                    DisplayName = "Pagamenti",
                    IsCollection = true,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "tipo", DisplayName = "Tipo pagamento", DataPath = "Pagamenti[].Tipo", SourceTable = "pagamento/documento", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "importo", DisplayName = "Importo", DataPath = "Pagamenti[].Importo", SourceTable = "pagamento/documento", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "importo_visuale", DisplayName = "Importo visuale", DataPath = "Pagamenti[].ImportoVisuale", SourceTable = "pagamento/documento", DataType = "string" }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "totali",
                    DisplayName = "Totali documento",
                    IsCollection = false,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "totale_documento", DisplayName = "Totale documento", DataPath = "Totali.TotaleDocumento", SourceTable = "documento", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "totale_imponibile", DisplayName = "Totale imponibile", DataPath = "Totali.TotaleImponibile", SourceTable = "documentoiva/documento", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "totale_iva", DisplayName = "Totale IVA", DataPath = "Totali.TotaleIva", SourceTable = "documentoiva", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "totale_pagato", DisplayName = "Totale pagato", DataPath = "Totali.TotalePagato", SourceTable = "pagamento/documento", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "totale_pagato_visuale", DisplayName = "Totale pagato visuale", DataPath = "Totali.TotalePagatoVisuale", SourceTable = "pagamento/documento", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "pagato_carta_visuale", DisplayName = "Pagato carta visuale", DataPath = "Totali.PagatoCartaVisuale", SourceTable = "documento.Pagatocartacredito", DataType = "string" }
                    ]
                }
            ]
        },
        new FastReportDocumentSchema
        {
            DocumentKey = "customers-list",
            DisplayName = "Elenco clienti",
            RootObjectName = "ClientiReport",
            DataSources =
            [
                new FastReportDataSourceDefinition
                {
                    Key = "report",
                    DisplayName = "Testata report",
                    IsCollection = false,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "titolo", DisplayName = "Titolo report", DataPath = "Report.Title", SourceTable = "runtime report", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "data_stampa", DisplayName = "Data stampa", DataPath = "Report.PrintDate", SourceTable = "runtime report", DataType = "date" },
                        new FastReportDataFieldDefinition { Key = "filtro", DisplayName = "Filtro attivo", DataPath = "Report.FilterSummary", SourceTable = "runtime report", DataType = "string" }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "clienti",
                    DisplayName = "Clienti",
                    IsCollection = true,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "oid", DisplayName = "Oid cliente", DataPath = "Clienti[].Oid", SourceTable = "soggetto", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "nominativo", DisplayName = "Nominativo", DataPath = "Clienti[].Nominativo", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "indirizzo", DisplayName = "Indirizzo", DataPath = "Clienti[].Indirizzo", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "cap", DisplayName = "CAP", DataPath = "Clienti[].Cap", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "citta", DisplayName = "Citta`", DataPath = "Clienti[].Citta", SourceTable = "soggetto/citta", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "provincia", DisplayName = "Provincia", DataPath = "Clienti[].Provincia", SourceTable = "citta", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "telefono", DisplayName = "Telefono", DataPath = "Clienti[].Telefono", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "email", DisplayName = "Email", DataPath = "Clienti[].Email", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "partita_iva", DisplayName = "Partita IVA", DataPath = "Clienti[].PartitaIva", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "codice_fiscale", DisplayName = "Codice fiscale", DataPath = "Clienti[].CodiceFiscale", SourceTable = "soggetto", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "punti_attuali", DisplayName = "Punti attuali", DataPath = "Clienti[].PuntiAttuali", SourceTable = "soggetto", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "punti_precedenti", DisplayName = "Punti precedenti", DataPath = "Clienti[].PuntiPrecedenti", SourceTable = "soggetto", DataType = "decimal" }
                    ]
                }
            ]
        },
        new FastReportDocumentSchema
        {
            DocumentKey = "articles-list",
            DisplayName = "Lista articoli",
            RootObjectName = "ArticoliReport",
            DataSources =
            [
                new FastReportDataSourceDefinition
                {
                    Key = "report",
                    DisplayName = "Testata report",
                    IsCollection = false,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "titolo", DisplayName = "Titolo report", DataPath = "Report.Title", SourceTable = "runtime report", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "data_stampa", DisplayName = "Data stampa", DataPath = "Report.PrintDate", SourceTable = "runtime report", DataType = "date" },
                        new FastReportDataFieldDefinition { Key = "filtro_categoria", DisplayName = "Filtro categoria", DataPath = "Report.CategoryFilter", SourceTable = "runtime report", DataType = "string" }
                    ]
                },
                new FastReportDataSourceDefinition
                {
                    Key = "articoli",
                    DisplayName = "Articoli",
                    IsCollection = true,
                    Fields =
                    [
                        new FastReportDataFieldDefinition { Key = "oid", DisplayName = "Oid articolo", DataPath = "Articoli[].Oid", SourceTable = "articolo", DataType = "int" },
                        new FastReportDataFieldDefinition { Key = "codice", DisplayName = "Codice articolo", DataPath = "Articoli[].Codice", SourceTable = "articolo", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "descrizione", DisplayName = "Descrizione", DataPath = "Articoli[].Descrizione", SourceTable = "articolo", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "unita_misura", DisplayName = "Unita` di misura", DataPath = "Articoli[].UnitaMisura", SourceTable = "articolo/unitadimisura", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "prezzo_vendita", DisplayName = "Prezzo vendita", DataPath = "Articoli[].PrezzoVendita", SourceTable = "articolo/listino", DataType = "decimal", Notes = "Da confermare sul contratto finale FM." },
                        new FastReportDataFieldDefinition { Key = "prezzo_acquisto", DisplayName = "Prezzo acquisto", DataPath = "Articoli[].PrezzoAcquisto", SourceTable = "articolo/costo", DataType = "decimal", Notes = "Da confermare sul contratto finale FM." },
                        new FastReportDataFieldDefinition { Key = "aliquota_iva", DisplayName = "Aliquota IVA", DataPath = "Articoli[].AliquotaIva", SourceTable = "iva/articolo", DataType = "decimal" },
                        new FastReportDataFieldDefinition { Key = "giacenza", DisplayName = "Giacenza", DataPath = "Articoli[].Giacenza", SourceTable = "giacenze FM", DataType = "decimal", Notes = "Da verificare sulla sorgente reale." },
                        new FastReportDataFieldDefinition { Key = "barcode", DisplayName = "Barcode", DataPath = "Articoli[].Barcode", SourceTable = "articolo/varianti", DataType = "string" },
                        new FastReportDataFieldDefinition { Key = "fornitore_principale", DisplayName = "Fornitore principale", DataPath = "Articoli[].FornitorePrincipale", SourceTable = "articolofornitore", DataType = "string", Notes = "Da verificare sulla sorgente reale." }
                    ]
                }
            ]
        }
    ];

    public Task<IReadOnlyList<FastReportDocumentSchema>> GetSchemasAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Schemas);
    }

    public Task<FastReportDocumentSchema?> GetSchemaAsync(string documentKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var schema = Schemas.FirstOrDefault(item =>
            string.Equals(item.DocumentKey, documentKey, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(schema);
    }
}
