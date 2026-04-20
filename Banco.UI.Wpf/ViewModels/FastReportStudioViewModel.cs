using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Diagnostics;
using Banco.Stampa;

namespace Banco.UI.Wpf.ViewModels;

public sealed class FastReportStudioViewModel : ViewModelBase
{
    private readonly IFastReportRuntimeService _fastReportRuntimeService;
    private readonly IFastReportDocumentSchemaService _schemaService;
    private readonly ILegacyRepxReportCatalogService _legacyRepxReportCatalogService;
    private readonly IPrintReportContractCatalogService _printReportContractCatalogService;
    private readonly IPrintReportFamilyCatalogService _printReportFamilyCatalogService;
    private readonly IPrintLayoutCatalogService _layoutCatalogService;
    private readonly IPrinterCatalogService _printerCatalogService;
    private readonly IPrintModulePathService _pathService;

    private FastReportRuntimeSupportInfo? _runtimeSupport;
    private PrintLayoutDefinition? _selectedLayout;
    private FastReportDocumentSchema? _selectedSchema;
    private LegacyRepxReportReference? _selectedLegacyReport;
    private PrintReportContractDefinition? _selectedContract;
    private PrintReportFamilyDefinition? _selectedReportFamily;
    private SystemPrinterInfo? _selectedPrinter;
    private string _statusMessage = "Diagnostica FastReport non ancora caricata.";

    public FastReportStudioViewModel(
        IFastReportRuntimeService fastReportRuntimeService,
        IFastReportDocumentSchemaService schemaService,
        ILegacyRepxReportCatalogService legacyRepxReportCatalogService,
        IPrintReportContractCatalogService printReportContractCatalogService,
        IPrintReportFamilyCatalogService printReportFamilyCatalogService,
        IPrintLayoutCatalogService layoutCatalogService,
        IPrinterCatalogService printerCatalogService,
        IPrintModulePathService pathService)
    {
        _fastReportRuntimeService = fastReportRuntimeService;
        _schemaService = schemaService;
        _legacyRepxReportCatalogService = legacyRepxReportCatalogService;
        _printReportContractCatalogService = printReportContractCatalogService;
        _printReportFamilyCatalogService = printReportFamilyCatalogService;
        _layoutCatalogService = layoutCatalogService;
        _printerCatalogService = printerCatalogService;
        _pathService = pathService;

        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        OpenLayoutsFolderCommand = new RelayCommand(OpenLayoutsFolder);
        OpenCatalogFileCommand = new RelayCommand(OpenCatalogFile);
        OpenSelectedLayoutCommand = new RelayCommand(OpenSelectedLayout, () => SelectedLayout is not null && !string.IsNullOrWhiteSpace(SelectedLayout.TemplateFileName));
        SavePrinterAssignmentCommand = new RelayCommand(() => _ = SavePrinterAssignmentAsync(), CanSavePrinterAssignment);
        PreviewSelectedLayoutCommand = new RelayCommand(() => _ = PreviewSelectedLayoutAsync(), CanRunLayoutRuntimeCommand);
        OpenDesignerCommand = new RelayCommand(() => _ = OpenDesignerAsync(), CanRunLayoutRuntimeCommand);
        PrintTestCommand = new RelayCommand(() => _ = PrintTestAsync(), CanPrintTest);

        _ = RefreshAsync();
    }

    public string Titolo => "FastReport";

    public ObservableCollection<PrintLayoutDefinition> Layouts { get; } = [];

    public ObservableCollection<FastReportDocumentSchema> Schemas { get; } = [];

    public ObservableCollection<LegacyRepxReportReference> LegacyReports { get; } = [];

    public ObservableCollection<SystemPrinterInfo> Printers { get; } = [];

    public ObservableCollection<PrintReportFamilyDefinition> ReportFamilies { get; } = [];

    public PrintLayoutDefinition? SelectedLayout
    {
        get => _selectedLayout;
        set
        {
            if (SetProperty(ref _selectedLayout, value))
            {
                SyncSelectedPrinterWithLayout();
                OpenSelectedLayoutCommand.RaiseCanExecuteChanged();
                SavePrinterAssignmentCommand.RaiseCanExecuteChanged();
                PreviewSelectedLayoutCommand.RaiseCanExecuteChanged();
                OpenDesignerCommand.RaiseCanExecuteChanged();
                PrintTestCommand.RaiseCanExecuteChanged();
                UpdateSelectedContract();
            }
        }
    }

