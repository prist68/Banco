using Banco.Core.Domain.Entities;
using Banco.Vendita.Fiscal;

namespace Banco.Vendita.Abstractions;

public interface IWinEcrAutoRunService
{
    Task<WinEcrAutoRunResult> GenerateReceiptAsync(DocumentoLocale documento, CancellationToken cancellationToken = default);

    Task<WinEcrAutoRunResult> ExecuteCashRegisterOperationAsync(
        CashRegisterOptionSelection selection,
        DocumentoLocale? documento = null,
        CancellationToken cancellationToken = default);
}
