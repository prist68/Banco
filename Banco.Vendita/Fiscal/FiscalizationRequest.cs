namespace Banco.Vendita.Fiscal;

public sealed class FiscalizationRequest
{
    public Guid LocalDocumentId { get; init; }

    public int ModellodocumentoOid { get; init; } = 27;

    public long Numero { get; init; }

    public int Anno { get; init; }

    public DateTime DataDocumento { get; init; }

    public int SoggettoOid { get; init; }

    public int? ListinoOid { get; init; }

    public string Operatore { get; init; } = string.Empty;

    public int MagazzinoOid { get; init; }

    public int PagamentoOid { get; init; }

    public int CausaleMagazzinoOid { get; init; }

    public decimal TotaleDocumento { get; init; }

    public decimal TotaleImponibile { get; init; }

    public decimal TotaleIva { get; init; }

    public FiscalizationPaymentBreakdown Pagamenti { get; init; } = new();

    public IReadOnlyList<FiscalizationRow> Righe { get; init; } = [];

    public FiscalizationPointsSettlement? PointsSettlement { get; init; }
}
