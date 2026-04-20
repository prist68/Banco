using Banco.Vendita.Pos;

namespace Banco.Vendita.Abstractions;

public interface IPosPaymentService
{
    Task<PosPaymentResult> ExecutePaymentAsync(decimal amount, CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
