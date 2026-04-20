namespace Banco.Riordino;

public interface IReorderListRepository
{
    event Action? CurrentListChanged;

    Task<ReorderListSnapshot> GetCurrentListAsync(CancellationToken cancellationToken = default);

    Task AddOrIncrementItemAsync(ReorderListItem item, CancellationToken cancellationToken = default);

    Task SetItemOrderedAsync(Guid itemId, bool isOrdered, CancellationToken cancellationToken = default);

    Task UpdateSelectedSupplierAsync(
        Guid itemId,
        int? supplierOid,
        string supplierName,
        CancellationToken cancellationToken = default);

    Task UpdateQuantityToOrderAsync(
        Guid itemId,
        decimal quantityToOrder,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReorderSupplierDraftState>> GetOrCreateSupplierDraftStatesAsync(
        Guid listId,
        IReadOnlyList<string> supplierNames,
        CancellationToken cancellationToken = default);

    Task SetSupplierDraftStatusAsync(
        Guid listId,
        string supplierName,
        ReorderSupplierDraftStatus status,
        CancellationToken cancellationToken = default);

    Task SetSupplierDraftFmDocumentAsync(
        Guid listId,
        string supplierName,
        int documentoOid,
        long numeroDocumento,
        int annoDocumento,
        CancellationToken cancellationToken = default);

    Task RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task RemoveSupplierDraftAsync(
        Guid listId,
        string supplierName,
        CancellationToken cancellationToken = default);
}
