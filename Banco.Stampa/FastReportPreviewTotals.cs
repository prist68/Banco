namespace Banco.Stampa;

public sealed class FastReportPreviewTotals
{
    public decimal TotaleDocumento { get; init; }

    public decimal TotaleImponibile { get; init; }

    public decimal TotaleIva { get; init; }

    public decimal TotalePagato { get; init; }

    public string TotaleDocumentoVisuale { get; init; } = string.Empty;

    public string TotalePagatoVisuale { get; init; } = string.Empty;

    public string ScontoPercentualeVisuale { get; init; } = string.Empty;

    public string ScontoImportoVisuale { get; init; } = string.Empty;

    public string ContantiVisuale { get; init; } = string.Empty;

    public string PagatoCartaVisuale { get; init; } = string.Empty;

    public string PagamentoPrincipaleLabel { get; init; } = string.Empty;

    public string PagamentoPrincipaleImportoVisuale { get; init; } = string.Empty;

    public string RestoVisuale { get; init; } = string.Empty;
}
