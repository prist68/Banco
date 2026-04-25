using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Documents;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleDocumentReadService : IGestionaleDocumentReadService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleDocumentReadService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<IReadOnlyList<GestionaleDocumentSummary>> GetRecentBancoDocumentsAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                d.OID,
                d.Numero,
                d.Anno,
                d.Data,
                d.Soggetto,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), NULLIF(TRIM(s.Rappresentantelegalenome), ''), CONCAT('Soggetto #', d.Soggetto)) AS SoggettoNominativo,
                d.Totaledocumento,
                d.Totaleimponibile,
                d.Totaleiva,
                d.Utente,
                CASE
                    WHEN NULLIF(TRIM(s.Codicecartafedelta), '') IS NULL THEN NULL
                    WHEN s.Punticartafedelta IS NOT NULL THEN s.Punticartafedelta
                    WHEN s.Punticartafedeltainiziali IS NOT NULL THEN s.Punticartafedeltainiziali
                    ELSE NULL
                END AS CustomerPoints,
                COALESCE(d.Pagato, 0) AS PagatoContanti,
                COALESCE(d.Pagatocartacredito, 0) AS PagatoCarta,
                COALESCE(d.Pagatoweb, 0) AS PagatoWeb,
                COALESCE(d.Pagatobuonipasto, 0) AS PagatoBuoni,
                d.Pagatosospeso,
                (COALESCE(d.Pagato, 0) + COALESCE(d.Pagatocartacredito, 0) + COALESCE(d.Pagatoweb, 0) + COALESCE(d.Pagatobuonipasto, 0)) AS TotalePagatoUfficiale,
                d.Fatturato,
                NULLIF(TRIM(d.Scontrinonumero), '') AS Scontrinonumero
            FROM documento d
            LEFT JOIN soggetto s ON s.OID = d.Soggetto
            WHERE d.Modellodocumento = 27
            ORDER BY d.Data DESC, d.OID DESC
            LIMIT @maxResults;
            """;
        command.Parameters.AddWithValue("@maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadDocumentSummariesAsync(reader, cancellationToken);
    }

    public async Task<GestionaleDocumentDetail?> GetDocumentDetailAsync(
        int documentOid,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var rowColumns = await GetTableColumnsAsync(connection, "documentoriga", cancellationToken);
        var sconto1Expression = ResolveScontoExpression(rowColumns, 1);
        var sconto2Expression = ResolveScontoExpression(rowColumns, 2);
        var sconto3Expression = ResolveScontoExpression(rowColumns, 3);
        var sconto4Expression = ResolveScontoExpression(rowColumns, 4);

        GestionaleDocumentDetail? detail = null;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT
                    d.OID,
                    d.Numero,
                    d.Anno,
                    d.Data,
                    d.Soggetto,
                    COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), NULLIF(TRIM(s.Rappresentantelegalenome), ''), CONCAT('Soggetto #', d.Soggetto)) AS SoggettoNominativo,
                    d.Totaledocumento,
                    d.Totaleimponibile,
                    d.Totaleiva,
                    COALESCE(d.Pagato, 0) AS PagatoContanti,
                    COALESCE(d.Pagatocartacredito, 0) AS PagatoCarta,
                    COALESCE(d.Pagatoweb, 0) AS PagatoWeb,
                    COALESCE(d.Pagatobuonipasto, 0) AS PagatoBuoni,
                    COALESCE(d.Pagatosospeso, 0) AS PagatoSospeso,
                    (COALESCE(d.Pagato, 0) + COALESCE(d.Pagatocartacredito, 0) + COALESCE(d.Pagatoweb, 0) + COALESCE(d.Pagatobuonipasto, 0)) AS TotalePagatoUfficiale,
                    d.Fatturato,
                    NULLIF(TRIM(d.Scontrinonumero), '') AS Scontrinonumero,
                    d.Listino AS ListinoOid,
                    NULLIF(TRIM(l.Listino), '') AS ListinoNome,
                    d.Utente
                FROM documento d
                LEFT JOIN soggetto s ON s.OID = d.Soggetto
                LEFT JOIN listino l ON l.OID = d.Listino
                WHERE d.OID = @documentOid
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("@documentOid", documentOid);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var oidOrdinal = reader.GetOrdinal("OID");
                var numeroOrdinal = reader.GetOrdinal("Numero");
                var annoOrdinal = reader.GetOrdinal("Anno");
                var dataOrdinal = reader.GetOrdinal("Data");
                var soggettoOrdinal = reader.GetOrdinal("Soggetto");
                var soggettoNominativoOrdinal = reader.GetOrdinal("SoggettoNominativo");
                var totaleOrdinal = reader.GetOrdinal("Totaledocumento");
                var imponibileOrdinal = reader.GetOrdinal("Totaleimponibile");
                var ivaOrdinal = reader.GetOrdinal("Totaleiva");
                var pagatoContantiOrdinal = reader.GetOrdinal("PagatoContanti");
                var pagatoCartaOrdinal = reader.GetOrdinal("PagatoCarta");
                var pagatoWebOrdinal = reader.GetOrdinal("PagatoWeb");
                var pagatoBuoniOrdinal = reader.GetOrdinal("PagatoBuoni");
                var pagatoSospesoOrdinal = reader.GetOrdinal("PagatoSospeso");
                var totalePagatoUfficialeOrdinal = reader.GetOrdinal("TotalePagatoUfficiale");
                var fatturatoOrdinal = reader.GetOrdinal("Fatturato");
                var scontrinoOrdinal = reader.GetOrdinal("Scontrinonumero");
                var listinoOidOrdinal = reader.GetOrdinal("ListinoOid");
                var listinoNomeOrdinal = reader.GetOrdinal("ListinoNome");
                var utenteOrdinal = reader.GetOrdinal("Utente");

                detail = new GestionaleDocumentDetail
                {
                    Oid = reader.GetInt32(oidOrdinal),
                    Numero = reader.GetInt32(numeroOrdinal),
                    Anno = reader.GetInt32(annoOrdinal),
                    Data = reader.GetDateTime(dataOrdinal),
                    SoggettoOid = reader.IsDBNull(soggettoOrdinal) ? 0 : reader.GetInt32(soggettoOrdinal),
                    SoggettoNominativo = reader.IsDBNull(soggettoNominativoOrdinal) ? string.Empty : reader.GetString(soggettoNominativoOrdinal),
                    TotaleDocumento = reader.IsDBNull(totaleOrdinal) ? 0 : reader.GetDecimal(totaleOrdinal),
                    TotaleImponibile = reader.IsDBNull(imponibileOrdinal) ? 0 : reader.GetDecimal(imponibileOrdinal),
                    TotaleIva = reader.IsDBNull(ivaOrdinal) ? 0 : reader.GetDecimal(ivaOrdinal),
                    PagatoContanti = reader.IsDBNull(pagatoContantiOrdinal) ? 0 : reader.GetDecimal(pagatoContantiOrdinal),
                    PagatoCarta = reader.IsDBNull(pagatoCartaOrdinal) ? 0 : reader.GetDecimal(pagatoCartaOrdinal),
                    PagatoWeb = reader.IsDBNull(pagatoWebOrdinal) ? 0 : reader.GetDecimal(pagatoWebOrdinal),
                    PagatoBuoni = reader.IsDBNull(pagatoBuoniOrdinal) ? 0 : reader.GetDecimal(pagatoBuoniOrdinal),
                    PagatoSospeso = reader.IsDBNull(pagatoSospesoOrdinal) ? 0 : reader.GetDecimal(pagatoSospesoOrdinal),
                    TotalePagatoUfficiale = reader.IsDBNull(totalePagatoUfficialeOrdinal) ? 0 : reader.GetDecimal(totalePagatoUfficialeOrdinal),
                    Fatturato = reader.IsDBNull(fatturatoOrdinal) ? null : reader.GetInt32(fatturatoOrdinal),
                    ScontrinoNumero = reader.IsDBNull(scontrinoOrdinal) ? null : reader.GetInt32(scontrinoOrdinal),
                    ListinoOid = reader.IsDBNull(listinoOidOrdinal) ? null : reader.GetInt32(listinoOidOrdinal),
                    ListinoNome = reader.IsDBNull(listinoNomeOrdinal) ? null : reader.GetString(listinoNomeOrdinal),
                    Operatore = reader.IsDBNull(utenteOrdinal) ? string.Empty : reader.GetString(utenteOrdinal)
                };
            }
        }

        if (detail is null)
        {
            return null;
        }

        var righe = new List<GestionaleDocumentRowDetail>();

        await using (var rowCommand = connection.CreateCommand())
        {
            rowCommand.CommandText =
                $"""
                SELECT
                    r.OID,
                    r.Codicearticolo,
                    COALESCE(NULLIF(TRIM(c.Codiceabarre), ''), NULLIF(TRIM(r.Codiceabarre), '')) AS BarcodeArticolo,
                    r.Descrizione,
                    COALESCE(NULLIF(TRIM(um.Unitadimisura), ''), 'PZ') AS UnitaMisura,
                    r.Quantita,
                    r.Valoreunitario,
                    {sconto1Expression} AS Sconto1,
                    {sconto2Expression} AS Sconto2,
                    {sconto3Expression} AS Sconto3,
                    {sconto4Expression} AS Sconto4,
                    r.Importoriga,
                    r.Iva,
                    r.Articolo,
                    c.Variantedettaglio1,
                    c.Variantedettaglio2
                FROM documentoriga r
                LEFT JOIN unitadimisura um ON um.OID = r.Unitadimisura
                LEFT JOIN documentorigacombinazionevarianti c ON c.Documentoriga = r.OID
                WHERE r.Documento = @documentOid
                ORDER BY r.OID;
                """;
            rowCommand.Parameters.AddWithValue("@documentOid", documentOid);

            await using var rowReader = await rowCommand.ExecuteReaderAsync(cancellationToken);
            var index = 1;
            var oidOrdinal = rowReader.GetOrdinal("OID");
            var codiceOrdinal = rowReader.GetOrdinal("Codicearticolo");
            var barcodeOrdinal = rowReader.GetOrdinal("BarcodeArticolo");
            var descrizioneOrdinal = rowReader.GetOrdinal("Descrizione");
            var unitaMisuraOrdinal = rowReader.GetOrdinal("UnitaMisura");
            var quantitaOrdinal = rowReader.GetOrdinal("Quantita");
            var valoreOrdinal = rowReader.GetOrdinal("Valoreunitario");
            var sconto1Ordinal = rowReader.GetOrdinal("Sconto1");
            var sconto2Ordinal = rowReader.GetOrdinal("Sconto2");
            var sconto3Ordinal = rowReader.GetOrdinal("Sconto3");
            var sconto4Ordinal = rowReader.GetOrdinal("Sconto4");
            var importoOrdinal = rowReader.GetOrdinal("Importoriga");
            var ivaOrdinal = rowReader.GetOrdinal("Iva");
            var articoloOrdinal = rowReader.GetOrdinal("Articolo");
            var variante1Ordinal = rowReader.GetOrdinal("Variantedettaglio1");
            var variante2Ordinal = rowReader.GetOrdinal("Variantedettaglio2");

            while (await rowReader.ReadAsync(cancellationToken))
            {
                var articoloIsNull = rowReader.IsDBNull(articoloOrdinal);

                righe.Add(new GestionaleDocumentRowDetail
                {
                    Oid = rowReader.GetInt32(oidOrdinal),
                    OrdineRiga = index++,
                    ArticoloOid = articoloIsNull ? null : rowReader.GetInt32(articoloOrdinal),
                    CodiceArticolo = rowReader.IsDBNull(codiceOrdinal) ? null : rowReader.GetString(codiceOrdinal),
                    BarcodeArticolo = rowReader.IsDBNull(barcodeOrdinal) ? null : rowReader.GetString(barcodeOrdinal),
                    Descrizione = rowReader.IsDBNull(descrizioneOrdinal) ? string.Empty : rowReader.GetString(descrizioneOrdinal),
                    UnitaMisura = rowReader.IsDBNull(unitaMisuraOrdinal) ? "PZ" : rowReader.GetString(unitaMisuraOrdinal),
                    Quantita = rowReader.IsDBNull(quantitaOrdinal) ? 0 : rowReader.GetDecimal(quantitaOrdinal),
                    PrezzoUnitario = rowReader.IsDBNull(valoreOrdinal) ? 0 : rowReader.GetDecimal(valoreOrdinal),
                    Sconto1 = rowReader.IsDBNull(sconto1Ordinal) ? 0 : Convert.ToDecimal(rowReader.GetValue(sconto1Ordinal)),
                    Sconto2 = rowReader.IsDBNull(sconto2Ordinal) ? 0 : Convert.ToDecimal(rowReader.GetValue(sconto2Ordinal)),
                    Sconto3 = rowReader.IsDBNull(sconto3Ordinal) ? 0 : Convert.ToDecimal(rowReader.GetValue(sconto3Ordinal)),
                    Sconto4 = rowReader.IsDBNull(sconto4Ordinal) ? 0 : Convert.ToDecimal(rowReader.GetValue(sconto4Ordinal)),
                    ImportoRiga = rowReader.IsDBNull(importoOrdinal) ? 0 : rowReader.GetDecimal(importoOrdinal),
                    IvaOid = rowReader.IsDBNull(ivaOrdinal) ? 0 : rowReader.GetInt32(ivaOrdinal),
                    VarianteDettaglioOid1 = rowReader.IsDBNull(variante1Ordinal) ? null : rowReader.GetInt32(variante1Ordinal),
                    VarianteDettaglioOid2 = rowReader.IsDBNull(variante2Ordinal) ? null : rowReader.GetInt32(variante2Ordinal),
                    TipoRigaLabel = articoloIsNull ? "Manuale" : "Articolo"
                });
            }
        }

        detail.Righe = righe;
        return detail;
    }

    public async Task<(int Numero, int Anno)> GetNextBancoDocumentNumberAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var anno = DateTime.Now.Year;

        await using (var yearCommand = connection.CreateCommand())
        {
            yearCommand.CommandText =
                """
                SELECT COALESCE(MAX(d.Anno), @fallbackYear)
                FROM documento d
                WHERE d.Modellodocumento = 27;
                """;
            yearCommand.Parameters.AddWithValue("@fallbackYear", anno);

            var yearResult = await yearCommand.ExecuteScalarAsync(cancellationToken);
            if (yearResult is not null && yearResult != DBNull.Value)
            {
                anno = Convert.ToInt32(yearResult);
            }
        }

        var numero = 0;

        await using (var numberCommand = connection.CreateCommand())
        {
            numberCommand.CommandText =
                """
                SELECT COALESCE(MAX(d.Numero), 0)
                FROM documento d
                WHERE d.Modellodocumento = 27
                  AND d.Anno = @anno;
                """;
            numberCommand.Parameters.AddWithValue("@anno", anno);

            var numberResult = await numberCommand.ExecuteScalarAsync(cancellationToken);
            if (numberResult is not null && numberResult != DBNull.Value)
            {
                numero = Convert.ToInt32(numberResult);
            }
        }

        return (numero + 1, anno);
    }

    public async Task<IReadOnlyList<GestionaleDocumentSummary>> GetCustomerDocumentsAsync(
        int soggettoOid,
        DateTime? dataInizio = null,
        DateTime? dataFine = null,
        string? filtroArticolo = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        // Query base: documenti banco del cliente
        var sql =
            """
            SELECT
                d.OID,
                d.Numero,
                d.Anno,
                d.Data,
                d.Soggetto,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), NULLIF(TRIM(s.Rappresentantelegalenome), ''), CONCAT('Soggetto #', d.Soggetto)) AS SoggettoNominativo,
                d.Totaledocumento,
                d.Totaleimponibile,
                d.Totaleiva,
                d.Utente,
                CASE
                    WHEN NULLIF(TRIM(s.Codicecartafedelta), '') IS NULL THEN NULL
                    WHEN s.Punticartafedelta IS NOT NULL THEN s.Punticartafedelta
                    WHEN s.Punticartafedeltainiziali IS NOT NULL THEN s.Punticartafedeltainiziali
                    ELSE NULL
                END AS CustomerPoints,
                COALESCE(d.Pagato, 0) AS PagatoContanti,
                COALESCE(d.Pagatocartacredito, 0) AS PagatoCarta,
                COALESCE(d.Pagatoweb, 0) AS PagatoWeb,
                COALESCE(d.Pagatobuonipasto, 0) AS PagatoBuoni,
                d.Pagatosospeso,
                (COALESCE(d.Pagato, 0) + COALESCE(d.Pagatocartacredito, 0) + COALESCE(d.Pagatoweb, 0) + COALESCE(d.Pagatobuonipasto, 0)) AS TotalePagatoUfficiale,
                d.Fatturato,
                NULLIF(TRIM(d.Scontrinonumero), '') AS Scontrinonumero
            FROM documento d
            LEFT JOIN soggetto s ON s.OID = d.Soggetto
            WHERE d.Modellodocumento = 27
              AND d.Soggetto = @soggettoOid
            """;

        command.Parameters.AddWithValue("@soggettoOid", soggettoOid);

        if (dataInizio.HasValue)
        {
            sql += "\n  AND d.Data >= @dataInizio";
            command.Parameters.AddWithValue("@dataInizio", dataInizio.Value);
        }

        if (dataFine.HasValue)
        {
            sql += "\n  AND d.Data <= @dataFine";
            command.Parameters.AddWithValue("@dataFine", dataFine.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtroArticolo))
        {
            sql += """

                  AND EXISTS (
                    SELECT 1 FROM documentoriga r
                    WHERE r.Documento = d.OID
                      AND (r.Descrizione LIKE @filtroArticolo OR r.Codicearticolo LIKE @filtroArticolo)
                  )
                """;
            command.Parameters.AddWithValue("@filtroArticolo", $"%{filtroArticolo.Trim()}%");
        }

        sql += "\nORDER BY d.Data DESC, d.OID DESC\nLIMIT 500;";
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadDocumentSummariesAsync(reader, cancellationToken);
    }

    public async Task<GestionaleArticlePurchaseQuickInfo?> GetLatestArticlePurchaseAsync(
        int articoloOid,
        CancellationToken cancellationToken = default)
    {
        if (articoloOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                r.Articolo AS ArticoloOid,
                d.Data AS DataDocumento,
                d.Soggetto AS FornitoreOid,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), NULLIF(TRIM(s.Rappresentantelegalenome), ''), CONCAT('Soggetto #', d.Soggetto)) AS FornitoreNominativo,
                COALESCE(NULLIF(TRIM(d.Alfa), ''), COALESCE(pn.NumeroDocumento, '')) AS RiferimentoFattura,
                r.Valoreunitario AS PrezzoUnitario
            FROM documentoriga r
            INNER JOIN documento d ON d.OID = r.Documento
            INNER JOIN modellodocumento md ON md.OID = d.Modellodocumento
            LEFT JOIN soggetto s ON s.OID = d.Soggetto
            LEFT JOIN (
                SELECT
                    Documento,
                    MAX(NULLIF(TRIM(Numerodocumento), '')) AS NumeroDocumento
                FROM primanota
                GROUP BY Documento
            ) pn ON pn.Documento = d.OID
            WHERE d.Modellodocumento IN (3, 7)
              AND r.Articolo = @articoloOid
            ORDER BY d.Data DESC, d.OID DESC, r.OID DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@articoloOid", articoloOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var articoloOrdinal = reader.GetOrdinal("ArticoloOid");
        var dataOrdinal = reader.GetOrdinal("DataDocumento");
        var fornitoreOidOrdinal = reader.GetOrdinal("FornitoreOid");
        var fornitoreOrdinal = reader.GetOrdinal("FornitoreNominativo");
        var riferimentoOrdinal = reader.GetOrdinal("RiferimentoFattura");
        var prezzoOrdinal = reader.GetOrdinal("PrezzoUnitario");

        return new GestionaleArticlePurchaseQuickInfo
        {
            ArticoloOid = reader.GetInt32(articoloOrdinal),
            DataUltimoAcquisto = reader.GetDateTime(dataOrdinal),
            FornitoreOid = reader.IsDBNull(fornitoreOidOrdinal) ? null : reader.GetInt32(fornitoreOidOrdinal),
            FornitoreNominativo = reader.IsDBNull(fornitoreOrdinal) ? string.Empty : reader.GetString(fornitoreOrdinal),
            RiferimentoFattura = reader.IsDBNull(riferimentoOrdinal) ? string.Empty : reader.GetString(riferimentoOrdinal),
            PrezzoUnitario = reader.IsDBNull(prezzoOrdinal) ? 0 : reader.GetDecimal(prezzoOrdinal)
        };
    }

    public async Task<GestionaleArticlePurchaseHistoryDetail> SearchArticlePurchaseHistoryAsync(
        GestionaleArticlePurchaseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var sql =
            """
            SELECT
                r.OID AS RigaOid,
                d.OID AS DocumentoOid,
                md.Modellodocumento AS TipoDocumento,
                d.Data AS DataDocumento,
                d.Soggetto AS FornitoreOid,
                COALESCE(NULLIF(TRIM(s.Ragionesociale1), ''), NULLIF(TRIM(s.Rappresentantelegalenome), ''), CONCAT('Soggetto #', d.Soggetto)) AS FornitoreNominativo,
                r.Articolo AS ArticoloOid,
                COALESCE(NULLIF(TRIM(r.Codicearticolo), ''), NULLIF(TRIM(a.Codicearticolo), ''), '') AS CodiceArticolo,
                COALESCE(NULLIF(TRIM(r.Descrizione), ''), NULLIF(TRIM(a.Descrizionearticolo), ''), '') AS DescrizioneArticolo,
                COALESCE(NULLIF(TRIM(d.Alfa), ''), COALESCE(pn.NumeroDocumento, '')) AS RiferimentoFattura,
                r.Quantita,
                r.Valoreunitario,
                COALESCE(r.Importoriga, ROUND(r.Quantita * r.Valoreunitario, 2)) AS TotaleRiga
            FROM documentoriga r
            INNER JOIN documento d ON d.OID = r.Documento
            INNER JOIN modellodocumento md ON md.OID = d.Modellodocumento
            LEFT JOIN soggetto s ON s.OID = d.Soggetto
            LEFT JOIN articolo a ON a.OID = r.Articolo
            LEFT JOIN (
                SELECT
                    Documento,
                    MAX(NULLIF(TRIM(Numerodocumento), '')) AS NumeroDocumento
                FROM primanota
                GROUP BY Documento
            ) pn ON pn.Documento = d.OID
            WHERE d.Modellodocumento IN (3, 7)
              AND r.Articolo IS NOT NULL
            """;

        if (request.ArticoloOid.HasValue && request.ArticoloOid.Value > 0)
        {
            sql += "\n  AND r.Articolo = @articoloOid";
            command.Parameters.AddWithValue("@articoloOid", request.ArticoloOid.Value);
        }

        if (request.FornitoreOid.HasValue && request.FornitoreOid.Value > 0)
        {
            sql += "\n  AND d.Soggetto = @fornitoreOid";
            command.Parameters.AddWithValue("@fornitoreOid", request.FornitoreOid.Value);
        }

        if (request.DataInizio.HasValue)
        {
            sql += "\n  AND d.Data >= @dataInizio";
            command.Parameters.AddWithValue("@dataInizio", request.DataInizio.Value.Date);
        }

        if (request.DataFine.HasValue)
        {
            sql += "\n  AND d.Data < @dataFineEsclusiva";
            command.Parameters.AddWithValue("@dataFineEsclusiva", request.DataFine.Value.Date.AddDays(1));
        }

        sql += "\nORDER BY d.Data DESC, d.OID DESC, r.OID DESC\nLIMIT @maxResults;";
        command.CommandText = sql;
        command.Parameters.AddWithValue("@maxResults", Math.Max(1, request.MaxResults));

        var items = new List<GestionaleArticlePurchaseHistoryItem>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rigaOidOrdinal = reader.GetOrdinal("RigaOid");
        var documentoOidOrdinal = reader.GetOrdinal("DocumentoOid");
        var tipoDocumentoOrdinal = reader.GetOrdinal("TipoDocumento");
        var dataDocumentoOrdinal = reader.GetOrdinal("DataDocumento");
        var fornitoreOidOrdinal = reader.GetOrdinal("FornitoreOid");
        var fornitoreNominativoOrdinal = reader.GetOrdinal("FornitoreNominativo");
        var articoloOidOrdinal = reader.GetOrdinal("ArticoloOid");
        var codiceArticoloOrdinal = reader.GetOrdinal("CodiceArticolo");
        var descrizioneArticoloOrdinal = reader.GetOrdinal("DescrizioneArticolo");
        var riferimentoOrdinal = reader.GetOrdinal("RiferimentoFattura");
        var quantitaOrdinal = reader.GetOrdinal("Quantita");
        var prezzoOrdinal = reader.GetOrdinal("Valoreunitario");
        var totaleRigaOrdinal = reader.GetOrdinal("TotaleRiga");

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new GestionaleArticlePurchaseHistoryItem
            {
                RigaOid = reader.GetInt32(rigaOidOrdinal),
                DocumentoOid = reader.GetInt32(documentoOidOrdinal),
                TipoDocumento = reader.IsDBNull(tipoDocumentoOrdinal) ? string.Empty : reader.GetString(tipoDocumentoOrdinal),
                DataDocumento = reader.GetDateTime(dataDocumentoOrdinal),
                FornitoreOid = reader.IsDBNull(fornitoreOidOrdinal) ? null : reader.GetInt32(fornitoreOidOrdinal),
                FornitoreNominativo = reader.IsDBNull(fornitoreNominativoOrdinal) ? string.Empty : reader.GetString(fornitoreNominativoOrdinal),
                ArticoloOid = reader.IsDBNull(articoloOidOrdinal) ? null : reader.GetInt32(articoloOidOrdinal),
                CodiceArticolo = reader.IsDBNull(codiceArticoloOrdinal) ? string.Empty : reader.GetString(codiceArticoloOrdinal),
                DescrizioneArticolo = reader.IsDBNull(descrizioneArticoloOrdinal) ? string.Empty : reader.GetString(descrizioneArticoloOrdinal),
                RiferimentoFattura = reader.IsDBNull(riferimentoOrdinal) ? string.Empty : reader.GetString(riferimentoOrdinal),
                Quantita = reader.IsDBNull(quantitaOrdinal) ? 0 : reader.GetDecimal(quantitaOrdinal),
                PrezzoUnitario = reader.IsDBNull(prezzoOrdinal) ? 0 : reader.GetDecimal(prezzoOrdinal),
                TotaleRiga = reader.IsDBNull(totaleRigaOrdinal) ? 0 : reader.GetDecimal(totaleRigaOrdinal)
            });
        }

        var firstItem = items.FirstOrDefault();
        return new GestionaleArticlePurchaseHistoryDetail
        {
            Items = items,
            Summary = new GestionaleArticlePurchaseHistorySummary
            {
                TotaleAcquistato = items.Sum(item => item.TotaleRiga),
                PezziAcquistati = items.Sum(item => item.Quantita),
                UltimoPrezzo = firstItem?.PrezzoUnitario ?? 0,
                HasResults = items.Count > 0
            }
        };
    }

    private static async Task<List<GestionaleDocumentSummary>> ReadDocumentSummariesAsync(
        MySqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var results = new List<GestionaleDocumentSummary>();

        var oidOrdinal = reader.GetOrdinal("OID");
        var numeroOrdinal = reader.GetOrdinal("Numero");
        var annoOrdinal = reader.GetOrdinal("Anno");
        var dataOrdinal = reader.GetOrdinal("Data");
        var soggettoOrdinal = reader.GetOrdinal("Soggetto");
        var soggettoNominativoOrdinal = reader.GetOrdinal("SoggettoNominativo");
        var totaleOrdinal = reader.GetOrdinal("Totaledocumento");
        var imponibileOrdinal = reader.GetOrdinal("Totaleimponibile");
        var ivaOrdinal = reader.GetOrdinal("Totaleiva");
        var utenteOrdinal = reader.GetOrdinal("Utente");
        var customerPointsOrdinal = reader.GetOrdinal("CustomerPoints");
        var pagatoContantiOrdinal = reader.GetOrdinal("PagatoContanti");
        var pagatoCartaOrdinal = reader.GetOrdinal("PagatoCarta");
        var pagatoWebOrdinal = reader.GetOrdinal("PagatoWeb");
        var pagatoBuoniOrdinal = reader.GetOrdinal("PagatoBuoni");
        var sospesoOrdinal = reader.GetOrdinal("Pagatosospeso");
        var pagatoUfficialeOrdinal = reader.GetOrdinal("TotalePagatoUfficiale");
        var fatturatoOrdinal = reader.GetOrdinal("Fatturato");
        var scontrinoOrdinal = reader.GetOrdinal("Scontrinonumero");

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GestionaleDocumentSummary
            {
                Oid = reader.GetInt32(oidOrdinal),
                Numero = reader.GetInt32(numeroOrdinal),
                Anno = reader.GetInt32(annoOrdinal),
                Data = reader.GetDateTime(dataOrdinal),
                SoggettoOid = reader.IsDBNull(soggettoOrdinal) ? 0 : reader.GetInt32(soggettoOrdinal),
                SoggettoNominativo = reader.IsDBNull(soggettoNominativoOrdinal) ? string.Empty : reader.GetString(soggettoNominativoOrdinal),
                Operatore = reader.IsDBNull(utenteOrdinal) ? string.Empty : reader.GetString(utenteOrdinal),
                TotaleDocumento = reader.IsDBNull(totaleOrdinal) ? 0 : reader.GetDecimal(totaleOrdinal),
                TotaleImponibile = reader.IsDBNull(imponibileOrdinal) ? 0 : reader.GetDecimal(imponibileOrdinal),
                TotaleIva = reader.IsDBNull(ivaOrdinal) ? 0 : reader.GetDecimal(ivaOrdinal),
                CustomerPoints = reader.IsDBNull(customerPointsOrdinal) ? null : Convert.ToDecimal(reader.GetValue(customerPointsOrdinal)),
                PagatoContanti = reader.IsDBNull(pagatoContantiOrdinal) ? 0 : reader.GetDecimal(pagatoContantiOrdinal),
                PagatoCarta = reader.IsDBNull(pagatoCartaOrdinal) ? 0 : reader.GetDecimal(pagatoCartaOrdinal),
                PagatoWeb = reader.IsDBNull(pagatoWebOrdinal) ? 0 : reader.GetDecimal(pagatoWebOrdinal),
                PagatoBuoni = reader.IsDBNull(pagatoBuoniOrdinal) ? 0 : reader.GetDecimal(pagatoBuoniOrdinal),
                Pagatosospeso = reader.IsDBNull(sospesoOrdinal) ? 0 : reader.GetDecimal(sospesoOrdinal),
                TotalePagatoUfficiale = reader.IsDBNull(pagatoUfficialeOrdinal) ? 0 : reader.GetDecimal(pagatoUfficialeOrdinal),
                Fatturato = reader.IsDBNull(fatturatoOrdinal) ? null : reader.GetInt32(fatturatoOrdinal),
                ScontrinoNumero = reader.IsDBNull(scontrinoOrdinal) ? null : reader.GetInt32(scontrinoOrdinal)
            });
        }

        return results;
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COLUMN_NAME
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName;
            """;
        command.Parameters.AddWithValue("@tableName", tableName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                columns.Add(reader.GetString(0));
            }
        }

        return columns;
    }

    private static string ResolveScontoExpression(IReadOnlySet<string> rowColumns, int numeroSconto)
    {
        var nomeColonna = $"Sconto{numeroSconto}";
        if (rowColumns.Contains(nomeColonna))
        {
            return $"COALESCE(r.{nomeColonna}, 0)";
        }

        if (numeroSconto == 1 && rowColumns.Contains("Sconto"))
        {
            return "COALESCE(r.Sconto, 0)";
        }

        if (numeroSconto == 1 && rowColumns.Contains("Scontopercentuale"))
        {
            return "COALESCE(r.Scontopercentuale, 0)";
        }

        return "0";
    }

    private async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        return await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
    }
}
