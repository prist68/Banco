using System.Drawing.Printing;

namespace Banco.Stampa;

public sealed class SystemPrinterCatalogService : IPrinterCatalogService
{
    public Task<IReadOnlyList<SystemPrinterInfo>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var defaultPrinterName = new PrinterSettings().PrinterName ?? string.Empty;
            var printers = PrinterSettings.InstalledPrinters
                .Cast<string>()
                .Select(printerName => new SystemPrinterInfo
                {
                    Name = printerName,
                    IsDefault = string.Equals(printerName, defaultPrinterName, StringComparison.OrdinalIgnoreCase),
                    IsAvailable = true
                })
                .OrderByDescending(printer => printer.IsDefault)
                .ThenBy(printer => printer.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult<IReadOnlyList<SystemPrinterInfo>>(printers);
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<SystemPrinterInfo>>(Array.Empty<SystemPrinterInfo>());
        }
    }
}
