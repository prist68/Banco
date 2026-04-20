namespace Banco.Stampa;

public interface IFastReportRuntimeService
{
    Task<FastReportRuntimeSupportInfo> GetRuntimeSupportAsync(CancellationToken cancellationToken = default);

    Task<string> EnsureLayoutFileAsync(
        string layoutFileName,
        CancellationToken cancellationToken = default);

    Task<FastReportRuntimeActionResult> OpenDesignerAsync(
        string layoutFileName,
        CancellationToken cancellationToken = default);

    Task<FastReportRuntimeActionResult> PreviewAsync(
        string layoutFileName,
        CancellationToken cancellationToken = default);

    Task<FastReportRuntimeActionResult> PreviewDocumentAsync(
        string layoutFileName,
        FastReportPreviewDocument document,
        CancellationToken cancellationToken = default);

    Task<FastReportRuntimeActionResult> PrintTestAsync(
        string layoutFileName,
        string printerName,
        CancellationToken cancellationToken = default);

    Task<FastReportRuntimeActionResult> PrintDocumentAsync(
        string layoutFileName,
        FastReportPreviewDocument document,
        string? printerName,
        CancellationToken cancellationToken = default);
}
