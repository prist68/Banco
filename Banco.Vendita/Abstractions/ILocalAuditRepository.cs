using Banco.Core.Domain.Entities;

namespace Banco.Vendita.Abstractions;

public interface ILocalAuditRepository
{
    Task SaveAsync(EventoAudit evento, CancellationToken cancellationToken = default);
}
