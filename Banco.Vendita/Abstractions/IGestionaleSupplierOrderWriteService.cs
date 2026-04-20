using Banco.Vendita.Documents;
using Banco.Vendita.Fiscal;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleSupplierOrderWriteService
{
    Task<FiscalizationResult> CreateSupplierOrderAsync(
        GestionaleSupplierOrderRequest request,
        CancellationToken cancellationToken = default);
}
