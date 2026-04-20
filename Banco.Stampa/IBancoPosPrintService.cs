using Banco.Core.Domain.Entities;
using Banco.Vendita.Customers;

namespace Banco.Stampa;

public interface IBancoPosPrintService
{
    Task<FastReportRuntimeActionResult> PreviewCortesiaAsync(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer = null,
        CancellationToken cancellationToken = default);

    Task<FastReportRuntimeActionResult> PrintCortesiaAsync(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer = null,
        CancellationToken cancellationToken = default);
}
