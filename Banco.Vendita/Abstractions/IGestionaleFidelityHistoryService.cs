using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleFidelityHistoryService
{
    Task<FidelityCustomerHistory?> GetCustomerHistoryAsync(
        int customerOid,
        CancellationToken cancellationToken = default);

    Task<FidelityBalanceRecalculationResult?> RecalculateCustomerBalanceAsync(
        int customerOid,
        bool persistToLegacy = true,
        string operatore = "Sistema",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FidelityBalanceRecalculationResult>> RecalculateAllActiveCustomersAsync(
        bool persistToLegacy = true,
        string operatore = "Sistema",
        CancellationToken cancellationToken = default);
}
