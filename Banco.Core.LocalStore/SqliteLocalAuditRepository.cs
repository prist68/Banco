using Banco.Core.Domain.Entities;
using Banco.Vendita.Abstractions;
using Microsoft.Data.Sqlite;

namespace Banco.Core.LocalStore;

public sealed class SqliteLocalAuditRepository : ILocalAuditRepository
{
    private readonly IApplicationConfigurationService _configurationService;

    public SqliteLocalAuditRepository(IApplicationConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public async Task SaveAsync(EventoAudit evento, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO AuditEvents (
                Id,
                DocumentId,
                EntityType,
                EntityId,
                EventType,
                Operatore,
                PayloadSinteticoJson,
                Esito,
                Timestamp)
            VALUES (
                $id,
                $documentId,
                $entityType,
                $entityId,
                $eventType,
                $operatore,
                $payloadSinteticoJson,
                $esito,
                $timestamp);
            """;

        command.Parameters.AddWithValue("$id", evento.Id.ToString("D"));
        command.Parameters.AddWithValue("$documentId", DBNull.Value);
        command.Parameters.AddWithValue("$entityType", evento.EntityType);
        command.Parameters.AddWithValue("$entityId", evento.EntityId);
        command.Parameters.AddWithValue("$eventType", evento.EventType);
        command.Parameters.AddWithValue("$operatore", evento.Operatore);
        command.Parameters.AddWithValue("$payloadSinteticoJson", evento.PayloadSinteticoJson);
        command.Parameters.AddWithValue("$esito", evento.Esito);
        command.Parameters.AddWithValue("$timestamp", evento.Timestamp.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await _configurationService.LoadAsync(cancellationToken);
        Directory.CreateDirectory(settings.LocalStore.BaseDirectory);
        return new SqliteConnection($"Data Source={settings.LocalStore.DatabasePath}");
    }
}
