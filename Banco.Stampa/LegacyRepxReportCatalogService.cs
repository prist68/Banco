namespace Banco.Stampa;

public sealed class LegacyRepxReportCatalogService : ILegacyRepxReportCatalogService
{
    private static readonly IReadOnlyList<LegacyRepxReportReference> Reports =
    [
        new LegacyRepxReportReference
        {
            Id = "legacy-pos-80",
            DocumentKey = "receipt-80-db",
            DisplayName = "Pos.repx legacy",
            SourceFilePath = @"C:\Facile Manager\DILTECH\Report\Pos.repx",
            LegacyPrinterName = "POS-80",
            PageWidth = 720,
            RollPaper = true,
            Notes = "Riferimento legacy DevExpress/XtraReport usato come blueprint del nuovo layout FastReport POS 80 mm.",
            Sections =
            [
                "PageHeader con logo, ragione sociale, recapiti e dati banca.",
                "DetailReport su righe documento ordinato per Ordineriga.",
                "ReportFooter con totale pagato, barcode, testo note e messaggi legali.",
                "PageFooter presente ma non portante per l'impaginazione del rotolo."
            ],
            Parameters =
            [
                new LegacyRepxParameterReference { Name = "Ragionesociale", DisplayName = "Ragione sociale" },
                new LegacyRepxParameterReference { Name = "Partitaiva", DisplayName = "Partita IVA" },
                new LegacyRepxParameterReference { Name = "Codicefiscale", DisplayName = "Codice fiscale" },
                new LegacyRepxParameterReference { Name = "Indirizzo", DisplayName = "Indirizzo" },
                new LegacyRepxParameterReference { Name = "Cap", DisplayName = "CAP" },
                new LegacyRepxParameterReference { Name = "Citta", DisplayName = "Citta`" },
                new LegacyRepxParameterReference { Name = "Provincia", DisplayName = "Provincia" },
                new LegacyRepxParameterReference { Name = "Telefono", DisplayName = "Telefono" },
                new LegacyRepxParameterReference { Name = "Email", DisplayName = "Email" },
                new LegacyRepxParameterReference { Name = "Sitoweb", DisplayName = "Sito web" },
                new LegacyRepxParameterReference { Name = "Bancanome", DisplayName = "Nome banca" },
                new LegacyRepxParameterReference { Name = "Bancaiban", DisplayName = "IBAN banca" },
                new LegacyRepxParameterReference { Name = "Etichettadocumento", DisplayName = "Etichetta documento", Notes = "Usata per intestazione dinamica del tipo documento." }
            ],
            Bindings =
            [
                new LegacyRepxBindingReference { Band = "DetailReport.Detail1", ControlName = "lblQuantita", Expression = "[Documentoriga.Quantita]" },
                new LegacyRepxBindingReference { Band = "DetailReport.Detail1", ControlName = "label44", Expression = "[Documentoriga.Descrizione]" },
                new LegacyRepxBindingReference { Band = "DetailReport.Detail1", ControlName = "lblValoreunitario", Expression = "[Documentoriga.Valoreunitario]" },
                new LegacyRepxBindingReference { Band = "DetailReport.Detail1", ControlName = "lblSconto1", Expression = "[Documentoriga.Sconto1]" },
                new LegacyRepxBindingReference { Band = "DetailReport.Detail1", ControlName = "lblImportoriga", Expression = "[Documentoriga.Importoriga]" },
                new LegacyRepxBindingReference { Band = "ReportFooter", ControlName = "label17", Expression = "[Pagato]", Notes = "Totale pagato stampato nel footer." }
            ],
            Rules =
            [
                new LegacyRepxRuleReference { Name = "lblQuantita_BeforePrint", Description = "Se la quantita` vale 0, il testo viene nascosto." },
                new LegacyRepxRuleReference { Name = "lblValoreunitario_BeforePrint", Description = "Il prezzo unitario viene nascosto quando la quantita` non e` visibile." },
                new LegacyRepxRuleReference { Name = "lblSconto_BeforePrint", Description = "Lo sconto viene nascosto quando vale 0,00." },
                new LegacyRepxRuleReference { Name = "lblImportoriga_BeforePrint", Description = "L'importo riga viene nascosto se la quantita` risulta a zero." },
                new LegacyRepxRuleReference { Name = "CalculatedFields", Description = "Compone telefono/email, partita IVA/codice fiscale e indirizzo completo da parametri separati." }
            ]
        }
    ];

    public Task<IReadOnlyList<LegacyRepxReportReference>> GetReportsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Reports);
    }
}
