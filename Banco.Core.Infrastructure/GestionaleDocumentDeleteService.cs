using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using MySqlConnector;

namespace Banco.Core.Infrastructure;

public sealed class GestionaleDocumentDeleteService : IGestionaleDocumentDeleteService
{
    private readonly IApplicationConfigurationService _configurationService;

    public GestionaleDocumentDeleteService(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task DeleteNonFiscalizedDocumentAsync(
        int documentoGestionaleOid,
        CancellationToken cancellationToken = default)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);

        await using var connection = await GestionaleConnectionFactory.CreateOpenConnectionAsync(settings, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await ValidateDocumentoAsync(connection, transaction, documentoGestionaleOid, cancellationToken);
            await DeleteDocumentoChildrenAsync(connection, transaction, documentoGestionaleOid, cancellationToken);

            await using var deleteDocumento = connection.CreateCommand();
            deleteDocumento.Transaction = transaction;
            deleteDocumento.CommandText = "DELETE FROM documento WHERE OID = @DocumentoOid;";
            deleteDocumento.Parameters.AddWithValue("@DocumentoOid", documentoGestionaleOid);
            var affectedRows = await deleteDocumento.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows <= 0)
            {
                throw new InvalidOperationException($"Il documento legacy {documentoGestionaleOid} non e` stato trovato in fase di cancellazione.");
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task ValidateDocumentoAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentoGestionaleOid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT
                d.Modellodocumento,
                COALESCE(d.Pagato, 0) AS PagatoContanti,
                COALESCE(d.Pagatocartacredito, 0) AS PagatoCarta,
                COALESCE(d.Pagatoweb, 0) AS PagatoWeb
            FROM documento d
            WHERE d.OID = @DocumentoOid
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@DocumentoOid", documentoGestionaleOid);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Il documento legacy {documentoGestionaleOid} non esiste su db_diltech.");
        }

        var modelloDocumento = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);

        if (modelloDocumento != 27)
        {
            throw new InvalidOperationException($"Il documento legacy {documentoGestionaleOid} non appartiene al modello Banco.");
        }
    }

    private static async Task DeleteDocumentoChildrenAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentoOid,
        CancellationToken cancellationToken)
    {
        await using (var deleteVarianti = connection.CreateCommand())
        {
            deleteVarianti.Transaction = transaction;
            deleteVarianti.CommandText =
                """
                DELETE drcv
                FROM documentorigacombinazionevarianti drcv
                INNER JOIN documentoriga dr ON dr.OID = drcv.Documentoriga
                WHERE dr.Documento = @Documento;
                """;
            deleteVarianti.Parameters.AddWithValue("@Documento", documentoOid);
            await deleteVarianti.ExecuteNonQueryAsync(cancellationToken);
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
}
