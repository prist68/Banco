using Banco.Core.Domain.Entities;

namespace Banco.Vendita.Abstractions;

public interface ILocalDocumentRepository
{
    Task<DocumentoLocale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DocumentoLocale?> GetByDocumentoGestionaleOidAsync(int documentoGestionaleOid, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoLocale>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DocumentoLocale documento, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
