using Banco.Core.Domain.Entities;
using Banco.Vendita.Customers;

namespace Banco.UI.Avalonia.Banco.Services;

public interface IBancoSalePrintService
{
    Task<BancoSalePrintResult> PrintPos80Async(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer,
        CancellationToken cancellationToken = default);

    Task<BancoSalePrintResult> PreviewPos80Async(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer,
        CancellationToken cancellationToken = default);
}

public sealed record BancoSalePrintResult(
    bool Succeeded,
    bool IsSupported,
    string Message,
    string? OutputPath = null,
    string? PrinterName = null);

public sealed class UnsupportedBancoSalePrintService : IBancoSalePrintService
{
    public Task<BancoSalePrintResult> PrintPos80Async(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateUnsupportedResult());
    }

    public Task<BancoSalePrintResult> PreviewPos80Async(
        DocumentoLocale documento,
        GestionaleCustomerSummary? customer,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateUnsupportedResult());
    }

    private static BancoSalePrintResult CreateUnsupportedResult()
    {
        return new BancoSalePrintResult(
            false,
            false,
            "Stampa POS80 non disponibile in questa build Avalonia. Su Windows verra collegata al servizio Banco.Stampa/FastReport esistente.");
    }
}
