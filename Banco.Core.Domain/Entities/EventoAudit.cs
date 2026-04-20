namespace Banco.Core.Domain.Entities;

public sealed class EventoAudit
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string EntityType { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string Operatore { get; init; } = string.Empty;

    public string PayloadSinteticoJson { get; init; } = "{}";

    public string Esito { get; init; } = "Ok";
}
