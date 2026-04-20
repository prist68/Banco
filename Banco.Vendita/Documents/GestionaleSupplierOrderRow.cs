namespace Banco.Vendita.Documents;

public sealed class GestionaleSupplierOrderRow
{
    public int OrdineRiga { get; init; }

    public int? ArticoloOid { get; init; }

    public string? CodiceArticolo { get; init; }

    public string Descrizione { get; init; } = string.Empty;

    public string UnitaMisura { get; init; } = "PZ";

    public decimal Quantita { get; init; }

    public decimal PrezzoUnitario { get; init; }

    public int IvaOid { get; init; } = 1;
}
