namespace Banco.Stampa;

public sealed class FastReportPreviewDocument
{
    public FastReportPreviewHeader Testata { get; init; } = new();

    public FastReportPreviewCustomer Cliente { get; init; } = new();

    public IReadOnlyList<FastReportPreviewRow> Righe { get; init; } = Array.Empty<FastReportPreviewRow>();

    public IReadOnlyList<FastReportPreviewPayment> Pagamenti { get; init; } = Array.Empty<FastReportPreviewPayment>();

    public FastReportPreviewTotals Totali { get; init; } = new();
}
