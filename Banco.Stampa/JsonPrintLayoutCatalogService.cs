namespace Banco.Stampa;

public sealed class JsonPrintLayoutCatalogService : IPrintLayoutCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IPrintModulePathService _pathService;

    public JsonPrintLayoutCatalogService(IPrintModulePathService pathService)
    {
        _pathService = pathService;
    }

    public async Task<IReadOnlyList<PrintLayoutDefinition>> GetLayoutsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalogPath = _pathService.GetCatalogFilePath();
        var rootDirectory = Path.GetDirectoryName(catalogPath);
        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            Directory.CreateDirectory(rootDirectory);
        }

        if (!File.Exists(catalogPath))
        {
            var defaults = CreateDefaultCatalog();
            await SaveLayoutsAsync(defaults, cancellationToken);
            return defaults;
        }

        PrintLayoutCatalogSettings settings;
        await using (var stream = File.OpenRead(catalogPath))
        {
            settings = await JsonSerializer.DeserializeAsync<PrintLayoutCatalogSettings>(stream, JsonOptions, cancellationToken)
                ?? new PrintLayoutCatalogSettings();
        }

        var layouts = EnsureBuiltinLayouts(settings.Layouts)
            .Where(layout => !string.IsNullOrWhiteSpace(layout.Id) && !string.IsNullOrWhiteSpace(layout.DocumentKey))
            .OrderBy(layout => layout.DocumentKey, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(layout => layout.IsDefault)
            .ThenBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (layouts.Length == 0)
        {
            return CreateDefaultCatalog();
        }

        if (layouts.Length != settings.Layouts.Count)
        {
            await SaveLayoutsAsync(layouts, cancellationToken);
        }

        return layouts;
    }

    public async Task SaveLayoutsAsync(IReadOnlyList<PrintLayoutDefinition> layouts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = (layouts ?? Array.Empty<PrintLayoutDefinition>())
            .Where(layout => !string.IsNullOrWhiteSpace(layout.Id) && !string.IsNullOrWhiteSpace(layout.DocumentKey))
            .Select(layout => layout with
            {
                Id = layout.Id.Trim(),
                DocumentKey = layout.DocumentKey.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(layout.DisplayName) ? layout.DocumentKey.Trim() : layout.DisplayName.Trim(),
                TemplateFileName = string.IsNullOrWhiteSpace(layout.TemplateFileName) ? null : layout.TemplateFileName.Trim(),
                AssignedPrinterName = string.IsNullOrWhiteSpace(layout.AssignedPrinterName) ? null : layout.AssignedPrinterName.Trim(),
                Notes = string.IsNullOrWhiteSpace(layout.Notes) ? null : layout.Notes.Trim()
            })
            .OrderBy(layout => layout.DocumentKey, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(layout => layout.IsDefault)
            .ThenBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var payload = new PrintLayoutCatalogSettings
        {
            Layouts = normalized
        };

        var catalogPath = _pathService.GetCatalogFilePath();
        var rootDirectory = Path.GetDirectoryName(catalogPath);
        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            Directory.CreateDirectory(rootDirectory);
        }

        await using var stream = File.Create(catalogPath);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
    }

    private static IReadOnlyList<PrintLayoutDefinition> CreateDefaultCatalog()
    {
        return EnsureBuiltinLayouts(
        [
            new PrintLayoutDefinition
            {
                Id = "fastreport-customers-list",
                DocumentKey = "customers-list",
                DisplayName = "FastReport elenco clienti starter",
                Engine = PrintEngineKind.FastReport,
                TemplateFileName = "ElencoClienti.frx",
                AssignedPrinterName = null,
                IsDefault = false,
                IsEnabled = true,
                Notes = "Layout starter per la futura famiglia report elenco clienti."
            },
            new PrintLayoutDefinition
            {
                Id = "fastreport-articles-list",
                DocumentKey = "articles-list",
                DisplayName = "FastReport lista articoli starter",
                Engine = PrintEngineKind.FastReport,
                TemplateFileName = "ListaArticoli.frx",
                AssignedPrinterName = null,
                IsDefault = false,
                IsEnabled = true,
                Notes = "Layout starter per la futura famiglia report lista articoli."
            }
        ]);
    }

    private static IReadOnlyList<PrintLayoutDefinition> EnsureBuiltinLayouts(IReadOnlyList<PrintLayoutDefinition> layouts)
    {
        var normalized = layouts.ToList();
        var hasFastReportPilot = normalized.Any(layout =>
            string.Equals(layout.Id, "fastreport-pos-80", StringComparison.OrdinalIgnoreCase));

        if (!hasFastReportPilot)
        {
            normalized.Add(new PrintLayoutDefinition
            {
                Id = "fastreport-pos-80",
                DocumentKey = "receipt-80-db",
                DisplayName = "FastReport POS 80 mm",
                Engine = PrintEngineKind.FastReport,
                TemplateFileName = "Pos.frx",
                AssignedPrinterName = null,
                IsDefault = true,
                IsEnabled = true,
                Notes = "Layout operativo FastReport Open Source associato alla Cortesia Banco."
            });
        }

        normalized = normalized
            .Where(layout => !string.Equals(layout.Id, "questpdf-cortesia-80", StringComparison.OrdinalIgnoreCase))
            .Select(layout => string.Equals(layout.Id, "fastreport-pos-80", StringComparison.OrdinalIgnoreCase)
                ? layout with
                {
                    DisplayName = "FastReport POS 80 mm",
                    IsDefault = true,
                    Notes = "Layout operativo FastReport Open Source associato alla Cortesia Banco."
                }
                : (string.Equals(layout.DocumentKey, "receipt-80-db", StringComparison.OrdinalIgnoreCase) &&
                   layout.Engine == PrintEngineKind.FastReport
                    ? layout with { IsDefault = false }
                    : layout))
            .ToList();

        if (!normalized.Any(layout => string.Equals(layout.Id, "fastreport-customers-list", StringComparison.OrdinalIgnoreCase)))
        {
            normalized.Add(new PrintLayoutDefinition
            {
                Id = "fastreport-customers-list",
                DocumentKey = "customers-list",
                DisplayName = "FastReport elenco clienti starter",
                Engine = PrintEngineKind.FastReport,
                TemplateFileName = "ElencoClienti.frx",
                AssignedPrinterName = null,
                IsDefault = false,
                IsEnabled = true,
                Notes = "Layout starter per la futura famiglia report elenco clienti."
            });
        }

        if (!normalized.Any(layout => string.Equals(layout.Id, "fastreport-articles-list", StringComparison.OrdinalIgnoreCase)))
        {
            normalized.Add(new PrintLayoutDefinition
            {
                Id = "fastreport-articles-list",
                DocumentKey = "articles-list",
                DisplayName = "FastReport lista articoli starter",
                Engine = PrintEngineKind.FastReport,
                TemplateFileName = "ListaArticoli.frx",
                AssignedPrinterName = null,
                IsDefault = false,
                IsEnabled = true,
                Notes = "Layout starter per la futura famiglia report lista articoli."
            });
        }

        return normalized;
    }
}
