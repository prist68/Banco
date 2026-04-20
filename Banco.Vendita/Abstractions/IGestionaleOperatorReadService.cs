using Banco.Vendita.Operators;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleOperatorReadService
{
    Task<IReadOnlyList<GestionaleOperatorSummary>> GetOperatorsAsync(
        CancellationToken cancellationToken = default);
}
