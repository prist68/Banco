using Banco.Vendita.Abstractions;
using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteLocalDocumentRepository : ILocalDocumentRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public SqliteLocalDocumentRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task<DocumentoLocale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                NumeroLocale,
                Stato,
                ModalitaChiusura,
                CategoriaDocumentoBanco,
                HasComponenteSospeso,
                StatoFiscaleBanco,
                Operatore,
                Cliente,
                ClienteOid,
                ListinoOid,
                ListinoNome,
                ScontoDocumento,
                DocumentoGestionaleOid,
                NumeroDocumentoGestionale,
                AnnoDocumentoGestionale,
                DataDocumentoGestionale,
                DataPagamentoFinale,
                DataComandoFiscaleFinale,
                DataCreazione,
                DataUltimaModifica
            FROM LocalDocuments
            WHERE Id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var documentId = Guid.Parse(reader.GetString(0));
        var stato = Enum.TryParse<StatoDocumentoLocale>(reader.GetString(2), out var parsedStato)
            ? parsedStato
            : StatoDocumentoLocale.BozzaLocale;
        var modalitaChiusura = Enum.TryParse<ModalitaChiusuraDocumento>(reader.GetString(3), out var parsedModalita)
            ? parsedModalita
            : ModalitaChiusuraDocumento.BozzaLocale;
        var categoriaDocumentoBanco = Enum.TryParse<CategoriaDocumentoBanco>(reader.GetString(4), out var parsedCategoria)
            ? parsedCategoria
            : CategoriaDocumentoBanco.Indeterminata;
        var hasComponenteSospeso = !reader.IsDBNull(5) && reader.GetInt32(5) == 1;
        var statoFiscaleBanco = Enum.TryParse<StatoFiscaleBanco>(reader.GetString(6), out var parsedStatoFiscale)
            ? parsedStatoFiscale
            : StatoFiscaleBanco.Nessuno;
        var operatore = reader.GetString(7);
        var cliente = reader.GetString(8);
        int? clienteOid = reader.IsDBNull(9) ? null : reader.GetInt32(9);
        int? listinoOid = reader.IsDBNull(10) ? null : reader.GetInt32(10);
        var listinoNome = reader.IsDBNull(11) ? null : reader.GetString(11);
        var scontoDocumento = Convert.ToDecimal(reader.GetDouble(12));
        int? documentoGestionaleOid = reader.IsDBNull(13) ? null : reader.GetInt32(13);
        long? numeroDocumentoGestionale = reader.IsDBNull(14) ? null : reader.GetInt64(14);
        int? annoDocumentoGestionale = reader.IsDBNull(15) ? null : reader.GetInt32(15);
        DateTime? dataDocumentoGestionale = reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16));
        DateTimeOffset? dataPagamentoFinale = reader.IsDBNull(17) ? null : DateTimeOffset.Parse(reader.GetString(17));
        DateTimeOffset? dataComandoFiscaleFinale = reader.IsDBNull(18) ? null : DateTimeOffset.Parse(reader.GetString(18));
        var dataCreazione = DateTimeOffset.Parse(reader.GetString(19));
        var dataUltimaModifica = DateTimeOffset.Parse(reader.GetString(20));

        await reader.CloseAsync();

        var righe = await LoadRowsAsync(connection, documentId, cancellationToken);
        var pagamenti = await LoadPaymentsAsync(connection, documentId, cancellationToken);

        return DocumentoLocale.Reidrata(
            documentId,
            stato,
            dataCreazione,
            dataUltimaModifica,
            operatore,
            cliente,
            clienteOid,
            listinoOid,
            listinoNome,
            string.Empty,
            modalitaChiusura,
            categoriaDocumentoBanco,
            hasComponenteSospeso,
            statoFiscaleBanco,
            scontoDocumento,
            documentoGestionaleOid,
            numeroDocumentoGestionale,
            annoDocumentoGestionale,
            dataDocumentoGestionale,
            dataPagamentoFinale,
            dataComandoFiscaleFinale,
            righe,
            pagamenti);
    }

    public async Task<DocumentoLocale?> GetByDocumentoGestionaleOidAsync(int documentoGestionaleOid, CancellationToken cancellationToken = default)
    {
        if (documentoGestionaleOid <= 0)
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM LocalDocuments WHERE DocumentoGestionaleOid = $documentoGestionaleOid LIMIT 1;";
        command.Parameters.AddWithValue("$documentoGestionaleOid", documentoGestionaleOid);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is not string rawId || !Guid.TryParse(rawId, out var documentId))
        {
            return null;
        }

        return await GetByIdAsync(documentId, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentoLocale>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentoLocale>();

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                NumeroLocale,
                Stato,
                ModalitaChiusura,
                CategoriaDocumentoBanco,
                HasComponenteSospeso,
                StatoFiscaleBanco,
                Operatore,
                Cliente,
                ClienteOid,
                ListinoOid,
                ListinoNome,
                ScontoDocumento,
                DocumentoGestionaleOid,
                NumeroDocumentoGestionale,
                AnnoDocumentoGestionale,
                DataDocumentoGestionale,
                DataPagamentoFinale,
                DataComandoFiscaleFinale,
                DataCreazione,
                DataUltimaModifica
            FROM LocalDocuments
            ORDER BY DataUltimaModifica DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(DocumentoLocale.Reidrata(
                Guid.Parse(reader.GetString(0)),
                Enum.TryParse<StatoDocumentoLocale>(reader.GetString(2), out var stato) ? stato : StatoDocumentoLocale.BozzaLocale,
                DateTimeOffset.Parse(reader.GetString(19)),
                DateTimeOffset.Parse(reader.GetString(20)),
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                string.Empty,
                Enum.TryParse<ModalitaChiusuraDocumento>(reader.GetString(3), out var modalitaChiusura)
                    ? modalitaChiusura
                    : ModalitaChiusuraDocumento.BozzaLocale,
                Enum.TryParse<CategoriaDocumentoBanco>(reader.GetString(4), out var categoriaDocumentoBanco)
                    ? categoriaDocumentoBanco
                    : CategoriaDocumentoBanco.Indeterminata,
                !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                Enum.TryParse<StatoFiscaleBanco>(reader.GetString(6), out var statoFiscaleBanco)
                    ? statoFiscaleBanco
                    : StatoFiscaleBanco.Nessuno,
                Convert.ToDecimal(reader.GetDouble(12)),
                reader.IsDBNull(13) ? null : reader.GetInt32(13),
                reader.IsDBNull(14) ? null : reader.GetInt64(14),
                reader.IsDBNull(15) ? null : reader.GetInt32(15),
                reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16)),
                reader.IsDBNull(17) ? null : DateTimeOffset.Parse(reader.GetString(17)),
                reader.IsDBNull(18) ? null : DateTimeOffset.Parse(reader.GetString(18)),
                [],
                []));
        }

        return results;
    }

    public async Task SaveAsync(DocumentoLocale documento, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO LocalDocuments (
                    Id, NumeroLocale, Stato, ModalitaChiusura, CategoriaDocumentoBanco, HasComponenteSospeso, StatoFiscaleBanco, Operatore, Cliente, ClienteOid, ListinoOid, ListinoNome, ScontoDocumento, TotaleDocumento,
                    DocumentoGestionaleOid, NumeroDocumentoGestionale, AnnoDocumentoGestionale, DataDocumentoGestionale,
                    DataPagamentoFinale, DataComandoFiscaleFinale,
                    DataCreazione, DataUltimaModifica)
                VALUES (
                    $id, $numeroLocale, $stato, $modalitaChiusura, $categoriaDocumentoBanco, $hasComponenteSospeso, $statoFiscaleBanco, $operatore, $cliente, $clienteOid, $listinoOid, $listinoNome, $scontoDocumento, $totaleDocumento,
                    $documentoGestionaleOid, $numeroDocumentoGestionale, $annoDocumentoGestionale, $dataDocumentoGestionale,
                    $dataPagamentoFinale, $dataComandoFiscaleFinale,
                    $dataCreazione, $dataUltimaModifica)
                ON CONFLICT(Id) DO UPDATE SET
                    NumeroLocale = excluded.NumeroLocale,
                    Stato = excluded.Stato,
                    ModalitaChiusura = excluded.ModalitaChiusura,
                    CategoriaDocumentoBanco = excluded.CategoriaDocumentoBanco,
                    HasComponenteSospeso = excluded.HasComponenteSospeso,
                    StatoFiscaleBanco = excluded.StatoFiscaleBanco,
                    Operatore = excluded.Operatore,
                    Cliente = excluded.Cliente,
                    ClienteOid = excluded.ClienteOid,
                    ListinoOid = excluded.ListinoOid,
                    ListinoNome = excluded.ListinoNome,
                    ScontoDocumento = excluded.ScontoDocumento,
                    TotaleDocumento = excluded.TotaleDocumento,
                    DocumentoGestionaleOid = excluded.DocumentoGestionaleOid,
                    NumeroDocumentoGestionale = excluded.NumeroDocumentoGestionale,
                    AnnoDocumentoGestionale = excluded.AnnoDocumentoGestionale,
                    DataDocumentoGestionale = excluded.DataDocumentoGestionale,
                    DataPagamentoFinale = excluded.DataPagamentoFinale,
                    DataComandoFiscaleFinale = excluded.DataComandoFiscaleFinale,
                    DataCreazione = excluded.DataCreazione,
                    DataUltimaModifica = excluded.DataUltimaModifica;
                """;
            command.Parameters.AddWithValue("$id", documento.Id.ToString("D"));
            command.Parameters.AddWithValue("$numeroLocale", string.Empty);
            command.Parameters.AddWithValue("$stato", documento.Stato.ToString());
            command.Parameters.AddWithValue("$modalitaChiusura", documento.ModalitaChiusura.ToString());
            command.Parameters.AddWithValue("$categoriaDocumentoBanco", documento.CategoriaDocumentoBanco.ToString());
            command.Parameters.AddWithValue("$hasComponenteSospeso", documento.HasComponenteSospeso ? 1 : 0);
            command.Parameters.AddWithValue("$statoFiscaleBanco", documento.StatoFiscaleBanco.ToString());
            command.Parameters.AddWithValue("$operatore", documento.Operatore);
            command.Parameters.AddWithValue("$cliente", documento.Cliente);
            command.Parameters.AddWithValue("$clienteOid", (object?)documento.ClienteOid ?? DBNull.Value);
            command.Parameters.AddWithValue("$listinoOid", (object?)documento.ListinoOid ?? DBNull.Value);
            command.Parameters.AddWithValue("$listinoNome", (object?)documento.ListinoNome ?? DBNull.Value);
            command.Parameters.AddWithValue("$scontoDocumento", documento.ScontoDocumento);
            command.Parameters.AddWithValue("$totaleDocumento", documento.TotaleDocumento);
            command.Parameters.AddWithValue("$documentoGestionaleOid", (object?)documento.DocumentoGestionaleOid ?? DBNull.Value);
            command.Parameters.AddWithValue("$numeroDocumentoGestionale", (object?)documento.NumeroDocumentoGestionale ?? DBNull.Value);
            command.Parameters.AddWithValue("$annoDocumentoGestionale", (object?)documento.AnnoDocumentoGestionale ?? DBNull.Value);
            command.Parameters.AddWithValue("$dataDocumentoGestionale", documento.DataDocumentoGestionale?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$dataPagamentoFinale", documento.DataPagamentoFinale?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$dataComandoFiscaleFinale", documento.DataComandoFiscaleFinale?.ToString("O") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$dataCreazione", documento.DataCreazione.ToString("O"));
            command.Parameters.AddWithValue("$dataUltimaModifica", documento.DataUltimaModifica.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await DeleteRowsAsync(connection, transaction, documento.Id, cancellationToken);
        await DeletePaymentsAsync(connection, transaction, documento.Id, cancellationToken);

        foreach (var riga in documento.Righe)
        {
            await using var rowCommand = connection.CreateCommand();
            rowCommand.Transaction = transaction;
            rowCommand.CommandText =
                """
                INSERT INTO LocalDocumentRows (
                    Id, DocumentId, OrdineRiga, TipoRiga, ArticoloOid, CodiceArticolo, Descrizione, Quantita, DisponibilitaRiferimento, PrezzoUnitario, ScontoPercentuale, Sconto1, Sconto2, Sconto3, Sconto4, IvaOid, AliquotaIva, FlagManuale, PromoCampaignOid, PromoRuleId, PromoEventId)
                VALUES (
                    $id, $documentId, $ordineRiga, $tipoRiga, $articoloOid, $codiceArticolo, $descrizione, $quantita, $disponibilitaRiferimento, $prezzoUnitario, $scontoPercentuale, $sconto1, $sconto2, $sconto3, $sconto4, $ivaOid, $aliquotaIva, $flagManuale, $promoCampaignOid, $promoRuleId, $promoEventId);
                """;
            rowCommand.Parameters.AddWithValue("$id", riga.Id.ToString("D"));
            rowCommand.Parameters.AddWithValue("$documentId", documento.Id.ToString("D"));
            rowCommand.Parameters.AddWithValue("$ordineRiga", riga.OrdineRiga);
            rowCommand.Parameters.AddWithValue("$tipoRiga", riga.TipoRiga.ToString());
            rowCommand.Parameters.AddWithValue("$articoloOid", (object?)riga.ArticoloOid ?? DBNull.Value);
            rowCommand.Parameters.AddWithValue("$codiceArticolo", (object?)riga.CodiceArticolo ?? DBNull.Value);
            rowCommand.Parameters.AddWithValue("$descrizione", riga.Descrizione);
            rowCommand.Parameters.AddWithValue("$quantita", riga.Quantita);
            rowCommand.Parameters.AddWithValue("$disponibilitaRiferimento", riga.DisponibilitaRiferimento);
            rowCommand.Parameters.AddWithValue("$prezzoUnitario", riga.PrezzoUnitario);
            rowCommand.Parameters.AddWithValue("$scontoPercentuale", riga.ScontoPercentuale);
            rowCommand.Parameters.AddWithValue("$sconto1", riga.Sconto1);
            rowCommand.Parameters.AddWithValue("$sconto2", riga.Sconto2);
            rowCommand.Parameters.AddWithValue("$sconto3", riga.Sconto3);
            rowCommand.Parameters.AddWithValue("$sconto4", riga.Sconto4);
            rowCommand.Parameters.AddWithValue("$ivaOid", riga.IvaOid);
            rowCommand.Parameters.AddWithValue("$aliquotaIva", riga.AliquotaIva);
            rowCommand.Parameters.AddWithValue("$flagManuale", riga.FlagManuale ? 1 : 0);
            rowCommand.Parameters.AddWithValue("$promoCampaignOid", (object?)riga.PromoCampaignOid ?? DBNull.Value);
            rowCommand.Parameters.AddWithValue("$promoRuleId", (object?)riga.PromoRuleId ?? DBNull.Value);
            rowCommand.Parameters.AddWithValue("$promoEventId", (object?)riga.PromoEventId ?? DBNull.Value);
            await rowCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var pagamento in documento.Pagamenti)
        {
            await using var paymentCommand = connection.CreateCommand();
            paymentCommand.Transaction = transaction;
            paymentCommand.CommandText =
                """
                INSERT INTO LocalPayments (Id, DocumentId, TipoPagamento, Importo, StatoPagamentoLocale, DataOra)
                VALUES ($id, $documentId, $tipoPagamento, $importo, $statoPagamentoLocale, $dataOra);
                """;
            paymentCommand.Parameters.AddWithValue("$id", pagamento.Id.ToString("D"));
            paymentCommand.Parameters.AddWithValue("$documentId", documento.Id.ToString("D"));
            paymentCommand.Parameters.AddWithValue("$tipoPagamento", pagamento.TipoPagamento);
            paymentCommand.Parameters.AddWithValue("$importo", pagamento.Importo);
            paymentCommand.Parameters.AddWithValue("$statoPagamentoLocale", pagamento.StatoPagamentoLocale);
            paymentCommand.Parameters.AddWithValue("$dataOra", pagamento.DataOra.ToString("O"));
            await paymentCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await DeleteRowsAsync(connection, transaction, id, cancellationToken);
        await DeletePaymentsAsync(connection, transaction, id, cancellationToken);

        await using var documentCommand = connection.CreateCommand();
        documentCommand.Transaction = transaction;
        documentCommand.CommandText = "DELETE FROM LocalDocuments WHERE Id = $id;";
        documentCommand.Parameters.AddWithValue("$id", id.ToString("D"));
        await documentCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        Directory.CreateDirectory(settings.LocalStore.BaseDirectory);
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }

    private static async Task<IReadOnlyList<RigaDocumentoLocale>> LoadRowsAsync(
        SqliteConnection connection,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var righe = new List<RigaDocumentoLocale>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, OrdineRiga, TipoRiga, ArticoloOid, CodiceArticolo, Descrizione, Quantita, DisponibilitaRiferimento, PrezzoUnitario, ScontoPercentuale, Sconto1, Sconto2, Sconto3, Sconto4, IvaOid, AliquotaIva, FlagManuale, PromoCampaignOid, PromoRuleId, PromoEventId
            FROM LocalDocumentRows
            WHERE DocumentId = $documentId
            ORDER BY OrdineRiga;
            """;
        command.Parameters.AddWithValue("$documentId", documentId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var scontoPercentualeLegacy = Convert.ToDecimal(reader.GetDouble(9));
            var sconto1 = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader.GetDouble(10));
            var sconto2 = reader.IsDBNull(11) ? 0 : Convert.ToDecimal(reader.GetDouble(11));
            var sconto3 = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader.GetDouble(12));
            var sconto4 = reader.IsDBNull(13) ? 0 : Convert.ToDecimal(reader.GetDouble(13));

            if (sconto1 == 0 && sconto2 == 0 && sconto3 == 0 && sconto4 == 0 && scontoPercentualeLegacy > 0)
            {
                sconto1 = scontoPercentualeLegacy;
            }

            righe.Add(new RigaDocumentoLocale
            {
                Id = Guid.Parse(reader.GetString(0)),
                OrdineRiga = reader.GetInt32(1),
                TipoRiga = Enum.TryParse<TipoRigaDocumento>(reader.GetString(2), out var tipoRiga)
                    ? tipoRiga
                    : TipoRigaDocumento.Articolo,
                ArticoloOid = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                CodiceArticolo = reader.IsDBNull(4) ? null : reader.GetString(4),
                Descrizione = reader.GetString(5),
                Quantita = Convert.ToDecimal(reader.GetDouble(6)),
                DisponibilitaRiferimento = Convert.ToDecimal(reader.GetDouble(7)),
                PrezzoUnitario = Convert.ToDecimal(reader.GetDouble(8)),
                Sconto1 = sconto1,
                Sconto2 = sconto2,
                Sconto3 = sconto3,
                Sconto4 = sconto4,
                IvaOid = reader.GetInt32(14),
                AliquotaIva = Convert.ToDecimal(reader.GetDouble(15)),
                FlagManuale = reader.GetInt32(16) == 1,
                PromoCampaignOid = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                PromoRuleId = reader.IsDBNull(18) ? null : reader.GetString(18),
                PromoEventId = reader.IsDBNull(19) ? null : reader.GetString(19)
            });
        }

        return righe;
    }

    private static async Task<IReadOnlyList<PagamentoLocale>> LoadPaymentsAsync(
        SqliteConnection connection,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var pagamenti = new List<PagamentoLocale>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TipoPagamento, Importo, StatoPagamentoLocale, DataOra
            FROM LocalPayments
            WHERE DocumentId = $documentId
            ORDER BY DataOra;
            """;
        command.Parameters.AddWithValue("$documentId", documentId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            pagamenti.Add(new PagamentoLocale
            {
                Id = Guid.Parse(reader.GetString(0)),
                TipoPagamento = reader.GetString(1),
                Importo = Convert.ToDecimal(reader.GetDouble(2)),
                StatoPagamentoLocale = reader.GetString(3),
                DataOra = DateTimeOffset.Parse(reader.GetString(4))
            });
        }

        return pagamenti;
    }

    private static async Task DeleteRowsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM LocalDocumentRows WHERE DocumentId = $documentId;";
        command.Parameters.AddWithValue("$documentId", documentId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeletePaymentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM LocalPayments WHERE DocumentId = $documentId;";
        command.Parameters.AddWithValue("$documentId", documentId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

}
