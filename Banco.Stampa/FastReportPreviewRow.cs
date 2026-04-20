namespace Banco.Stampa;

public sealed class FastReportPreviewRow
{
    public int RigaOid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public string Descrizione { get; init; } = string.Empty;

    public int OrdineRiga { get; init; }

    public decimal Quantita { get; init; }

    public string UnitaMisura { get; init; } = string.Empty;

    public decimal PrezzoUnitario { get; init; }

    public decimal ScontoPercentuale { get; init; }

    public decimal Sconto2 { get; init; }

    public decimal ImportoRiga { get; init; }

    public decimal AliquotaIva { get; init; }

    public string QuantitaVisuale { get; init; } = string.Empty;

    public string PrezzoUnitarioVisuale { get; init; } = string.Empty;

    public string ScontoVisuale { get; init; } = string.Empty;

    public string Sconto2Visuale { get; init; } = string.Empty;

    public string ImportoRigaVisuale { get; init; } = string.Empty;
}
