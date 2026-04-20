using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Banco.Vendita.Fiscal;

namespace Banco.Vendita.Abstractions;

public interface IBancoDocumentWorkflowService
{
    Task<FiscalizationResult> PublishAsync(
        DocumentoLocale documento,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        CancellationToken cancellationToken = default);
}
