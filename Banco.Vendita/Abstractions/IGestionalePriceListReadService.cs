using Banco.Vendita.PriceLists;

namespace Banco.Vendita.Abstractions;

public interface IGestionalePriceListReadService
{
    Task<IReadOnlyList<GestionalePriceListSummary>> GetSalesPriceListsAsync(
        CancellationToken cancellationToken = default);

    Task<GestionalePriceListSummary?> GetPriceListByOidAsync(
        int priceListOid,
        CancellationToken cancellationToken = default);
}
