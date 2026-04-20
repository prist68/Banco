namespace Banco.Vendita.Fiscal;

public sealed class FiscalizationRow
{
    public int OrdineRiga { get; init; }

    public int? ArticoloOid { get; init; }

    public string? CodiceArticolo { get; init; }

    public string? BarcodeArticolo { get; init; }

    public string UnitaMisura { get; init; } = "PZ";

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public string Descrizione { get; init; } = string.Empty;

    public decimal Quantita { get; init; }

    public decimal ValoreUnitario { get; init; }

    public decimal ImportoRiga { get; init; }

    public int IvaOid { get; init; }

    public decimal Sconto1 { get; init; }

    public decimal Sconto2 { get; init; }

    public decimal Sconto3 { get; init; }

    public decimal Sconto4 { get; init; }

    public bool FlagManuale { get; init; }
}
