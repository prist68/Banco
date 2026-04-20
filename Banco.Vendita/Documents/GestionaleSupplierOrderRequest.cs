namespace Banco.Vendita.Documents;

public sealed class GestionaleSupplierOrderRequest
{
    public int SupplierOid { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string OperatorName { get; init; } = string.Empty;

    public DateTime DocumentDate { get; init; } = DateTime.Today;

    public IReadOnlyList<GestionaleSupplierOrderRow> Rows { get; init; } = [];
}
