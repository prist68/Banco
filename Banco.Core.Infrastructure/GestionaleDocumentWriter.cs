using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Fiscal;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleDocumentWriter : IGestionaleDocumentWriter
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleDocumentWriter(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<FiscalizationResult> UpsertFiscalDocumentAsync(
        FiscalizationRequest request,
        int? documentoGestionaleOid = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);

        await using var connection = await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var modelDefaults = await LoadModelDefaultsAsync(connection, transaction, request.ModellodocumentoOid, cancellationToken);
            var aliquote = await LoadAliquoteAsync(connection, transaction, request.Righe, cancellationToken);
            var totals = BuildTotals(request.Righe, aliquote);
            var effectiveDocumentOid = documentoGestionaleOid.HasValue
                ? await UpdateDocumentoAsync(connection, transaction, documentoGestionaleOid.Value, request, modelDefaults, totals, cancellationToken)
                : await InsertDocumentoAsync(connection, transaction, request, modelDefaults, totals, cancellationToken);
            await DeleteDocumentoChildrenAsync(connection, transaction, effectiveDocumentOid, cancellationToken);
            await InsertRigheAsync(connection, transaction, effectiveDocumentOid, request, modelDefaults.CausaleMagazzinoOid, modelDefaults.MagazzinoOid, cancellationToken);
            await InsertDocumentoIvaAsync(connection, transaction, effectiveDocumentOid, totals.IvaRows, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new FiscalizationResult
            {
                DocumentoGestionaleOid = effectiveDocumentOid,
                NumeroDocumentoGestionale = request.Numero,
                AnnoDocumentoGestionale = request.Anno,
                DataDocumentoGestionale = request.DataDocumento,
                Message = documentoGestionaleOid.HasValue
                    ? $"Documento gestionale {request.Numero}/{request.Anno} aggiornato sullo stesso OID {effectiveDocumentOid}."
                    : $"Documento gestionale {request.Numero}/{request.Anno} creato con OID {effectiveDocumentOid}."
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<int> UpdateDocumentoAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentoOid,
        FiscalizationRequest request,
        (int SoggettoOid, int PagamentoOid, int MagazzinoOid, int CausaleMagazzinoOid) modelDefaults,
        (decimal TotaleDocumento, decimal TotaleImponibile, decimal TotaleIva, IReadOnlyList<(int IvaOid, decimal Imponibile, decimal Imposta)> IvaRows) totals,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE documento
            SET
                Data = @Data,
                Modellodocumento = @Modellodocumento,
                Soggetto = @Soggetto,
                Listino = @Listino,
                Totaledocumento = @Totaledocumento,
                Totaleimponibile = @Totaleimponibile,
                Totaleiva = @Totaleiva,
                Pagato = @Pagato,
                Pagatocartacredito = @Pagatocartacredito,
                Pagatoweb = @Pagatoweb,
                Pagatobuonipasto = @Pagatobuonipasto,
                Pagatosospeso = @Pagatosospeso,
                Utente = @Utente,
                Magazzino = @Magazzino,
                Pagamento = @Pagamento,
                Dataaggiornamento = @Dataaggiornamento
            WHERE OID = @DocumentoOid;
            """;
        command.Parameters.AddWithValue("@DocumentoOid", documentoOid);
        command.Parameters.AddWithValue("@Data", request.DataDocumento);
        command.Parameters.AddWithValue("@Modellodocumento", request.ModellodocumentoOid);
        command.Parameters.AddWithValue("@Soggetto", request.SoggettoOid > 0 ? request.SoggettoOid : modelDefaults.SoggettoOid);
        command.Parameters.AddWithValue("@Listino", (object?)request.ListinoOid ?? DBNull.Value);
        command.Parameters.AddWithValue("@Totaledocumento", totals.TotaleDocumento);
        command.Parameters.AddWithValue("@Totaleimponibile", totals.TotaleImponibile);
        command.Parameters.AddWithValue("@Totaleiva", totals.TotaleIva);
        command.Parameters.AddWithValue("@Pagato", request.Pagamenti.Contanti);
        command.Parameters.AddWithValue("@Pagatocartacredito", request.Pagamenti.Carta);
        command.Parameters.AddWithValue("@Pagatoweb", request.Pagamenti.Web);
        command.Parameters.AddWithValue("@Pagatobuonipasto", request.Pagamenti.Buoni);
        command.Parameters.AddWithValue("@Pagatosospeso", request.Pagamenti.Sospeso);
        command.Parameters.AddWithValue("@Utente", request.Operatore);
        command.Parameters.AddWithValue("@Magazzino", request.MagazzinoOid > 0 ? request.MagazzinoOid : modelDefaults.MagazzinoOid);
        command.Parameters.AddWithValue("@Pagamento", request.PagamentoOid > 0 ? request.PagamentoOid : modelDefaults.PagamentoOid);
        command.Parameters.AddWithValue("@Dataaggiornamento", DateTime.Now);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return documentoOid;
    }

    private static async Task<Dictionary<int, decimal>> LoadAliquoteAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyList<FiscalizationRow> rows,
        CancellationToken cancellationToken)
    {
        var ivaOids = rows
            .Select(row => row.IvaOid)
            .Where(oid => oid > 0)
            .Distinct()
            .ToArray();

        var result = new Dictionary<int, decimal>();
        if (ivaOids.Length == 0)
        {
            return result;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var parameterNames = new List<string>(ivaOids.Length);
        for (var index = 0; index < ivaOids.Length; index++)
        {
            var parameterName = $"@iva{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, ivaOids[index]);
        }

        command.CommandText =
            $"SELECT OID, COALESCE(Percentualeiva, 0) FROM iva WHERE OID IN ({string.Join(", ", parameterNames)});";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetInt32(0)] = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
        }

        return result;
    }

    private static (decimal TotaleDocumento, decimal TotaleImponibile, decimal TotaleIva, IReadOnlyList<(int IvaOid, decimal Imponibile, decimal Imposta)> IvaRows) BuildTotals(
        IReadOnlyList<FiscalizationRow> rows,
        IReadOnlyDictionary<int, decimal> aliquote)
    {
        var ivaGroups = new Dictionary<int, (decimal Imponibile, decimal Imposta)>();
        decimal totaleDocumento = 0;
        decimal totaleImponibile = 0;
        decimal totaleIva = 0;

        foreach (var row in rows)
        {
            var importoRiga = Math.Round(row.ImportoRiga, 2, MidpointRounding.AwayFromZero);
            var aliquota = aliquote.TryGetValue(row.IvaOid, out var fetchedAliquota) ? fetchedAliquota : 0;
            var imponibile = aliquota <= 0
                ? importoRiga
                : Math.Round(importoRiga / (1 + (aliquota / 100m)), 2, MidpointRounding.AwayFromZero);
            var imposta = Math.Round(importoRiga - imponibile, 2, MidpointRounding.AwayFromZero);

            totaleDocumento += importoRiga;
            totaleImponibile += imponibile;
            totaleIva += imposta;

            if (!ivaGroups.TryGetValue(row.IvaOid, out var existing))
            {
                ivaGroups[row.IvaOid] = (imponibile, imposta);
                continue;
            }

            ivaGroups[row.IvaOid] = (existing.Imponibile + imponibile, existing.Imposta + imposta);
        }

        return (
            Math.Round(totaleDocumento, 2, MidpointRounding.AwayFromZero),
            Math.Round(totaleImponibile, 2, MidpointRounding.AwayFromZero),
            Math.Round(totaleIva, 2, MidpointRounding.AwayFromZero),
            ivaGroups
                .OrderBy(item => item.Key)
                .Select(item => (
                    item.Key,
                    Math.Round(item.Value.Imponibile, 2, MidpointRounding.AwayFromZero),
                    Math.Round(item.Value.Imposta, 2, MidpointRounding.AwayFromZero)))
                .ToList());
    }

    private static async Task<(int SoggettoOid, int PagamentoOid, int MagazzinoOid, int CausaleMagazzinoOid)> LoadModelDefaultsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int modelloDocumentoOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                COALESCE(Soggetto, 1) AS Soggetto,
                COALESCE(Pagamento, 10) AS Pagamento,
                COALESCE(Magazzino, 1) AS Magazzino,
                COALESCE(Causalemagazzino, 21) AS Causalemagazzino
            FROM modellodocumento
            WHERE OID = @Modellodocumento
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@Modellodocumento", modelloDocumentoOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (1, 10, 1, 21);
        }

        return (
            reader.IsDBNull(0) ? 1 : reader.GetInt32(0),
            reader.IsDBNull(1) ? 10 : reader.GetInt32(1),
            reader.IsDBNull(2) ? 1 : reader.GetInt32(2),
            reader.IsDBNull(3) ? 21 : reader.GetInt32(3));
    }

    private static async Task<int> InsertDocumentoAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        FiscalizationRequest request,
        (int SoggettoOid, int PagamentoOid, int MagazzinoOid, int CausaleMagazzinoOid) modelDefaults,
        (decimal TotaleDocumento, decimal TotaleImponibile, decimal TotaleIva, IReadOnlyList<(int IvaOid, decimal Imponibile, decimal Imposta)> IvaRows) totals,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO documento (
                Numero,
                Anno,
                Data,
                Modellodocumento,
                Soggetto,
                Listino,
                Totaledocumento,
                Totaleimponibile,
                Totaleiva,
                Pagato,
                Pagatocartacredito,
                Pagatoweb,
                Pagatobuonipasto,
                Pagatosospeso,
                Utente,
                Magazzino,
                Pagamento,
                OptimisticLockField,
                Datacreazione,
                Dataaggiornamento
            )
            VALUES (
                @Numero,
                @Anno,
                @Data,
                @Modellodocumento,
                @Soggetto,
                @Listino,
                @Totaledocumento,
                @Totaleimponibile,
                @Totaleiva,
                @Pagato,
                @Pagatocartacredito,
                @Pagatoweb,
                @Pagatobuonipasto,
                @Pagatosospeso,
                @Utente,
                @Magazzino,
                @Pagamento,
                0,
                @Datacreazione,
                @Dataaggiornamento
            );
            """;

        command.Parameters.AddWithValue("@Numero", request.Numero);
        command.Parameters.AddWithValue("@Anno", request.Anno);
        command.Parameters.AddWithValue("@Data", request.DataDocumento);
        command.Parameters.AddWithValue("@Modellodocumento", request.ModellodocumentoOid);
        command.Parameters.AddWithValue("@Soggetto", request.SoggettoOid > 0 ? request.SoggettoOid : modelDefaults.SoggettoOid);
        command.Parameters.AddWithValue("@Listino", (object?)request.ListinoOid ?? DBNull.Value);
        command.Parameters.AddWithValue("@Totaledocumento", totals.TotaleDocumento);
        command.Parameters.AddWithValue("@Totaleimponibile", totals.TotaleImponibile);
        command.Parameters.AddWithValue("@Totaleiva", totals.TotaleIva);
        command.Parameters.AddWithValue("@Pagato", request.Pagamenti.Contanti);
        command.Parameters.AddWithValue("@Pagatocartacredito", request.Pagamenti.Carta);
        command.Parameters.AddWithValue("@Pagatoweb", request.Pagamenti.Web);
        command.Parameters.AddWithValue("@Pagatobuonipasto", request.Pagamenti.Buoni);
        command.Parameters.AddWithValue("@Pagatosospeso", request.Pagamenti.Sospeso);
        command.Parameters.AddWithValue("@Utente", request.Operatore);
        command.Parameters.AddWithValue("@Magazzino", request.MagazzinoOid > 0 ? request.MagazzinoOid : modelDefaults.MagazzinoOid);
        command.Parameters.AddWithValue("@Pagamento", request.PagamentoOid > 0 ? request.PagamentoOid : modelDefaults.PagamentoOid);
        command.Parameters.AddWithValue("@Datacreazione", request.DataDocumento);
        command.Parameters.AddWithValue("@Dataaggiornamento", request.DataDocumento);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return Convert.ToInt32(command.LastInsertedId);
    }

    private static async Task InsertRigheAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentoOid,
        FiscalizationRequest request,
        int causaleMagazzinoOid,
        int magazzinoOid,
        CancellationToken cancellationToken)
    {
        foreach (var row in request.Righe.OrderBy(item => item.OrdineRiga))
        {
            var unitaMisuraOid = await ResolveUnitaMisuraOidAsync(connection, transaction, row.UnitaMisura, cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO documentoriga (
                    Documento,
                    Ordineriga,
                    Datadocumento,
                    Articolo,
                    Codicearticolo,
                    Descrizione,
                    Quantita,
                    Valoreunitario,
                    Valorebase,
                    Valoreivato,
                    Importoriga,
                    Iva,
                    Magazzino,
                    Causalemagazzino,
                    Unitadimisura,
                    Codiceabarre,
                    Sconto1,
                    Sconto2,
                    Sconto3,
                    Sconto4,
                    OptimisticLockField
                )
                VALUES (
                    @Documento,
                    @Ordineriga,
                    @Datadocumento,
                    @Articolo,
                    @Codicearticolo,
                    @Descrizione,
                    @Quantita,
                    @Valoreunitario,
                    @Valorebase,
                    @Valoreivato,
                    @Importoriga,
                    @Iva,
                    @Magazzino,
                    @Causalemagazzino,
                    @Unitadimisura,
                    @Codiceabarre,
                    @Sconto1,
                    @Sconto2,
                    @Sconto3,
                    @Sconto4,
                    0
                );
                """;

            command.Parameters.AddWithValue("@Documento", documentoOid);
            command.Parameters.AddWithValue("@Ordineriga", row.OrdineRiga * 5);
            command.Parameters.AddWithValue("@Datadocumento", request.DataDocumento);
            command.Parameters.AddWithValue("@Articolo", (object?)row.ArticoloOid ?? DBNull.Value);
            command.Parameters.AddWithValue("@Codicearticolo", (object?)row.CodiceArticolo ?? DBNull.Value);
            command.Parameters.AddWithValue("@Descrizione", row.Descrizione);
            command.Parameters.AddWithValue("@Quantita", row.Quantita);
            command.Parameters.AddWithValue("@Valoreunitario", row.ValoreUnitario);
            command.Parameters.AddWithValue("@Valorebase", row.ValoreUnitario);
            command.Parameters.AddWithValue("@Valoreivato", true);
            command.Parameters.AddWithValue("@Importoriga", row.ImportoRiga);
            command.Parameters.AddWithValue("@Iva", row.IvaOid);
            command.Parameters.AddWithValue("@Magazzino", request.MagazzinoOid > 0 ? request.MagazzinoOid : magazzinoOid);
            command.Parameters.AddWithValue("@Causalemagazzino", request.CausaleMagazzinoOid > 0 ? request.CausaleMagazzinoOid : causaleMagazzinoOid);
            command.Parameters.AddWithValue("@Unitadimisura", unitaMisuraOid > 0 ? unitaMisuraOid : DBNull.Value);
            command.Parameters.AddWithValue("@Codiceabarre", string.IsNullOrWhiteSpace(row.BarcodeArticolo) ? DBNull.Value : row.BarcodeArticolo);
            command.Parameters.AddWithValue("@Sconto1", row.Sconto1);
            command.Parameters.AddWithValue("@Sconto2", row.Sconto2);
            command.Parameters.AddWithValue("@Sconto3", row.Sconto3);
            command.Parameters.AddWithValue("@Sconto4", row.Sconto4);
            await command.ExecuteNonQueryAsync(cancellationToken);

            var insertedRowOid = Convert.ToInt32(command.LastInsertedId);
            await InsertRigaVariantiAsync(connection, transaction, insertedRowOid, row, cancellationToken);
        }
    }

    private static async Task InsertRigaVariantiAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentorigaOid,
        FiscalizationRow row,
        CancellationToken cancellationToken)
    {
        if (!row.VarianteDettaglioOid1.HasValue && !row.VarianteDettaglioOid2.HasValue && string.IsNullOrWhiteSpace(row.BarcodeArticolo))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO documentorigacombinazionevarianti (
                Documentoriga,
                Variantedettaglio1,
                Variantedettaglio2,
                Documentorigacombinazionevariantiordine,
                Codiceabarre,
                Quantita,
                OptimisticLockField,
                Seleziona
            )
            VALUES (
                @Documentoriga,
                @Variantedettaglio1,
                @Variantedettaglio2,
                @Documentorigacombinazionevariantiordine,
                @Codiceabarre,
                @Quantita,
                0,
                @Seleziona
            );
            """;

        command.Parameters.AddWithValue("@Documentoriga", documentorigaOid);
        command.Parameters.AddWithValue("@Variantedettaglio1", row.VarianteDettaglioOid1.HasValue ? row.VarianteDettaglioOid1.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Variantedettaglio2", row.VarianteDettaglioOid2.HasValue ? row.VarianteDettaglioOid2.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Documentorigacombinazionevariantiordine", DBNull.Value);
        command.Parameters.AddWithValue("@Codiceabarre", string.IsNullOrWhiteSpace(row.BarcodeArticolo) ? DBNull.Value : row.BarcodeArticolo);
        command.Parameters.AddWithValue("@Quantita", row.Quantita);
        command.Parameters.AddWithValue("@Seleziona", false);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int?> ResolveUnitaMisuraOidAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string? unitaMisura,
        CancellationToken cancellationToken)
    {
        var codice = string.IsNullOrWhiteSpace(unitaMisura) ? "PZ" : unitaMisura.Trim().ToUpperInvariant();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT OID
            FROM unitadimisura
            WHERE UPPER(TRIM(Unitadimisura)) = @codice
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@codice", codice);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? null : Convert.ToInt32(result);
    }

    private static async Task DeleteDocumentoChildrenAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentoOid,
        CancellationToken cancellationToken)
    {
        await using (var deleteCombinazioniVarianti = connection.CreateCommand())
        {
            deleteCombinazioniVarianti.Transaction = transaction;
            deleteCombinazioniVarianti.CommandText =
                """
                DELETE comb
                FROM documentorigacombinazionevarianti comb
                INNER JOIN documentoriga riga ON riga.OID = comb.Documentoriga
                WHERE riga.Documento = @Documento;
                """;
            deleteCombinazioniVarianti.Parameters.AddWithValue("@Documento", documentoOid);
            await deleteCombinazioniVarianti.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteRighe = connection.CreateCommand())
        {
            deleteRighe.Transaction = transaction;
            deleteRighe.CommandText = "DELETE FROM documentoriga WHERE Documento = @Documento;";
            deleteRighe.Parameters.AddWithValue("@Documento", documentoOid);
            await deleteRighe.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteIva = connection.CreateCommand())
        {
            deleteIva.Transaction = transaction;
            deleteIva.CommandText = "DELETE FROM documentoiva WHERE Documento = @Documento;";
            deleteIva.Parameters.AddWithValue("@Documento", documentoOid);
            await deleteIva.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertDocumentoIvaAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentoOid,
        IReadOnlyList<(int IvaOid, decimal Imponibile, decimal Imposta)> ivaRows,
        CancellationToken cancellationToken)
    {
        foreach (var ivaRow in ivaRows)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO documentoiva (
                    Documento,
                    Iva,
                    Imponibile,
                    Imposta,
                    Spese,
                    OptimisticLockField
                )
                VALUES (
                    @Documento,
                    @Iva,
                    @Imponibile,
                    @Imposta,
                    0,
                    0
                );
                """;

            command.Parameters.AddWithValue("@Documento", documentoOid);
            command.Parameters.AddWithValue("@Iva", ivaRow.IvaOid);
            command.Parameters.AddWithValue("@Imponibile", ivaRow.Imponibile);
            command.Parameters.AddWithValue("@Imposta", ivaRow.Imposta);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