    public FastReportDocumentSchema? SelectedSchema
    {
        get => _selectedSchema;
        set => SetProperty(ref _selectedSchema, value);
    }

    public LegacyRepxReportReference? SelectedLegacyReport
    {
        get => _selectedLegacyReport;
        set
        {
            if (SetProperty(ref _selectedLegacyReport, value))
            {
                NotifyPropertyChanged(nameof(SelectedLegacyReportSections));
                NotifyPropertyChanged(nameof(SelectedLegacyReportParameters));
                NotifyPropertyChanged(nameof(SelectedLegacyReportBindings));
                NotifyPropertyChanged(nameof(SelectedLegacyReportRules));
                NotifyPropertyChanged(nameof(SelectedLegacySourceFilePath));
                NotifyPropertyChanged(nameof(SelectedLegacyPrinter));
                NotifyPropertyChanged(nameof(SelectedLegacyPaperMode));
            }
        }
    }

    public SystemPrinterInfo? SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            if (SetProperty(ref _selectedPrinter, value))
            {
                SavePrinterAssignmentCommand.RaiseCanExecuteChanged();
                PrintTestCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public PrintReportContractDefinition? SelectedContract
    {
        get => _selectedContract;
        private set
        {
            if (SetProperty(ref _selectedContract, value))
            {
                NotifyPropertyChanged(nameof(SelectedContractFamily));
                NotifyPropertyChanged(nameof(SelectedContractDomainContext));
                NotifyPropertyChanged(nameof(SelectedContractRuntimeParameters));
                NotifyPropertyChanged(nameof(SelectedContractMappings));
            }
        }
    }

    public PrintReportFamilyDefinition? SelectedReportFamily
    {
        get => _selectedReportFamily;
        set
        {
            if (SetProperty(ref _selectedReportFamily, value))
            {
                SyncSelectionsFromFamily();
                NotifyPropertyChanged(nameof(SelectedReportFamilyName));
                NotifyPropertyChanged(nameof(SelectedReportFamilyDomainContext));
                NotifyPropertyChanged(nameof(SelectedReportFamilyRuntimeModel));
                NotifyPropertyChanged(nameof(SelectedReportFamilySupportedDocuments));
                NotifyPropertyChanged(nameof(SelectedReportFamilyGroups));
            }
        }
    }

    public string RuntimeDescription => _runtimeSupport?.RuntimeDescription ?? "Runtime non verificato.";

    public string BlockingReason => string.IsNullOrWhiteSpace(_runtimeSupport?.BlockingReason)
        ? "Nessun blocco rilevato."
        : _runtimeSupport!.BlockingReason;

    public string DesignerModeLabel => _runtimeSupport?.DesignerMode switch
    {
        FastReportDesignerMode.EmbeddedInBanco => "Designer integrato nella UI Banco",
        FastReportDesignerMode.ExternalWindow => "Designer avviabile in finestra separata",
        _ => "Designer non disponibile"
    };

    public string LayoutsDirectory => _runtimeSupport?.LayoutsDirectory ?? _pathService.GetLayoutsDirectory();

    public string CatalogFilePath => _pathService.GetCatalogFilePath();

    public string RuntimeAssemblyPath => string.IsNullOrWhiteSpace(_runtimeSupport?.RuntimeAssemblyPath)
        ? "Non rilevato"
        : _runtimeSupport!.RuntimeAssemblyPath;

    public string DesignerAssemblyPath => string.IsNullOrWhiteSpace(_runtimeSupport?.DesignerAssemblyPath)
        ? "Non rilevato"
        : _runtimeSupport!.DesignerAssemblyPath;

    public string DetectedVersion => string.IsNullOrWhiteSpace(_runtimeSupport?.DetectedVersion)
        ? "-"
        : _runtimeSupport!.DetectedVersion;

    public bool CanOpenDesigner => _runtimeSupport?.CanOpenDesigner == true;

    public bool CanAttemptPreview => _runtimeSupport?.CanAttemptPreview == true;

    public bool CanAttemptPrint => _runtimeSupport?.CanAttemptPrint == true;

    public string SelectedContractFamily => SelectedContract?.Family ?? "-";

    public string SelectedContractDomainContext => SelectedContract?.DomainContext ?? "-";

    public string SelectedContractRuntimeParameters => SelectedContract?.RuntimeParametersSummary ?? "-";

    public string SelectedContractMappings => JoinLines(SelectedContract?.FieldMappings.Select(mapping =>
        $"{mapping.Zone} | {mapping.TargetField} <= {mapping.SourceField} [{FormatConfidence(mapping.Confidence)}]"));

    public string SelectedReportFamilyName => SelectedReportFamily?.DisplayName ?? "-";

    public string SelectedReportFamilyDomainContext => SelectedReportFamily?.DomainContext ?? "-";

    public string SelectedReportFamilyRuntimeModel => SelectedReportFamily?.RuntimeModelSummary ?? "-";

    public string SelectedReportFamilySupportedDocuments => JoinLines(SelectedReportFamily?.SupportedDocumentKeys);

    public string SelectedReportFamilyGroups => JoinLines(SelectedReportFamily?.FieldGroups.Select(group =>
        $"{group.ReportArea} / {group.DisplayName}: {group.Description}{Environment.NewLine}"
        + JoinLines(group.Fields.Select(fieldDefinition =>
            $"  - {fieldDefinition.DisplayName} | {fieldDefinition.BindingPath} | {fieldDefinition.SourceContext} [{FormatConfidence(fieldDefinition.Confidence)}]"))));

    public string SelectedLegacySourceFilePath => SelectedLegacyReport?.SourceFilePath ?? "Nessun riferimento legacy selezionato.";

    public string SelectedLegacyPrinter => string.IsNullOrWhiteSpace(SelectedLegacyReport?.LegacyPrinterName)
        ? "Non indicata"
        : SelectedLegacyReport!.LegacyPrinterName!;

    public string SelectedLegacyPaperMode => SelectedLegacyReport is null
        ? "-"
        : $"Larghezza {SelectedLegacyReport.PageWidth}, roll paper = {SelectedLegacyReport.RollPaper}";

    public string SelectedLegacyReportSections => JoinLines(SelectedLegacyReport?.Sections);

    public string SelectedLegacyReportParameters => JoinLines(SelectedLegacyReport?.Parameters.Select(parameter =>
        string.IsNullOrWhiteSpace(parameter.Notes)
            ? $"{parameter.Name} - {parameter.DisplayName}"
            : $"{parameter.Name} - {parameter.DisplayName} ({parameter.Notes})"));

    public string SelectedLegacyReportBindings => JoinLines(SelectedLegacyReport?.Bindings.Select(binding =>
        string.IsNullOrWhiteSpace(binding.Notes)
            ? $"{binding.ControlName} <= {binding.Expression}"
            : $"{binding.ControlName} <= {binding.Expression} ({binding.Notes})"));

    public string SelectedLegacyReportRules => JoinLines(SelectedLegacyReport?.Rules.Select(rule =>
        $"{rule.Name} - {rule.Description}"));

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand OpenLayoutsFolderCommand { get; }

    public RelayCommand OpenCatalogFileCommand { get; }

    public RelayCommand OpenSelectedLayoutCommand { get; }

    public RelayCommand SavePrinterAssignmentCommand { get; }

    public RelayCommand PreviewSelectedLayoutCommand { get; }

    public RelayCommand OpenDesignerCommand { get; }

    public RelayCommand PrintTestCommand { get; }

    public async Task RefreshAsync()
    {
        try
        {
            _runtimeSupport = await _fastReportRuntimeService.GetRuntimeSupportAsync();
            var layouts = await _layoutCatalogService.GetLayoutsAsync();
            var schemas = await _schemaService.GetSchemasAsync();
            var legacyReports = await _legacyRepxReportCatalogService.GetReportsAsync();
            var contracts = await _printReportContractCatalogService.GetContractsAsync();
            var families = await _printReportFamilyCatalogService.GetFamiliesAsync();
            var printers = await _printerCatalogService.GetAvailablePrintersAsync();

            Layouts.Clear();
            foreach (var layout in layouts.OrderBy(item => item.Engine).ThenBy(item => item.DocumentKey, StringComparer.OrdinalIgnoreCase))
            {
                Layouts.Add(layout);
            }

            Schemas.Clear();
            foreach (var schema in schemas.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                Schemas.Add(schema);
            }

            LegacyReports.Clear();
            foreach (var legacyReport in legacyReports.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                LegacyReports.Add(legacyReport);
            }

            Printers.Clear();
            foreach (var printer in printers.OrderByDescending(item => item.IsDefault).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                Printers.Add(printer);
            }

            ReportFamilies.Clear();
            foreach (var family in families.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                ReportFamilies.Add(family);
            }

            SelectedLayout ??= Layouts.FirstOrDefault(layout => layout.Engine == PrintEngineKind.FastReport)
                ?? Layouts.FirstOrDefault();
            SelectedSchema ??= Schemas.FirstOrDefault();
            SelectedLegacyReport ??= LegacyReports.FirstOrDefault();
            SelectedContract ??= contracts.FirstOrDefault(item =>
                string.Equals(item.DocumentKey, SelectedLayout?.DocumentKey, StringComparison.OrdinalIgnoreCase))
                ?? contracts.FirstOrDefault();
            SelectedReportFamily ??= ResolveFamilyFromSelection(families, SelectedContract, SelectedLayout)
                ?? families.FirstOrDefault();
            SyncSelectedPrinterWithLayout();

            NotifyPropertyChanged(nameof(RuntimeDescription));
            NotifyPropertyChanged(nameof(BlockingReason));
            NotifyPropertyChanged(nameof(DesignerModeLabel));
            NotifyPropertyChanged(nameof(LayoutsDirectory));
            NotifyPropertyChanged(nameof(CatalogFilePath));
            NotifyPropertyChanged(nameof(RuntimeAssemblyPath));
            NotifyPropertyChanged(nameof(DesignerAssemblyPath));
            NotifyPropertyChanged(nameof(DetectedVersion));
            NotifyPropertyChanged(nameof(CanOpenDesigner));
            NotifyPropertyChanged(nameof(CanAttemptPreview));
            NotifyPropertyChanged(nameof(CanAttemptPrint));
            NotifyPropertyChanged(nameof(SelectedLegacySourceFilePath));
            NotifyPropertyChanged(nameof(SelectedLegacyPrinter));
            NotifyPropertyChanged(nameof(SelectedLegacyPaperMode));
            NotifyPropertyChanged(nameof(SelectedLegacyReportSections));
            NotifyPropertyChanged(nameof(SelectedLegacyReportParameters));
            NotifyPropertyChanged(nameof(SelectedLegacyReportBindings));
            NotifyPropertyChanged(nameof(SelectedLegacyReportRules));
            NotifyPropertyChanged(nameof(SelectedContractFamily));
            NotifyPropertyChanged(nameof(SelectedContractDomainContext));
            NotifyPropertyChanged(nameof(SelectedContractRuntimeParameters));
            NotifyPropertyChanged(nameof(SelectedContractMappings));
            NotifyPropertyChanged(nameof(SelectedReportFamilyName));
            NotifyPropertyChanged(nameof(SelectedReportFamilyDomainContext));
            NotifyPropertyChanged(nameof(SelectedReportFamilyRuntimeModel));
            NotifyPropertyChanged(nameof(SelectedReportFamilySupportedDocuments));
            NotifyPropertyChanged(nameof(SelectedReportFamilyGroups));

            StatusMessage = $"Diagnostica FastReport aggiornata. Layout catalogati: {Layouts.Count}. Schemi documento: {Schemas.Count}. Famiglie report: {ReportFamilies.Count}. Riferimenti legacy: {LegacyReports.Count}. Stampanti: {Printers.Count}.";
            OpenSelectedLayoutCommand.RaiseCanExecuteChanged();
            SavePrinterAssignmentCommand.RaiseCanExecuteChanged();
            PreviewSelectedLayoutCommand.RaiseCanExecuteChanged();
            OpenDesignerCommand.RaiseCanExecuteChanged();
            PrintTestCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore diagnostica FastReport: {ex.Message}";
        }
    }

    private void OpenLayoutsFolder()
    {
        var directory = _pathService.GetLayoutsDirectory();
        Directory.CreateDirectory(directory);
        OpenPathWithShell(directory);
        StatusMessage = $"Cartella layout aperta: {directory}";
    }

    private void OpenCatalogFile()
    {
        var catalogPath = _pathService.GetCatalogFilePath();
        if (!File.Exists(catalogPath))
        {
            File.WriteAllText(catalogPath, "{\r\n  \"Layouts\": []\r\n}");
        }

        OpenPathWithShell(catalogPath);
        StatusMessage = $"Catalogo layout aperto: {catalogPath}";
    }

    private void OpenSelectedLayout()
    {
        if (SelectedLayout is null || string.IsNullOrWhiteSpace(SelectedLayout.TemplateFileName))
        {
            return;
        }

        try
        {
            var layoutPath = _fastReportRuntimeService
                .EnsureLayoutFileAsync(SelectedLayout.TemplateFileName)
                .GetAwaiter()
                .GetResult();

            OpenPathWithShell(layoutPath);
            StatusMessage = $"Layout aperto: {layoutPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apertura layout fallita: {ex.Message}";
        }
    }

    private async Task SavePrinterAssignmentAsync()
    {
        if (SelectedLayout is null)
        {
            return;
        }

        var layouts = await _layoutCatalogService.GetLayoutsAsync();
        var updatedLayouts = layouts
            .Select(layout => layout.Id == SelectedLayout.Id
                ? layout with { AssignedPrinterName = SelectedPrinter?.Name }
                : layout)
            .ToArray();

        await _layoutCatalogService.SaveLayoutsAsync(updatedLayouts);
        SelectedLayout = updatedLayouts.FirstOrDefault(layout => layout.Id == SelectedLayout.Id);
        StatusMessage = SelectedPrinter is null
            ? $"Associazione stampante rimossa dal layout '{SelectedLayout?.DisplayName}'."
            : $"Stampante '{SelectedPrinter.Name}' associata al layout '{SelectedLayout?.DisplayName}'.";
    }

    private async Task PreviewSelectedLayoutAsync()
    {
        if (SelectedLayout is null || string.IsNullOrWhiteSpace(SelectedLayout.TemplateFileName))
        {
            return;
        }

        var result = await _fastReportRuntimeService.PreviewAsync(SelectedLayout.TemplateFileName);
        StatusMessage = result.Message;
    }

    private async Task OpenDesignerAsync()
    {
        if (SelectedLayout is null || string.IsNullOrWhiteSpace(SelectedLayout.TemplateFileName))
        {
            return;
        }

        var result = await _fastReportRuntimeService.OpenDesignerAsync(SelectedLayout.TemplateFileName);
        StatusMessage = result.Message;
    }

    private async Task PrintTestAsync()
    {
        if (SelectedLayout is null || string.IsNullOrWhiteSpace(SelectedLayout.TemplateFileName))
        {
            return;
        }

        var printerName = SelectedPrinter?.Name
            ?? SelectedLayout.AssignedPrinterName
            ?? string.Empty;

        var result = await _fastReportRuntimeService.PrintTestAsync(SelectedLayout.TemplateFileName, printerName);
        StatusMessage = result.Message;
    }

    private bool CanSavePrinterAssignment()
    {
        return SelectedLayout is not null;
    }

    private bool CanRunLayoutRuntimeCommand()
    {
        return SelectedLayout is not null && !string.IsNullOrWhiteSpace(SelectedLayout.TemplateFileName);
    }

    private bool CanPrintTest()
    {
        return CanRunLayoutRuntimeCommand() && (SelectedPrinter is not null || !string.IsNullOrWhiteSpace(SelectedLayout?.AssignedPrinterName));
    }

    private void SyncSelectedPrinterWithLayout()
    {
        if (SelectedLayout is null)
        {
            SelectedPrinter = Printers.FirstOrDefault(item => item.IsDefault);
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedLayout.AssignedPrinterName))
        {
            SelectedPrinter = Printers.FirstOrDefault(item =>
                string.Equals(item.Name, SelectedLayout.AssignedPrinterName, StringComparison.OrdinalIgnoreCase));
            return;
        }

        SelectedPrinter = Printers.FirstOrDefault(item => item.IsDefault);
    }

