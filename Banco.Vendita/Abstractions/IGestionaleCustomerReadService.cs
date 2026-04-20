using Banco.Vendita.Customers;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleCustomerReadService
{
    Task<IReadOnlyList<GestionaleCustomerSummary>> SearchCustomersAsync(
        string searchText,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GestionaleCustomerSummary>> SearchSuppliersAsync(
        string searchText,
        int maxResults = 20,
        CancellationToken cancellationToken = default);

    Task<GestionaleCustomerSummary?> GetCustomerByOidAsync(
        int customerOid,
        CancellationToken cancellationToken = default);
}
