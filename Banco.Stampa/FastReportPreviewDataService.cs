using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Documents;
using MySqlConnector;

namespace Banco.Stampa;

public sealed class FastReportPreviewDataService : IFastReportPreviewDataService
{
    private const int ExampleDocumentNumber = 2308;
    private const int ExampleModelDocument = 27;

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IGestionaleDocumentReadService _documentReadService;

    public FastReportPreviewDataService(
        IApplicationConfigurationService configurationService,
        IGestionaleDocumentReadService documentReadService)
    {
        _configurationService = configurationService;
        _documentReadService = documentReadService;
    }

    public async Task<FastReportPreviewDocument> GetPreviewDocumentAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        await using var connection = await CreateOpenConnectionAsync(settings.GestionaleDatabase, cancellationToken);

        var documentOid = await FindExampleDocumentOidAsync(connection, cancellationToken);
        if (documentOid <= 0)
        {
            throw new InvalidOperationException($"Documento esempio n. {ExampleDocumentNumber} non trovato su modello {ExampleModelDocument}.");
        }

        var detail = await _documentReadService.GetDocumentDetailAsync(documentOid, cancellationToken)
            ?? throw new InvalidOperationException($"Dettaglio documento OID {documentOid} non trovato.");

        var customer = await LoadCustomerAsync(connection, detail.SoggettoOid, cancellationToken);
        var rows = detail.Righe.Select(MapRow).ToArray();

