namespace Banco.Vendita.Documents;

public sealed class GestionaleDocumentRowDetail
{
    public int Oid { get; init; }

    public int OrdineRiga { get; init; }

    public int? ArticoloOid { get; init; }

    public string? CodiceArticolo { get; init; }

    public string? BarcodeArticolo { get; init; }

    public string UnitaMisura { get; init; } = "PZ";

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public string Descrizione { get; init; } = string.Empty;

    public decimal Quantita { get; init; }

    public decimal PrezzoUnitario { get; init; }

    public decimal Sconto1 { get; init; }

    public decimal Sconto2 { get; init; }

    public decimal Sconto3 { get; init; }

    public decimal Sconto4 { get; init; }

    public decimal ScontoPercentuale
    {
        get
        {
            var fattore = 1m;
            fattore *= 1 - (Math.Max(0, Math.Min(100, Sconto1)) / 100m);
            fattore *= 1 - (Math.Max(0, Math.Min(100, Sconto2)) / 100m);
            fattore *= 1 - (Math.Max(0, Math.Min(100, Sconto3)) / 100m);
            fattore *= 1 - (Math.Max(0, Math.Min(100, Sconto4)) / 100m);
            return Math.Round((1 - fattore) * 100m, 2, MidpointRounding.AwayFromZero);
        }
    }

    public decimal ImportoRiga { get; init; }

    public int IvaOid { get; init; }

    public string TipoRigaLabel { get; init; } = string.Empty;
}