    private void UpdateSelectedContract()
    {
        if (SelectedLayout is null)
        {
            SelectedContract = null;
            SelectedReportFamily = null;
            return;
        }

        var contract = _printReportContractCatalogService
            .GetContractAsync(SelectedLayout.DocumentKey)
            .GetAwaiter()
            .GetResult();

        SelectedContract = contract;

        var families = _printReportFamilyCatalogService
            .GetFamiliesAsync()
            .GetAwaiter()
            .GetResult();

        SelectedReportFamily = ResolveFamilyFromSelection(families, contract, SelectedLayout)
            ?? SelectedReportFamily
            ?? families.FirstOrDefault();
    }

    private static PrintReportFamilyDefinition? ResolveFamilyFromSelection(
        IReadOnlyList<PrintReportFamilyDefinition> families,
        PrintReportContractDefinition? contract,
        PrintLayoutDefinition? layout)
    {
        if (!string.IsNullOrWhiteSpace(layout?.DocumentKey))
        {
            var byDocumentKey = families.FirstOrDefault(item =>
                item.SupportedDocumentKeys.Any(key => string.Equals(key, layout.DocumentKey, StringComparison.OrdinalIgnoreCase)));

            if (byDocumentKey is not null)
            {
                return byDocumentKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(contract?.Family))
        {
            return families.FirstOrDefault(item =>
                item.DisplayName.Contains(contract.Family, StringComparison.OrdinalIgnoreCase)
                || contract.Family.Contains(item.DisplayName, StringComparison.OrdinalIgnoreCase)
                || item.FamilyKey.Contains("pos", StringComparison.OrdinalIgnoreCase) && contract.Family.Contains("POS", StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private void SyncSelectionsFromFamily()
    {
        if (SelectedReportFamily is null)
        {
            return;
        }

        if (SelectedLayout is null || !SelectedReportFamily.SupportedDocumentKeys.Any(key =>
                string.Equals(key, SelectedLayout.DocumentKey, StringComparison.OrdinalIgnoreCase)))
        {
            var matchingLayout = Layouts.FirstOrDefault(layout =>
                SelectedReportFamily.SupportedDocumentKeys.Any(key =>
                    string.Equals(key, layout.DocumentKey, StringComparison.OrdinalIgnoreCase)));

            if (matchingLayout is not null && !ReferenceEquals(matchingLayout, SelectedLayout))
            {
                SelectedLayout = matchingLayout;
                return;
            }
        }

        if (SelectedSchema is null || !SelectedReportFamily.SupportedDocumentKeys.Any(key =>
                string.Equals(key, SelectedSchema.DocumentKey, StringComparison.OrdinalIgnoreCase)))
        {
            var matchingSchema = Schemas.FirstOrDefault(schema =>
                SelectedReportFamily.SupportedDocumentKeys.Any(key =>
                    string.Equals(key, schema.DocumentKey, StringComparison.OrdinalIgnoreCase)));

            if (matchingSchema is not null)
            {
                SelectedSchema = matchingSchema;
            }
        }
    }

    private static string FormatConfidence(PrintContractConfidence confidence)
    {
        return confidence switch
        {
            PrintContractConfidence.Certain => "Certo",
            PrintContractConfidence.StrongInference => "Deduzione forte",
            _ => "Da verificare"
        };
    }

    private static string JoinLines(IEnumerable<string>? values)
    {
        var normalized = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()
            ?? [];

        return normalized.Length == 0
            ? "-"
            : string.Join(Environment.NewLine, normalized);
    }

    private static void OpenPathWithShell(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            MessageBox.Show(
                "Il percorso richiesto non esiste.",
                "Apri risorsa",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }
}