        return new FastReportPreviewDocument
        {
            Testata = new FastReportPreviewHeader
            {
                DocumentoOid = detail.Oid,
                Numero = detail.Numero,
                Anno = detail.Anno,
                Data = detail.Data,
                EtichettaDocumento = "Vendita al banco",
                ModelloDocumento = "Banco 80 mm",
                Operatore = detail.Operatore,
                StatoRuntime = detail.StatoLabel,
                NumeroCompleto = detail.DocumentoLabel,
                DataTesto = detail.Data.ToString("dd/MM/yyyy"),
                PagamentoLabel = ResolvePaymentLabel(detail),
                DocumentoLabel = "Vendita al banco",
                AnnoVisuale = detail.Anno.ToString(),
                ProgressivoVenditaLabel = $"Progressivo vendita {detail.Numero}/{detail.Anno}"
            },
            Cliente = customer,
            Righe = rows,
            Pagamenti = BuildPayments(detail),
            Totali = new FastReportPreviewTotals
            {
                TotaleDocumento = detail.TotaleDocumento,
                TotaleImponibile = detail.TotaleImponibile,
                TotaleIva = detail.TotaleIva,
                TotalePagato = detail.TotalePagatoUfficiale,
                TotaleDocumentoVisuale = FormatMoney(detail.TotaleDocumento),
                TotalePagatoVisuale = FormatMoney(detail.TotalePagatoUfficiale),
                ScontoPercentualeVisuale = "0",
                ScontoImportoVisuale = "0,00 EUR",
                ContantiVisuale = FormatMoneyWithCurrency(detail.PagatoContanti),
                PagatoCartaVisuale = FormatMoneyWithCurrency(detail.PagatoCarta),
                PagamentoPrincipaleLabel = ResolvePaymentLabel(detail),
                PagamentoPrincipaleImportoVisuale = FormatMoneyWithCurrency(ResolvePrimaryPaymentAmount(detail)),
                RestoVisuale = "0,00 EUR"
            }
        };
    }

    private static async Task<MySqlConnection> CreateOpenConnectionAsync(
        GestionaleDatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Host,
            Port = (uint)settings.Port,
            Database = settings.Database,
            UserID = settings.Username,
            Password = settings.Password,
            ConnectionTimeout = 5
        };

        if (!string.IsNullOrWhiteSpace(settings.CharacterSet))
        {
            builder.CharacterSet = settings.CharacterSet.Trim();
        }

        var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<int> FindExampleDocumentOidAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT OID
            FROM documento
            WHERE Numero = @numero
              AND Modellodocumento = @modellodocumento
            ORDER BY OID DESC
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@numero", ExampleDocumentNumber);
        command.Parameters.AddWithValue("@modellodocumento", ExampleModelDocument);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private static async Task<FastReportPreviewCustomer> LoadCustomerAsync(
        MySqlConnection connection,
        int soggettoOid,
        CancellationToken cancellationToken)
    {
        if (soggettoOid <= 0)
        {
            return new FastReportPreviewCustomer
            {
                Nominativo = "Cliente generico",
                PuntiPrecedentiVisuale = "n.d.",
                PuntiAttualiVisuale = "0"
            };
        }

        const string sql =
            """
            SELECT
                s.OID,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), NULLIF(TRIM(s.Rappresentantelegalenome), ''), CONCAT('Soggetto #', s.OID)) AS Nominativo,
                COALESCE(NULLIF(TRIM(s.Indirizzo), ''), '') AS Indirizzo,
                COALESCE(NULLIF(TRIM(s.Telefono), ''), '') AS Telefono,
                COALESCE(NULLIF(TRIM(s.Email), ''), '') AS Email,
                COALESCE(NULLIF(TRIM(s.Partitaiva), ''), '') AS PartitaIva,
                COALESCE(NULLIF(TRIM(s.Codicefiscale), ''), '') AS CodiceFiscale,
                COALESCE(NULLIF(TRIM(s.Codicecartafedelta), ''), '') AS CodiceCartaFedelta,
                COALESCE(s.Punticartafedeltainiziali, 0) AS PuntiPrecedenti,
                COALESCE(s.Punticartafedelta, 0) AS PuntiAttuali,
                COALESCE(NULLIF(TRIM(c.Cap), ''), COALESCE(NULLIF(TRIM(s.Cap), ''), '')) AS Cap,
                COALESCE(NULLIF(TRIM(c.Citta), ''), '') AS Citta,
                COALESCE(NULLIF(TRIM(c.Provincia), ''), '') AS Provincia
            FROM soggetto s
            LEFT JOIN citta c ON c.OID = s.Citta
            WHERE s.OID = @soggettoOid
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@soggettoOid", soggettoOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new FastReportPreviewCustomer
            {
                ClienteOid = soggettoOid,
                Nominativo = $"Cliente #{soggettoOid}",
                PuntiPrecedentiVisuale = "n.d.",
                PuntiAttualiVisuale = "0"
            };
        }

        var indirizzo = SafeGetString(reader, "Indirizzo");
        var cap = SafeGetString(reader, "Cap");
        var citta = SafeGetString(reader, "Citta");
        var provincia = SafeGetString(reader, "Provincia");
        var telefono = SafeGetString(reader, "Telefono");
        var email = SafeGetString(reader, "Email");
        var partitaIva = SafeGetString(reader, "PartitaIva");
        var puntiPrecedentiOrdinal = reader.GetOrdinal("PuntiPrecedenti");
        var puntiAttualiOrdinal = reader.GetOrdinal("PuntiAttuali");
        var oidOrdinal = reader.GetOrdinal("OID");
        var puntiPrecedenti = reader.IsDBNull(puntiPrecedentiOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(puntiPrecedentiOrdinal));
        var puntiAttuali = reader.IsDBNull(puntiAttualiOrdinal) ? 0 : Convert.ToDecimal(reader.GetValue(puntiAttualiOrdinal));

        return new FastReportPreviewCustomer
        {
            ClienteOid = reader.GetInt32(oidOrdinal),
            Nominativo = SafeGetString(reader, "Nominativo"),
            Indirizzo = indirizzo,
            Cap = cap,
            Citta = citta,
            Provincia = provincia,
            PartitaIva = partitaIva,
            CodiceFiscale = SafeGetString(reader, "CodiceFiscale"),
            CodiceCartaFedelta = SafeGetString(reader, "CodiceCartaFedelta"),
            Telefono = telefono,
            Email = email,
            IndirizzoCompleto = ComposeAddress(indirizzo, cap, citta, provincia),
            ContattiCompleti = ComposeContacts(telefono, email),
            FiscaleCompleto = string.Join(" - ", new[] { partitaIva, SafeGetString(reader, "CodiceFiscale") }.Where(value => !string.IsNullOrWhiteSpace(value))),
            PuntiPrecedentiVisuale = FormatPoints(puntiPrecedenti),
            PuntiAttualiVisuale = FormatPoints(puntiAttuali)
        };
    }

    private static FastReportPreviewRow MapRow(GestionaleDocumentRowDetail row)
    {
        var showQuantita = row.Quantita != 0;
        var quantita = showQuantita ? row.Quantita.ToString("0.##") : string.Empty;
        var prezzo = !showQuantita || row.PrezzoUnitario == 0 ? string.Empty : FormatMoney(row.PrezzoUnitario);
        var sconto = row.ScontoPercentuale <= 0 ? string.Empty : row.ScontoPercentuale.ToString("0.##");
        var sconto2 = row.Sconto2 <= 0 ? string.Empty : row.Sconto2.ToString("0.##");
        var importo = !showQuantita || row.ImportoRiga == 0 ? string.Empty : FormatMoney(row.ImportoRiga);

        return new FastReportPreviewRow
        {
            RigaOid = row.Oid,
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
            AliquotaIva = 0,
            QuantitaVisuale = quantita,
            PrezzoUnitarioVisuale = prezzo,
            ScontoVisuale = sconto,
            Sconto2Visuale = sconto2,
            ImportoRigaVisuale = importo
        };
    }

    private static IReadOnlyList<FastReportPreviewPayment> BuildPayments(GestionaleDocumentDetail detail)
    {
        var payments = new List<FastReportPreviewPayment>();

        AddPayment(payments, "Contanti", detail.PagatoContanti);
        AddPayment(payments, "Carta", detail.PagatoCarta);
        AddPayment(payments, "Web", detail.PagatoWeb);
        AddPayment(payments, "Buoni", detail.PagatoBuoni);
        AddPayment(payments, "Sospeso", detail.PagatoSospeso);

        return payments;
    }

    private static void AddPayment(List<FastReportPreviewPayment> payments, string type, decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        payments.Add(new FastReportPreviewPayment
        {
            Tipo = type,
            Importo = amount,
            ImportoVisuale = FormatMoney(amount)
        });
    }

    private static string ResolvePaymentLabel(GestionaleDocumentDetail detail)
    {
        if (detail.PagatoBuoni > 0)
        {
            return "Buoni";
        }

        if (detail.PagatoCarta > 0)
        {
            return "Carta";
        }

        if (detail.PagatoWeb > 0)
        {
            return "Web";
        }

        if (detail.PagatoSospeso > 0)
        {
            return "Sospeso";
        }

        return "Contanti";
    }

    private static decimal ResolvePrimaryPaymentAmount(GestionaleDocumentDetail detail)
    {
        if (detail.PagatoBuoni > 0)
        {
            return detail.PagatoBuoni;
        }

        if (detail.PagatoCarta > 0)
        {
            return detail.PagatoCarta;
        }

        if (detail.PagatoWeb > 0)
        {
            return detail.PagatoWeb;
        }

        if (detail.PagatoSospeso > 0)
        {
            return detail.PagatoSospeso;
        }

        return detail.PagatoContanti;
    }

    private static string ComposeAddress(string indirizzo, string cap, string citta, string provincia)
    {
        var city = string.IsNullOrWhiteSpace(citta)
            ? string.Empty
            : string.IsNullOrWhiteSpace(provincia)
                ? citta.Trim()
                : $"{citta.Trim()} ({provincia.Trim()})";

        return string.Join(" - ",
            new[] { indirizzo, string.Join(" ", new[] { cap, city }.Where(value => !string.IsNullOrWhiteSpace(value))) }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));
    }

    private static string ComposeContacts(string telefono, string email)
    {
        var items = new List<string>();
        if (!string.IsNullOrWhiteSpace(telefono))
        {
            items.Add($"Tel. {telefono.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            items.Add($"Email {email.Trim()}");
        }

        return string.Join(" - ", items);
    }

    private static string SafeGetString(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal).Trim();
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
