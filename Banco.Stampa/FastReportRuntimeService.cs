using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.Text;
using FastReport;
using FastReport.Barcode;
using FastReport.Data;
using FastReport.Export.Html;

namespace Banco.Stampa;

public sealed class FastReportRuntimeService : IFastReportRuntimeService
{
    private readonly IPrintModulePathService _pathService;
    private readonly IPrinterCatalogService _printerCatalogService;
    private readonly IFastReportStoreProfileService _storeProfileService;
    private readonly IFastReportPreviewDataService _previewDataService;

    public FastReportRuntimeService(
        IPrintModulePathService pathService,
        IPrinterCatalogService printerCatalogService,
        IFastReportStoreProfileService storeProfileService,
        IFastReportPreviewDataService previewDataService)
    {
        _pathService = pathService;
        _printerCatalogService = printerCatalogService;
        _storeProfileService = storeProfileService;
        _previewDataService = previewDataService;
    }

    public async Task<FastReportRuntimeSupportInfo> GetRuntimeSupportAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var layoutsDirectory = _pathService.GetLayoutsDirectory();
        var printers = await _printerCatalogService.GetAvailablePrintersAsync(cancellationToken);
        var runtimeAssemblyPath = typeof(Report).Assembly.Location;
        var detectedVersion = typeof(Report).Assembly.GetName().Version?.ToString() ?? string.Empty;
        var designerPath = FindCommunityDesignerPath();

        return new FastReportRuntimeSupportInfo
        {
            CanOpenDesigner = !string.IsNullOrWhiteSpace(designerPath),
            CanAttemptPreview = true,
            CanAttemptPrint = true,
            DesignerMode = string.IsNullOrWhiteSpace(designerPath)
                ? FastReportDesignerMode.NotAvailable
                : FastReportDesignerMode.ExternalWindow,
            RuntimeDescription = "Runtime FastReport Open Source rilevato. Preview e stampa POS80 passano da export HTML locale governato da Banco.",
            BlockingReason = string.IsNullOrWhiteSpace(designerPath)
                ? "Designer integrato non disponibile nel runtime Open Source. Per modificare visualmente il layout serve un Community Designer esterno installato."
                : (printers.Any(printer => printer.IsAvailable)
                    ? string.Empty
                    : "Nessuna stampante di sistema rilevata; il test stampa prepara comunque un HTML locale stampabile."),
            LayoutsDirectory = layoutsDirectory,
            RuntimeAssemblyPath = runtimeAssemblyPath,
            DesignerAssemblyPath = designerPath ?? string.Empty,
            DetectedVersion = detectedVersion
        };
    }

    public Task<string> EnsureLayoutFileAsync(
        string layoutFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EnsureLayoutTemplateExists(layoutFileName));
    }

    public async Task<FastReportRuntimeActionResult> OpenDesignerAsync(
        string layoutFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var layoutPath = EnsureLayoutTemplateExists(layoutFileName);
            await PrepareDesignTimeDataAsync(layoutPath, cancellationToken);
            MarkLayoutAsCustomized(layoutPath);
            var designerPath = FindCommunityDesignerPath();
            if (string.IsNullOrWhiteSpace(designerPath))
            {
                return new FastReportRuntimeActionResult
                {
                    IsSupported = false,
                    Succeeded = false,
                    Message = "Designer FastReport non disponibile nel pacchetto Open Source installato. Serve un Community Designer esterno."
                };
            }

            OpenWithShell(designerPath, $"\"{layoutPath}\"");

            return new FastReportRuntimeActionResult
            {
                IsSupported = true,
                Succeeded = true,
                Message = $"Designer esterno FastReport aperto per '{layoutFileName}'."
            };
        }
        catch (Exception ex)
        {
            return BuildFailureResult("Designer", layoutFileName, ex);
        }
    }

    public async Task<FastReportRuntimeActionResult> PreviewAsync(
        string layoutFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var layoutPath = EnsureLayoutTemplateExists(layoutFileName);
            using var report = await CreateConfiguredReportAsync(layoutPath, cancellationToken);
            report.Prepare();
            var previewPath = BuildOutputFilePath(layoutFileName, "preview", "html");
            using var export = new HTMLExport
            {
                Preview = true,
                Navigator = true,
                SinglePage = true,
                EmbedPictures = true,
                Pictures = true,
                SubFolder = false,
                OpenAfterExport = false
            };

            export.Export(report, previewPath);

            return new FastReportRuntimeActionResult
            {
                IsSupported = true,
                Succeeded = true,
                Message = $"Anteprima FastReport generata: {previewPath}",
                OutputPath = previewPath
            };
        }
        catch (Exception ex)
        {
            return BuildFailureResult("Anteprima", layoutFileName, ex);
        }
    }

    public async Task<FastReportRuntimeActionResult> PreviewDocumentAsync(
        string layoutFileName,
        FastReportPreviewDocument document,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var layoutPath = EnsureLayoutTemplateExists(layoutFileName);
            using var report = await CreateConfiguredReportAsync(layoutPath, document, cancellationToken);
            report.Prepare();
            var previewPath = BuildOutputFilePath(layoutFileName, "preview", "html");
            using var export = new HTMLExport
            {
                Preview = true,
                Navigator = true,
                SinglePage = true,
                EmbedPictures = true,
                Pictures = true,
                SubFolder = false,
                OpenAfterExport = false
            };

            export.Export(report, previewPath);

            return new FastReportRuntimeActionResult
            {
                IsSupported = true,
                Succeeded = true,
                Message = $"Anteprima FastReport generata: {previewPath}",
                OutputPath = previewPath
            };
        }
        catch (Exception ex)
        {
            return BuildFailureResult("Anteprima", layoutFileName, ex);
        }
    }

    public async Task<FastReportRuntimeActionResult> PrintTestAsync(
        string layoutFileName,
        string printerName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var layoutPath = EnsureLayoutTemplateExists(layoutFileName);
            using var report = await CreateConfiguredReportAsync(layoutPath, cancellationToken);
            report.Prepare();
            var printPreviewPath = BuildOutputFilePath(layoutFileName, "print", "html");
            using var export = new HTMLExport
            {
                Print = false,
                Navigator = false,
                SinglePage = true,
                EmbedPictures = true,
                Pictures = true,
                SubFolder = false,
                OpenAfterExport = false
            };

            export.Export(report, printPreviewPath);
            OpenWithShell(printPreviewPath);

            var suffix = string.IsNullOrWhiteSpace(printerName)
                ? string.Empty
                : $" sulla stampante '{printerName}'";

            return new FastReportRuntimeActionResult
            {
                IsSupported = true,
                Succeeded = true,
                Message = $"Test stampa FastReport generato per '{layoutFileName}'{suffix}. L'HTML aperto e` stampabile dal browser."
            };
        }
        catch (Exception ex)
        {
            return BuildFailureResult("Test stampa", layoutFileName, ex);
        }
    }

    public async Task<FastReportRuntimeActionResult> PrintDocumentAsync(
        string layoutFileName,
        FastReportPreviewDocument document,
        string? printerName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var layoutPath = EnsureLayoutTemplateExists(layoutFileName);
            using var report = await CreateConfiguredReportAsync(layoutPath, document, cancellationToken);
            report.Prepare();
            var printPreviewPath = BuildOutputFilePath(layoutFileName, "print", "html");
            using var export = new HTMLExport
            {
                Print = false,
                Navigator = false,
                SinglePage = true,
                EmbedPictures = true,
                Pictures = true,
                SubFolder = false,
                OpenAfterExport = false
            };

            export.Export(report, printPreviewPath);

            var suffix = string.IsNullOrWhiteSpace(printerName)
                ? string.Empty
                : $" sulla stampante '{printerName}'";

            return new FastReportRuntimeActionResult
            {
                IsSupported = true,
                Succeeded = true,
                Message = $"Stampa FastReport preparata per '{layoutFileName}'{suffix}.",
                OutputPath = printPreviewPath,
                AssignedPrinterName = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim()
            };
        }
        catch (Exception ex)
        {
            return BuildFailureResult("Stampa", layoutFileName, ex);
        }
    }

    private async Task<Report> CreateConfiguredReportAsync(string layoutPath, CancellationToken cancellationToken)
    {
        var previewDocument = await LoadPreviewDocumentAsync(cancellationToken);
        return await CreateConfiguredReportAsync(layoutPath, previewDocument, cancellationToken);
    }

    private async Task<Report> CreateConfiguredReportAsync(
        string layoutPath,
        FastReportPreviewDocument previewDocument,
        CancellationToken cancellationToken)
    {
        var storeProfile = await LoadStoreProfileAsync(cancellationToken);
        PreparePosPilotDesignData(layoutPath, previewDocument.Righe, previewDocument.Pagamenti);

        var report = new Report();
        report.Load(layoutPath);
        EnableRegisteredDataSources(report);
        EnsurePilotParameters(report);
        BindPilotDataBand(report);
        ApplyPreviewLayoutValues(report, previewDocument, storeProfile);

        return report;
    }

    private string EnsureLayoutTemplateExists(string layoutFileName)
    {
        if (string.IsNullOrWhiteSpace(layoutFileName))
        {
            throw new InvalidOperationException("Nessun file layout FastReport selezionato.");
        }

        var layoutPath = Path.Combine(_pathService.GetLayoutsDirectory(), layoutFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);

        if (File.Exists(layoutPath))
        {
            if (string.Equals(layoutFileName, "Pos.frx", StringComparison.OrdinalIgnoreCase)
                && !IsLayoutCustomizationLocked(layoutPath)
                && ShouldRefreshPosPilotTemplate(layoutPath))
            {
                CreatePosPilotTemplate(layoutPath);
            }

            return layoutPath;
        }

        if (string.Equals(layoutFileName, "Pos.frx", StringComparison.OrdinalIgnoreCase))
        {
            CreatePosPilotTemplate(layoutPath);
            return layoutPath;
        }

        using var report = new Report();
        report.Save(layoutPath);
        return layoutPath;
    }

    private static bool IsLayoutCustomizationLocked(string layoutPath)
    {
        return File.Exists(GetCustomizationLockPath(layoutPath));
    }

    private static void MarkLayoutAsCustomized(string layoutPath)
    {
        if (!string.Equals(Path.GetFileName(layoutPath), "Pos.frx", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var lockPath = GetCustomizationLockPath(layoutPath);
        if (File.Exists(lockPath))
        {
            return;
        }

        File.WriteAllText(
            lockPath,
            "Layout POS personalizzato da designer Banco. Non rigenerare automaticamente il template pilota.",
            Encoding.UTF8);
    }

    private static string GetCustomizationLockPath(string layoutPath)
    {
        return $"{layoutPath}.customized.lock";
    }

    private string BuildOutputFilePath(string layoutFileName, string suffix, string extension)
    {
        var outputDirectory = Path.Combine(_pathService.GetRootDirectory(), "Anteprime");
        Directory.CreateDirectory(outputDirectory);

        var baseName = Path.GetFileNameWithoutExtension(layoutFileName);
        return Path.Combine(outputDirectory, $"{baseName}.{suffix}.{extension}");
    }

    private static void EnableRegisteredDataSources(Report report)
    {
        foreach (var dataSource in report.Dictionary.DataSources.OfType<DataSourceBase>())
        {
            dataSource.Enabled = true;
        }
    }

    private static void BindPilotDataBand(Report report)
    {
        if (report.FindObject("DataRighe") is not DataBand dataBand)
        {
            return;
        }

        var dataSource = report.GetDataSource("Righe");
        if (dataSource is not null)
        {
            dataSource.Enabled = true;
            dataBand.DataSource = dataSource;
        }
    }

    private static bool ShouldRefreshPosPilotTemplate(string layoutPath)
    {
        var templateContent = File.ReadAllText(layoutPath);
        return templateContent.Contains("DocumentoBanco.", StringComparison.Ordinal)
            || templateContent.Contains("[Ragionesociale]", StringComparison.Ordinal)
            || templateContent.Contains("[Bancanome]", StringComparison.Ordinal)
            || templateContent.Contains("<Dictionary/>", StringComparison.Ordinal)
            || !templateContent.Contains("CsvDataConnection Name=\"RigheCsv\"", StringComparison.Ordinal)
            || !templateContent.Contains("TableDataSource Name=\"Righe\"", StringComparison.Ordinal)
            || !templateContent.Contains("CsvDataConnection Name=\"ArticoloCsv\"", StringComparison.Ordinal)
            || !templateContent.Contains("TableDataSource Name=\"Articolo\"", StringComparison.Ordinal)
            || templateContent.Contains("Parameter Name=\"StoreRagioneSociale\"", StringComparison.Ordinal)
            || templateContent.Contains("Parameter Name=\"TestataPagamentoLabel\"", StringComparison.Ordinal)
            || !templateContent.Contains("Parameter Name=\"Negozio\"", StringComparison.Ordinal)
            || !templateContent.Contains("Parameter Name=\"Testata\"", StringComparison.Ordinal)
            || !templateContent.Contains("Parameter Name=\"Totali\"", StringComparison.Ordinal)
            || !templateContent.Contains("Parameter Name=\"Cliente\"", StringComparison.Ordinal)
            || !templateContent.Contains("Parameter Name=\"Footer\"", StringComparison.Ordinal);
    }

    private static void ApplyPreviewLayoutValues(
        Report report,
        FastReportPreviewDocument document,
        FastReportStoreProfile storeProfile)
    {
        SetParameterValue(report, "Negozio.BrandLine", string.Empty);
        SetParameterValue(report, "Negozio.RagioneSociale", storeProfile.RagioneSociale);
        SetParameterValue(report, "Negozio.IndirizzoCompleto", storeProfile.IntestazioneCompleta);
        SetParameterValue(report, "Negozio.ContattiCompleti", storeProfile.ContattiCompleti);
        SetParameterValue(report, "Negozio.PartitaIvaVisuale", storeProfile.PartitaIvaVisuale);
        SetParameterValue(report, "Testata.PagamentoLabel", document.Testata.PagamentoLabel);
        SetParameterValue(report, "Testata.DocumentoLabel", document.Testata.DocumentoLabel);
        SetParameterValue(report, "Testata.AnnoVisuale", document.Testata.AnnoVisuale);
        SetParameterValue(report, "Testata.DataTesto", document.Testata.DataTesto);
        SetParameterValue(report, "Testata.NumeroVisuale", document.Testata.Numero.ToString());
        SetParameterValue(report, "Testata.ProgressivoVenditaLabel", document.Testata.ProgressivoVenditaLabel);
        SetParameterValue(report, "Totali.TotaleDocumentoVisuale", document.Totali.TotaleDocumentoVisuale);
        SetParameterValue(report, "Totali.TotalePagatoVisuale", document.Totali.TotalePagatoVisuale);
        SetParameterValue(report, "Totali.ValutaLabel", "EUR");
        SetParameterValue(report, "Totali.ScontoPercentualeVisuale", document.Totali.ScontoPercentualeVisuale);
        SetParameterValue(report, "Totali.ScontoImportoVisuale", document.Totali.ScontoImportoVisuale);
        SetParameterValue(report, "Totali.ContantiVisuale", document.Totali.ContantiVisuale);
        SetParameterValue(report, "Totali.PagatoCartaVisuale", document.Totali.PagatoCartaVisuale);
        SetParameterValue(report, "Totali.PagamentoPrincipaleLabel", document.Totali.PagamentoPrincipaleLabel);
        SetParameterValue(report, "Totali.PagamentoPrincipaleImportoVisuale", document.Totali.PagamentoPrincipaleImportoVisuale);
        SetParameterValue(report, "Totali.RestoVisuale", document.Totali.RestoVisuale);
        SetParameterValue(report, "Cliente.Nominativo", document.Cliente.Nominativo);
        SetParameterValue(report, "Cliente.CodiceCartaFedelta", document.Cliente.CodiceCartaFedelta);
        SetParameterValue(report, "Cliente.PuntiPrecedentiLabel", $"Punti prec.: {document.Cliente.PuntiPrecedentiVisuale}");
        SetParameterValue(report, "Cliente.PuntiAttualiLabel", $"Punti att.: {document.Cliente.PuntiAttualiVisuale}");
        SetParameterValue(report, "Punti.PrecedentiLabel", $"Punti prec.: {document.Cliente.PuntiPrecedentiVisuale}");
        SetParameterValue(report, "Punti.AttualiLabel", $"Punti att.: {document.Cliente.PuntiAttualiVisuale}");
        SetParameterValue(report, "Punti.MaturatiVendita", string.Empty);
        SetParameterValue(report, "Punti.UtilizzatiVendita", string.Empty);
        SetParameterValue(report, "Footer.Website", BuildWebsiteReference(storeProfile.RiferimentoScontrino));
        SetParameterValue(report, "Footer.GestionaleLabel", "Gestionale vendita");
        SetParameterValue(report, "Footer.NoteFinali", "Non si accettano reclami trascorsi 8 giorni dalla data del presente documento. La garanzia delle sigarette elettroniche e` di 3 mesi.");
        ApplyStoreLogo(report);
    }

    private static void SetParameterValue(Report report, string parameterName, object value)
    {
        report.SetParameterValue(parameterName, value);
    }

    private void PreparePosPilotDesignData(
        string layoutPath,
        IEnumerable<FastReportPreviewRow> rows,
        IEnumerable<FastReportPreviewPayment> payments)
    {
        if (!string.Equals(Path.GetFileName(layoutPath), "Pos.frx", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rowsFilePath = GetPosPilotRowsFilePath();
        var paymentsFilePath = GetPosPilotPaymentsFilePath();
        WriteRowsCsv(rowsFilePath, rows);
        WritePaymentsCsv(paymentsFilePath, payments);
        SynchronizePosPilotTemplateDataSources(layoutPath, rowsFilePath, paymentsFilePath);
    }

    private async Task PrepareDesignTimeDataAsync(string layoutPath, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetFileName(layoutPath), "Pos.frx", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previewDocument = await LoadPreviewDocumentAsync(cancellationToken);
        var storeProfile = await LoadStoreProfileAsync(cancellationToken);
        PreparePosPilotDesignData(layoutPath, previewDocument.Righe, previewDocument.Pagamenti);

        using var report = new Report();
        report.Load(layoutPath);
        EnableRegisteredDataSources(report);
        EnsurePilotParameters(report);
        SynchronizePosPilotTemplateExpressions(report);
        EnsurePosPilotFooterObjects(report);
        BindPilotDataBand(report);
        ApplyPreviewLayoutValues(report, previewDocument, storeProfile);
        report.Save(layoutPath);
    }

    private void SynchronizePosPilotTemplateDataSources(string layoutPath, string rowsFilePath, string paymentsFilePath)
    {
        using var report = new Report();
        report.Load(layoutPath);

        if (!TryEnsureCsvDataSource(report, "RigheCsv", rowsFilePath, "Righe", "righe", SynchronizeRigheColumns))
        {
            CreatePosPilotTemplate(layoutPath);
            return;
        }

        if (!TryEnsureCsvDataSource(report, "ArticoloCsv", rowsFilePath, "Articolo", "articolo", SynchronizeRigheColumns))
        {
            CreatePosPilotTemplate(layoutPath);
            return;
        }

        if (!TryEnsureCsvDataSource(report, "PagamentiCsv", paymentsFilePath, "Pagamenti", "pagamenti", SynchronizePagamentiColumns))
        {
            CreatePosPilotTemplate(layoutPath);
            return;
        }

        EnsurePilotParameters(report);
        SynchronizePosPilotTemplateExpressions(report);
        EnsurePosPilotFooterObjects(report);
        BindPilotDataBand(report);
        report.Save(layoutPath);
    }

    private void CreatePosPilotTemplate(string layoutPath)
    {
        using var report = new Report();
        var sampleDocument = CreateSampleDocument();
        var rowsFilePath = GetPosPilotRowsFilePath();
        var paymentsFilePath = GetPosPilotPaymentsFilePath();
        WriteRowsCsv(rowsFilePath, sampleDocument.Righe);
        WritePaymentsCsv(paymentsFilePath, sampleDocument.Pagamenti);

        var rowsConnection = new CsvDataConnection
        {
            Name = "RigheCsv",
            CsvFile = rowsFilePath,
            Separator = ";",
            Codepage = 65001,
            FieldNamesInFirstString = true,
            ConvertFieldTypes = false
        };

        report.Dictionary.Connections.Add(rowsConnection);
        rowsConnection.CreateAllTables();

        var righeTable = report.Dictionary.AllObjects.OfType<TableDataSource>().FirstOrDefault();
        if (righeTable is not null)
        {
            righeTable.Name = "Righe";
            righeTable.Alias = "Righe";
            righeTable.TableName = "Righe";
            righeTable.Enabled = true;
            SynchronizeRigheColumns(righeTable);
        }

        var articoloConnection = new CsvDataConnection
        {
            Name = "ArticoloCsv",
            CsvFile = rowsFilePath,
            Separator = ";",
            Codepage = 65001,
            FieldNamesInFirstString = true,
            ConvertFieldTypes = false
        };

        report.Dictionary.Connections.Add(articoloConnection);
        articoloConnection.CreateAllTables();

        var articoloTable = articoloConnection.Tables.OfType<TableDataSource>().FirstOrDefault();
        if (articoloTable is not null)
        {
            articoloTable.Name = "Articolo";
            articoloTable.Alias = "Articolo";
            articoloTable.TableName = "Articolo";
            articoloTable.Enabled = true;
            SynchronizeRigheColumns(articoloTable);
        }

        var paymentsConnection = new CsvDataConnection
        {
            Name = "PagamentiCsv",
            CsvFile = paymentsFilePath,
            Separator = ";",
            Codepage = 65001,
            FieldNamesInFirstString = true,
            ConvertFieldTypes = false
        };

        report.Dictionary.Connections.Add(paymentsConnection);
        paymentsConnection.CreateAllTables();

        var pagamentiTable = report.Dictionary.AllObjects
            .OfType<TableDataSource>()
            .FirstOrDefault(table =>
                string.Equals(table.Name, "Pagamenti", StringComparison.OrdinalIgnoreCase)
                || string.Equals(table.TableName, "Pagamenti", StringComparison.OrdinalIgnoreCase)
                || table.Name.Contains("pagamenti", StringComparison.OrdinalIgnoreCase)
                || table.TableName.Contains("pagamenti", StringComparison.OrdinalIgnoreCase));

        if (pagamentiTable is not null)
        {
            pagamentiTable.Name = "Pagamenti";
            pagamentiTable.Alias = "Pagamenti";
            pagamentiTable.TableName = "Pagamenti";
            pagamentiTable.Enabled = true;
            SynchronizePagamentiColumns(pagamentiTable);
        }

        EnsurePilotParameters(report);

        var page = new ReportPage
        {
            Name = "Page1",
            PaperWidth = 80,
            PaperHeight = 310,
            LeftMargin = 4,
            RightMargin = 4,
            TopMargin = 4,
            BottomMargin = 4,
            Landscape = false
        };

        report.Pages.Add(page);

        var pageHeader = new PageHeaderBand
        {
            Name = "PageHeader1",
            Height = 202
        };
        page.PageHeader = pageHeader;

        AddPicture(pageHeader, "picStoreLogo", 25, 0, 220, 56);
        AddText(pageHeader, "txtBrandLine", "[Negozio.BrandLine]", 0, 58, 270, 12, 8, FontStyle.Bold, HorzAlign.Center);
        AddText(pageHeader, "txtRagioneSociale", "[Negozio.RagioneSociale]", 0, 68, 270, 18, 12, FontStyle.Bold, HorzAlign.Center);
        AddText(pageHeader, "txtIndirizzo", "[Negozio.IndirizzoCompleto]", 0, 84, 270, 16, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(pageHeader, "txtContatti", "[Negozio.ContattiCompleti]", 0, 98, 270, 16, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(pageHeader, "txtPiva", "[Negozio.PartitaIvaVisuale]", 0, 112, 270, 14, 8, FontStyle.Regular, HorzAlign.Center);

        AddText(pageHeader, "txtPagamentoLabel", "Pagamento", 0, 132, 80, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(pageHeader, "txtPagamentoValue", "[Testata.PagamentoLabel]", 88, 132, 100, 14, 10, FontStyle.Bold, HorzAlign.Left);
        AddText(pageHeader, "txtAnnoValue", "[Testata.AnnoVisuale]", 208, 132, 62, 14, 10, FontStyle.Bold, HorzAlign.Right);

        AddText(pageHeader, "txtDataLabel", "Data", 0, 150, 80, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(pageHeader, "txtDocumentoValue", "[Testata.DocumentoLabel]", 88, 150, 120, 14, 10, FontStyle.Bold, HorzAlign.Left);
        AddText(pageHeader, "txtDataValue", "[Testata.DataTesto]", 88, 166, 120, 14, 10, FontStyle.Bold, HorzAlign.Left);

        AddText(pageHeader, "txtNumeroLabel", "N.", 0, 168, 80, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(pageHeader, "txtNumeroValue", "[Testata.NumeroVisuale]", 88, 182, 120, 14, 10, FontStyle.Bold, HorzAlign.Left);

        AddText(pageHeader, "txtLineaSep1", "****************************************", 0, 198, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(pageHeader, "txtIntestazioneQuantita", "Q.t", 0, 208, 28, 12, 8, FontStyle.Bold, HorzAlign.Left);
        AddText(pageHeader, "txtIntestazioneDescrizione", "Descrizione", 34, 208, 138, 12, 8, FontStyle.Bold, HorzAlign.Left);
        AddText(pageHeader, "txtIntestazionePrezzo", "Prezzo", 178, 208, 38, 12, 7, FontStyle.Bold, HorzAlign.Right);
        AddText(pageHeader, "txtIntestazioneSconto", "Sc", 220, 208, 18, 12, 7, FontStyle.Bold, HorzAlign.Right);
        AddText(pageHeader, "txtIntestazioneTotale", "Totale", 242, 208, 28, 12, 7, FontStyle.Bold, HorzAlign.Right);

        var dataBand = new DataBand
        {
            Name = "DataRighe",
            Height = 42
        };
        page.Bands.Add(dataBand);

        var dataSource = report.GetDataSource("Righe");
        if (dataSource is not null)
        {
            dataSource.Enabled = true;
            dataBand.DataSource = dataSource;
        }

        AddText(dataBand, "txtQuantita", "[Righe.QuantitaVisuale]", 0, 0, 28, 12, 10, FontStyle.Regular, HorzAlign.Left);
        AddText(dataBand, "txtDescrizione", "[Righe.Descrizione]", 34, 0, 138, 24, 9, FontStyle.Bold, HorzAlign.Left, canGrow: true, wordWrap: true);
        AddText(dataBand, "txtPrezzoUnitario", "[Righe.PrezzoUnitarioVisuale]", 178, 0, 38, 12, 8, FontStyle.Regular, HorzAlign.Right);
        AddText(dataBand, "txtSconto", "[Righe.ScontoVisuale]", 220, 0, 18, 12, 8, FontStyle.Regular, HorzAlign.Right);
        AddText(dataBand, "txtImportoRiga", "[Righe.ImportoRigaVisuale]", 242, 0, 28, 12, 10, FontStyle.Bold, HorzAlign.Right);

        var reportSummary = new ReportSummaryBand
        {
            Name = "ReportSummary1",
            Height = 228
        };
        page.ReportSummary = reportSummary;

        AddText(reportSummary, "txtLineaSep2", "****************************************", 0, 0, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(reportSummary, "txtScontoPercentualeLabel", "Sconto %", 0, 14, 70, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtScontoPercentuale", "[Totali.ScontoPercentualeVisuale]", 74, 14, 34, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtTotaleLabel", "Totale", 150, 12, 60, 18, 13, FontStyle.Bold, HorzAlign.Right);
        AddText(reportSummary, "txtTotaleValore", "[Totali.TotaleDocumentoVisuale]", 214, 12, 56, 18, 13, FontStyle.Bold, HorzAlign.Right);
        AddText(reportSummary, "txtTotaleEuro", "[Totali.ValutaLabel]", 238, 28, 32, 16, 12, FontStyle.Bold, HorzAlign.Right);

        AddText(reportSummary, "txtScontoImportoLabel", "Sconto EUR", 0, 48, 92, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtScontoImporto", "[Totali.ScontoImportoVisuale]", 96, 48, 54, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtContantiLabel", "Contanti", 0, 64, 92, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtContantiImporto", "[Totali.ContantiVisuale]", 96, 64, 54, 14, 9, FontStyle.Regular, HorzAlign.Left);

        AddText(reportSummary, "txtPagamentoPrincipaleLabel", "[Totali.PagamentoPrincipaleLabel]", 176, 48, 48, 14, 9, FontStyle.Regular, HorzAlign.Right);
        AddText(reportSummary, "txtPagamentoPrincipaleImporto", "[Totali.PagamentoPrincipaleImportoVisuale]", 228, 48, 42, 14, 9, FontStyle.Regular, HorzAlign.Right);
        AddText(reportSummary, "txtRestoLabel", "Resto", 176, 64, 48, 16, 11, FontStyle.Bold, HorzAlign.Right);
        AddText(reportSummary, "txtRestoImporto", "[Totali.RestoVisuale]", 228, 64, 42, 16, 11, FontStyle.Bold, HorzAlign.Right);

        AddText(reportSummary, "txtCartaLabel", "Carta", 0, 80, 92, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtCartaImporto", "[Totali.PagatoCartaVisuale]", 96, 80, 54, 14, 9, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtLineaSep3", "****************************************", 0, 100, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(reportSummary, "txtClienteNome", "[Cliente.Nominativo]", 0, 114, 270, 18, 13, FontStyle.Bold, HorzAlign.Center);
        AddText(reportSummary, "txtCodiceCartaFedelta", "[Cliente.CodiceCartaFedelta]", 0, 260, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(reportSummary, "txtPuntiPrecedenti", "[Punti.PrecedentiLabel]", 0, 134, 120, 14, 8, FontStyle.Regular, HorzAlign.Left);
        AddText(reportSummary, "txtPuntiAttuali", "[Punti.AttualiLabel]", 164, 134, 106, 14, 8, FontStyle.Regular, HorzAlign.Right);

        AddText(reportSummary, "txtProgressivoVendita", "[Testata.ProgressivoVenditaLabel]", 0, 168, 270, 16, 9, FontStyle.Regular, HorzAlign.Center);
        AddText(reportSummary, "txtLineaSep4", "****************************************", 0, 186, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        AddText(reportSummary, "txtWebsite", "[Footer.Website]", 0, 200, 270, 18, 12, FontStyle.Bold, HorzAlign.Center);
        AddText(reportSummary, "txtGestionaleVendita", "[Footer.GestionaleLabel]", 0, 218, 270, 14, 9, FontStyle.Regular, HorzAlign.Center);
        AddText(reportSummary, "txtNoteFinali", "[Footer.NoteFinali]", 0, 292, 270, 28, 8, FontStyle.Regular, HorzAlign.Left, canGrow: true, wordWrap: true);
        AddBarcode(reportSummary, "barcodeFidelityCard", "[Cliente.CodiceCartaFedelta]", 45, 238, 180, 20);
        if (reportSummary.FindObject("txtCodiceCartaFedelta") is TextObject codiceCartaFedeltaText)
        {
            codiceCartaFedeltaText.Top = 260;
            codiceCartaFedeltaText.Height = 10;
        }
        if (reportSummary.FindObject("txtNoteFinali") is TextObject noteFinaliText)
        {
            noteFinaliText.Top = 292;
            noteFinaliText.Height = 28;
        }
        reportSummary.Height = 324;

        report.Save(layoutPath);
    }

    private static void EnsurePilotParameters(Report report)
    {
        RemoveLegacyFlatParameters(report);

        EnsureNestedStringParameter(report, "Negozio", "BrandLine");
        EnsureNestedStringParameter(report, "Negozio", "RagioneSociale");
        EnsureNestedStringParameter(report, "Negozio", "IndirizzoCompleto");
        EnsureNestedStringParameter(report, "Negozio", "ContattiCompleti");
        EnsureNestedStringParameter(report, "Negozio", "PartitaIvaVisuale");

        EnsureNestedStringParameter(report, "Testata", "PagamentoLabel");
        EnsureNestedStringParameter(report, "Testata", "DocumentoLabel");
        EnsureNestedStringParameter(report, "Testata", "AnnoVisuale");
        EnsureNestedStringParameter(report, "Testata", "DataTesto");
        EnsureNestedStringParameter(report, "Testata", "NumeroVisuale");
        EnsureNestedStringParameter(report, "Testata", "ProgressivoVenditaLabel");

        EnsureNestedStringParameter(report, "Totali", "TotaleDocumentoVisuale");
        EnsureNestedStringParameter(report, "Totali", "TotalePagatoVisuale");
        EnsureNestedStringParameter(report, "Totali", "ValutaLabel");
        EnsureNestedStringParameter(report, "Totali", "ScontoPercentualeVisuale");
        EnsureNestedStringParameter(report, "Totali", "ScontoImportoVisuale");
        EnsureNestedStringParameter(report, "Totali", "ContantiVisuale");
        EnsureNestedStringParameter(report, "Totali", "PagatoCartaVisuale");
        EnsureNestedStringParameter(report, "Totali", "PagamentoPrincipaleLabel");
        EnsureNestedStringParameter(report, "Totali", "PagamentoPrincipaleImportoVisuale");
        EnsureNestedStringParameter(report, "Totali", "RestoVisuale");

        EnsureNestedStringParameter(report, "Cliente", "Nominativo");
        EnsureNestedStringParameter(report, "Cliente", "CodiceCartaFedelta");
        EnsureNestedStringParameter(report, "Cliente", "PuntiPrecedentiLabel");
        EnsureNestedStringParameter(report, "Cliente", "PuntiAttualiLabel");

        EnsureNestedStringParameter(report, "Punti", "PrecedentiLabel");
        EnsureNestedStringParameter(report, "Punti", "AttualiLabel");
        EnsureNestedStringParameter(report, "Punti", "MaturatiVendita");
        EnsureNestedStringParameter(report, "Punti", "UtilizzatiVendita");

        EnsureNestedStringParameter(report, "Footer", "Website");
        EnsureNestedStringParameter(report, "Footer", "GestionaleLabel");
        EnsureNestedStringParameter(report, "Footer", "NoteFinali");
    }

    private static void EnsureNestedStringParameter(Report report, string groupName, string parameterName)
    {
        var group = EnsureGroupParameter(report, groupName);
        if (group.Parameters.FindByName(parameterName) is not null)
        {
            return;
        }

        var parameter = new Parameter(parameterName)
        {
            DataType = typeof(string),
            Value = string.Empty
        };

        group.Parameters.Add(parameter);
    }

    private static Parameter EnsureGroupParameter(Report report, string groupName)
    {
        if (report.Dictionary.Parameters.FindByName(groupName) is Parameter existingGroup)
        {
            return existingGroup;
        }

        var group = new Parameter(groupName)
        {
            DataType = typeof(string),
            Value = string.Empty
        };

        report.Dictionary.Parameters.Add(group);
        return group;
    }

    private static void RemoveLegacyFlatParameters(Report report)
    {
        string[] legacyParameterNames =
        [
            "StoreBrandLine",
            "StoreRagioneSociale",
            "StoreIndirizzoCompleto",
            "StoreContattiCompleti",
            "StorePartitaIvaVisuale",
            "TestataPagamentoLabel",
            "TestataDocumentoLabel",
            "TestataAnnoVisuale",
            "TestataDataTesto",
            "TestataNumeroVisuale",
            "TestataProgressivoVenditaLabel",
            "TotaliTotaleDocumentoVisuale",
            "TotaliValutaLabel",
            "TotaliScontoPercentualeVisuale",
            "TotaliScontoImportoVisuale",
            "TotaliContantiVisuale",
            "TotaliPagamentoPrincipaleLabel",
            "TotaliPagamentoPrincipaleImportoVisuale",
            "TotaliRestoVisuale",
            "ClienteNominativo",
            "ClientePuntiPrecedentiLabel",
            "ClientePuntiAttualiLabel",
            "FooterWebsite",
            "FooterGestionaleLabel",
            "FooterNoteFinali"
        ];

        foreach (var parameterName in legacyParameterNames)
        {
            if (report.Dictionary.Parameters.FindByName(parameterName) is Parameter legacyParameter)
            {
                report.Dictionary.Parameters.Remove(legacyParameter);
            }
        }
    }

    private static void SynchronizePosPilotTemplateExpressions(Report report)
    {
        EnsurePicture(report, "picStoreLogo", 25, 0, 220, 56);
        EnsureTextExpression(report, "txtBrandLine", "[Negozio.BrandLine]");
        EnsureTextExpression(report, "txtRagioneSociale", "[Negozio.RagioneSociale]");
        EnsureTextExpression(report, "txtIndirizzo", "[Negozio.IndirizzoCompleto]");
        EnsureTextExpression(report, "txtContatti", "[Negozio.ContattiCompleti]");
        EnsureTextExpression(report, "txtPiva", "[Negozio.PartitaIvaVisuale]");
        EnsureTextExpression(report, "txtPagamentoValue", "[Testata.PagamentoLabel]");
        EnsureTextExpression(report, "txtAnnoValue", "[Testata.AnnoVisuale]");
        EnsureTextExpression(report, "txtDocumentoValue", "[Testata.DocumentoLabel]");
        EnsureTextExpression(report, "txtDataValue", "[Testata.DataTesto]");
        EnsureTextExpression(report, "txtNumeroValue", "[Testata.NumeroVisuale]");
        EnsureTextExpression(report, "txtTotaleValore", "[Totali.TotaleDocumentoVisuale]");
        EnsureTextExpression(report, "txtTotaleEuro", "[Totali.ValutaLabel]");
        EnsureTextExpression(report, "txtScontoPercentuale", "[Totali.ScontoPercentualeVisuale]");
        EnsureTextExpression(report, "txtScontoImporto", "[Totali.ScontoImportoVisuale]");
        EnsureTextExpression(report, "txtContantiImporto", "[Totali.ContantiVisuale]");
        EnsureTextExpression(report, "txtCartaImporto", "[Totali.PagatoCartaVisuale]");
        EnsureTextExpression(report, "txtPagamentoPrincipaleLabel", "[Totali.PagamentoPrincipaleLabel]");
        EnsureTextExpression(report, "txtPagamentoPrincipaleImporto", "[Totali.PagamentoPrincipaleImportoVisuale]");
        EnsureTextExpression(report, "txtRestoImporto", "[Totali.RestoVisuale]");
        EnsureTextExpression(report, "txtClienteNome", "[Cliente.Nominativo]");
        EnsureTextExpression(report, "txtCodiceCartaFedelta", "[Cliente.CodiceCartaFedelta]");
        EnsureTextExpression(report, "txtPuntiPrecedenti", "[Punti.PrecedentiLabel]");
        EnsureTextExpression(report, "txtPuntiAttuali", "[Punti.AttualiLabel]");
        EnsureTextExpression(report, "txtProgressivoVendita", "[Testata.ProgressivoVenditaLabel]");
        EnsureTextExpression(report, "txtWebsite", "[Footer.Website]");
        EnsureTextExpression(report, "txtGestionaleVendita", "[Footer.GestionaleLabel]");
        EnsureTextExpression(report, "txtNoteFinali", "[Footer.NoteFinali]");
    }

    private static void EnsurePosPilotFooterObjects(Report report)
    {
        if (report.FindObject("ReportSummary1") is not ReportSummaryBand reportSummary)
        {
            return;
        }

        reportSummary.Height = Math.Max(reportSummary.Height, 324);
        EnsureText(reportSummary, "txtCartaLabel", "Carta", 0, 80, 92, 14, 9, FontStyle.Regular, HorzAlign.Left);
        EnsureText(reportSummary, "txtCartaImporto", "[Totali.PagatoCartaVisuale]", 96, 80, 54, 14, 9, FontStyle.Regular, HorzAlign.Left);
        EnsureText(reportSummary, "txtLineaSep3", "****************************************", 0, 100, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        EnsureText(reportSummary, "txtClienteNome", "[Cliente.Nominativo]", 0, 114, 270, 18, 13, FontStyle.Bold, HorzAlign.Center);
        EnsureText(reportSummary, "txtPuntiPrecedenti", "[Punti.PrecedentiLabel]", 0, 134, 120, 14, 8, FontStyle.Regular, HorzAlign.Left);
        EnsureText(reportSummary, "txtPuntiAttuali", "[Punti.AttualiLabel]", 164, 134, 106, 14, 8, FontStyle.Regular, HorzAlign.Right);
        EnsureText(reportSummary, "txtProgressivoVendita", "[Testata.ProgressivoVenditaLabel]", 0, 168, 270, 16, 9, FontStyle.Regular, HorzAlign.Center);
        EnsureText(reportSummary, "txtLineaSep4", "****************************************", 0, 186, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        EnsureText(reportSummary, "txtWebsite", "[Footer.Website]", 0, 200, 270, 18, 12, FontStyle.Bold, HorzAlign.Center);
        EnsureText(reportSummary, "txtGestionaleVendita", "[Footer.GestionaleLabel]", 0, 218, 270, 14, 9, FontStyle.Regular, HorzAlign.Center);
        EnsureBarcode(reportSummary, "barcodeFidelityCard", "[Cliente.CodiceCartaFedelta]", 45, 238, 180, 20);
        EnsureText(reportSummary, "txtCodiceCartaFedelta", "[Cliente.CodiceCartaFedelta]", 0, 260, 270, 10, 8, FontStyle.Regular, HorzAlign.Center);
        EnsureText(reportSummary, "txtNoteFinali", "[Footer.NoteFinali]", 0, 292, 270, 28, 8, FontStyle.Regular, HorzAlign.Left, canGrow: true, wordWrap: true);
    }

    private static void EnsureTextExpression(Report report, string objectName, string text)
    {
        if (report.FindObject(objectName) is TextObject textObject)
        {
            if (string.IsNullOrWhiteSpace(textObject.Text)
                || textObject.Text.Contains("DocumentoBanco.", StringComparison.Ordinal)
                || textObject.Text.Contains("[Ragionesociale]", StringComparison.Ordinal)
                || textObject.Text.Contains("[Bancanome]", StringComparison.Ordinal))
            {
                textObject.Text = text;
            }
        }
    }

    private static void EnsureText(
        BandBase parent,
        string name,
        string text,
        float left,
        float top,
        float width,
        float height,
        float fontSize,
        FontStyle fontStyle,
        HorzAlign horzAlign,
        bool canGrow = false,
        bool wordWrap = false)
    {
        if (parent.FindObject(name) is not TextObject textObject)
        {
            AddText(parent, name, text, left, top, width, height, fontSize, fontStyle, horzAlign, canGrow, wordWrap);
            return;
        }

        if (string.IsNullOrWhiteSpace(textObject.Text))
        {
            textObject.Text = text;
        }
    }

    private static void AddText(
        BandBase parent,
        string name,
        string text,
        float left,
        float top,
        float width,
        float height,
        float fontSize,
        FontStyle fontStyle,
        HorzAlign horzAlign,
        bool canGrow = false,
        bool wordWrap = false)
    {
        var textObject = new TextObject
        {
            Name = name,
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Font = new Font("Consolas", fontSize, fontStyle),
            HorzAlign = horzAlign,
            CanGrow = canGrow,
            WordWrap = wordWrap
        };

        parent.Objects.Add(textObject);
    }

    private static void EnsurePicture(
        Report report,
        string name,
        float left,
        float top,
        float width,
        float height)
    {
        if (report.FindObject(name) is not PictureObject pictureObject)
        {
            if (report.Pages.Cast<Base>().OfType<ReportPage>().FirstOrDefault() is ReportPage page && page.PageHeader is not null)
            {
                AddPicture(page.PageHeader, name, left, top, width, height);
            }

            return;
        }

        pictureObject.SizeMode = PictureBoxSizeMode.Zoom;
        pictureObject.CanGrow = false;
    }

    private static void AddPicture(
        BandBase parent,
        string name,
        float left,
        float top,
        float width,
        float height)
    {
        var pictureObject = new PictureObject
        {
            Name = name,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            SizeMode = PictureBoxSizeMode.Zoom,
            CanGrow = false
        };

        parent.Objects.Add(pictureObject);
    }

    private static void EnsureBarcode(
        BandBase parent,
        string name,
        string expression,
        float left,
        float top,
        float width,
        float height)
    {
        if (parent.FindObject(name) is not BarcodeObject barcodeObject)
        {
            AddBarcode(parent, name, expression, left, top, width, height);
            return;
        }

        if (string.IsNullOrWhiteSpace(barcodeObject.Expression))
        {
            barcodeObject.Expression = expression;
        }
        barcodeObject.AutoSize = false;
        barcodeObject.ShowText = false;
        barcodeObject.HideIfNoData = true;
        barcodeObject.Padding = new Padding(0);
        barcodeObject.Barcode = new Barcode128();
    }

    private static void AddBarcode(
        BandBase parent,
        string name,
        string expression,
        float left,
        float top,
        float width,
        float height)
    {
        var barcodeObject = new BarcodeObject
        {
            Name = name,
            Expression = expression,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            AutoSize = false,
            ShowText = false,
            HideIfNoData = true,
            Padding = new Padding(0),
            Barcode = new Barcode128()
        };

        parent.Objects.Add(barcodeObject);
    }

    private string GetPosPilotRowsFilePath()
    {
        return Path.Combine(_pathService.GetDesignDataDirectory(), "Pos.righe.csv");
    }

    private string GetPosPilotPaymentsFilePath()
    {
        return Path.Combine(_pathService.GetDesignDataDirectory(), "Pos.pagamenti.csv");
    }

    private static void WriteRowsCsv(string dataFilePath, IEnumerable<FastReportPreviewRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dataFilePath)!);

        using var writer = new StreamWriter(dataFilePath, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(';', PosPilotRowColumns.Select(column => column.Name)));

        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(
                ';',
                PosPilotRowColumns.Select(column => EscapeCsv(column.GetValue(row)))));
        }
    }

    private static void WritePaymentsCsv(string dataFilePath, IEnumerable<FastReportPreviewPayment> payments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dataFilePath)!);

        using var writer = new StreamWriter(dataFilePath, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(';', PosPilotPaymentColumns.Select(column => column.Name)));

        foreach (var payment in payments)
        {
            writer.WriteLine(string.Join(
                ';',
                PosPilotPaymentColumns.Select(column => EscapeCsv(column.GetValue(payment)))));
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static DataTable BuildRowsDataTable(IEnumerable<FastReportPreviewRow> rows)
    {
        var table = new DataTable("Righe");
        foreach (var column in PosPilotRowColumns)
        {
            table.Columns.Add(column.Name, column.DataType);
        }

        foreach (var row in rows)
        {
            table.Rows.Add(PosPilotRowColumns.Select(column => column.GetTypedValue(row)).ToArray());
        }

        return table;
    }

    private static void SynchronizeRigheColumns(TableDataSource righeTable)
    {
        var expectedColumns = PosPilotRowColumns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        var currentColumns = righeTable.Columns.Cast<Column>().ToArray();

        foreach (var column in currentColumns)
        {
            if (!expectedColumns.ContainsKey(column.Name))
            {
                righeTable.RemoveChild(column);
            }
        }

        foreach (var definition in PosPilotRowColumns)
        {
            var column = righeTable.Columns.Cast<Column>()
                .FirstOrDefault(item => string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase));

            if (column is null)
            {
                column = new Column();
                righeTable.AddChild(column);
            }

            column.Name = definition.Name;
            column.Alias = definition.Name;
            column.PropName = definition.Name;
            column.DataType = definition.DataType;
        }
    }

    private static void SynchronizePagamentiColumns(TableDataSource pagamentiTable)
    {
        var expectedColumns = PosPilotPaymentColumns.ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
        var currentColumns = pagamentiTable.Columns.Cast<Column>().ToArray();

        foreach (var column in currentColumns)
        {
            if (!expectedColumns.ContainsKey(column.Name))
            {
                pagamentiTable.RemoveChild(column);
            }
        }

        foreach (var definition in PosPilotPaymentColumns)
        {
            var column = pagamentiTable.Columns.Cast<Column>()
                .FirstOrDefault(item => string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase));

            if (column is null)
            {
                column = new Column();
                pagamentiTable.AddChild(column);
            }

            column.Name = definition.Name;
            column.Alias = definition.Name;
            column.PropName = definition.Name;
            column.DataType = definition.DataType;
        }
    }

    private static bool TryEnsureCsvDataSource(
        Report report,
        string connectionName,
        string csvFilePath,
        string tableName,
        string fallbackToken,
        Action<TableDataSource> synchronizeColumns)
    {
        var connection = report.Dictionary.Connections
            .OfType<CsvDataConnection>()
            .FirstOrDefault(item => string.Equals(item.Name, connectionName, StringComparison.OrdinalIgnoreCase));

        if (connection is null)
        {
            connection = new CsvDataConnection();
            report.Dictionary.Connections.Add(connection);
        }

        connection.Name = connectionName;
        connection.CsvFile = csvFilePath;
        connection.Separator = ";";
        connection.Codepage = 65001;
        connection.FieldNamesInFirstString = true;
        connection.ConvertFieldTypes = false;
        connection.CreateAllTables();

        var table = report.Dictionary.AllObjects
            .OfType<TableDataSource>()
            .FirstOrDefault(item =>
                string.Equals(item.Name, tableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TableName, tableName, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(fallbackToken, StringComparison.OrdinalIgnoreCase)
                || item.TableName.Contains(fallbackToken, StringComparison.OrdinalIgnoreCase)
                || item.Alias.Contains(fallbackToken, StringComparison.OrdinalIgnoreCase));

        if (table is null)
        {
            table = connection.Tables.OfType<TableDataSource>().FirstOrDefault();
        }

        if (table is null)
        {
            table = new TableDataSource
            {
                Connection = connection
            };

            connection.Tables.Add(table);
        }

        table.Name = tableName;
        table.Alias = tableName;
        table.TableName = tableName;
        table.Enabled = true;
        synchronizeColumns(table);
        return true;
    }

    private sealed record PosPilotRowColumnDefinition(string Name, Type DataType, Func<FastReportPreviewRow, object?> GetTypedValue, Func<FastReportPreviewRow, string> GetValue);
    private sealed record PosPilotPaymentColumnDefinition(string Name, Type DataType, Func<FastReportPreviewPayment, object?> GetTypedValue, Func<FastReportPreviewPayment, string> GetValue);

    private static readonly PosPilotRowColumnDefinition[] PosPilotRowColumns =
    [
        new("RigaOid", typeof(int), row => row.RigaOid, row => row.RigaOid.ToString()),
        new("CodiceArticolo", typeof(string), row => row.CodiceArticolo, row => row.CodiceArticolo),
        new("Barcode", typeof(string), row => row.Barcode, row => row.Barcode),
        new("Descrizione", typeof(string), row => row.Descrizione, row => row.Descrizione),
        new("OrdineRiga", typeof(int), row => row.OrdineRiga, row => row.OrdineRiga.ToString()),
        new("Quantita", typeof(decimal), row => row.Quantita, row => row.Quantita.ToString("0.##")),
        new("UnitaMisura", typeof(string), row => row.UnitaMisura, row => row.UnitaMisura),
        new("PrezzoUnitario", typeof(decimal), row => row.PrezzoUnitario, row => row.PrezzoUnitario.ToString("0.00")),
        new("ScontoPercentuale", typeof(decimal), row => row.ScontoPercentuale, row => row.ScontoPercentuale.ToString("0.##")),
        new("Sconto2", typeof(decimal), row => row.Sconto2, row => row.Sconto2.ToString("0.##")),
        new("ImportoRiga", typeof(decimal), row => row.ImportoRiga, row => row.ImportoRiga.ToString("0.00")),
        new("AliquotaIva", typeof(decimal), row => row.AliquotaIva, row => row.AliquotaIva.ToString("0.##")),
        new("QuantitaVisuale", typeof(string), row => row.QuantitaVisuale, row => row.QuantitaVisuale),
        new("PrezzoUnitarioVisuale", typeof(string), row => row.PrezzoUnitarioVisuale, row => row.PrezzoUnitarioVisuale),
        new("ScontoVisuale", typeof(string), row => row.ScontoVisuale, row => row.ScontoVisuale),
        new("Sconto2Visuale", typeof(string), row => row.Sconto2Visuale, row => row.Sconto2Visuale),
        new("ImportoRigaVisuale", typeof(string), row => row.ImportoRigaVisuale, row => row.ImportoRigaVisuale)
    ];

    private static readonly PosPilotPaymentColumnDefinition[] PosPilotPaymentColumns =
    [
        new("Tipo", typeof(string), payment => payment.Tipo, payment => payment.Tipo),
        new("Importo", typeof(decimal), payment => payment.Importo, payment => payment.Importo.ToString("0.00")),
        new("ImportoVisuale", typeof(string), payment => payment.ImportoVisuale, payment => payment.ImportoVisuale)
    ];

    private static void OpenWithShell(string path, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            startInfo.Arguments = arguments;
        }

        Process.Start(startInfo);
    }

    private string? FindCommunityDesignerPath()
    {
        var candidatePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "FastReport", "Designer.exe"),
            Path.Combine(AppContext.BaseDirectory, "FastReport", "FastReport.Designer.Community.exe"),
            @"C:\Program Files\FastReport Community Designer\FastReport.Designer.Community.exe",
            @"C:\Program Files (x86)\FastReport Community Designer\FastReport.Designer.Community.exe",
            @"C:\Program Files\FastReport\FastReport.Designer.Community.exe",
            @"C:\Program Files (x86)\FastReport\FastReport.Designer.Community.exe",
            @"C:\Program Files\FastReports\FastReport.Designer.Community.exe",
            @"C:\Program Files (x86)\FastReports\FastReport.Designer.Community.exe"
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidatePaths.Add(Path.Combine(directory.FullName, "FastReport", "Designer.exe"));
            candidatePaths.Add(Path.Combine(directory.FullName, "FastReport", "FastReport.Designer.Community.exe"));
            directory = directory.Parent;
        }

        return candidatePaths.FirstOrDefault(File.Exists);
    }

    private async Task<FastReportPreviewDocument> LoadPreviewDocumentAsync(CancellationToken cancellationToken)
    {
        return await _previewDataService.GetPreviewDocumentAsync(cancellationToken);
    }

    private async Task<FastReportStoreProfile> LoadStoreProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _storeProfileService.GetStoreProfileAsync(cancellationToken);
        }
        catch
        {
            return new FastReportStoreProfile
            {
                RagioneSociale = "SVAPOBAT",
                Indirizzo = "Corso Vittorio Emanuele 90",
                Cap = "76121",
                Citta = "Barletta",
                Provincia = "BT",
                PartitaIva = "IT07003640724",
                Telefono = "340 56 15 907",
                Email = "info@svapobat.it",
                RiferimentoScontrino = "*** WWW.SVAPOBAT.IT ***"
            };
        }
    }

    private static string BuildWebsiteReference(string riferimentoScontrino)
    {
        if (string.IsNullOrWhiteSpace(riferimentoScontrino))
        {
            return "VISITA WWW.SVAPOBAT.IT";
        }

        var normalized = riferimentoScontrino.Replace("*", string.Empty, StringComparison.Ordinal).Trim();
        if (normalized.StartsWith("VISITA ", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return normalized.StartsWith("WWW.", StringComparison.OrdinalIgnoreCase)
            ? $"VISITA {normalized}"
            : normalized;
    }

    private static void ApplyStoreLogo(Report report)
    {
        if (report.FindObject("picStoreLogo") is not PictureObject pictureObject)
        {
            return;
        }

        var logoPath = FindStoreLogoPath();
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
        {
            pictureObject.Visible = false;
            return;
        }

        pictureObject.ImageLocation = logoPath;
        pictureObject.Visible = true;
    }

    private static string? FindStoreLogoPath()
    {
        var candidatePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "Immagini", "Logo-svapobat.png"),
            Path.Combine(AppContext.BaseDirectory, "Banco.UI.Wpf", "Immagini", "Logo-svapobat.png")
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidatePaths.Add(Path.Combine(directory.FullName, "Immagini", "Logo-svapobat.png"));
            candidatePaths.Add(Path.Combine(directory.FullName, "Banco.UI.Wpf", "Immagini", "Logo-svapobat.png"));
            directory = directory.Parent;
        }

        return candidatePaths.FirstOrDefault(File.Exists);
    }

    private static FastReportPreviewDocument CreateSampleDocument()
    {
        var rows = new[]
        {
            new FastReportPreviewRow
            {
                RigaOid = 1,
                CodiceArticolo = "PD0120_1",
                Barcode = "805000000001",
                Descrizione = "Liquido demo menta 10 ml",
                OrdineRiga = 1,
                Quantita = 2,
                UnitaMisura = "PZ",
                PrezzoUnitario = 4.50m,
                ScontoPercentuale = 0m,
                ImportoRiga = 9.00m,
                AliquotaIva = 22m,
                QuantitaVisuale = "2",
                PrezzoUnitarioVisuale = "x 4,50",
                ScontoVisuale = string.Empty,
                ImportoRigaVisuale = "9,00"
            },
            new FastReportPreviewRow
            {
                RigaOid = 2,
                CodiceArticolo = "ACC-001",
                Barcode = "805000000002",
                Descrizione = "Accessorio demo",
                OrdineRiga = 2,
                Quantita = 1,
                UnitaMisura = "PZ",
                PrezzoUnitario = 2.50m,
                ScontoPercentuale = 0m,
                ImportoRiga = 2.50m,
                AliquotaIva = 22m,
                QuantitaVisuale = string.Empty,
                PrezzoUnitarioVisuale = string.Empty,
                ScontoVisuale = string.Empty,
                ImportoRigaVisuale = string.Empty
            }
        };

        return new FastReportPreviewDocument
        {
            Testata = new FastReportPreviewHeader
            {
                DocumentoOid = 1001,
                Numero = 101,
                Anno = DateTime.Today.Year,
                Data = DateTime.Today,
                EtichettaDocumento = "Cortesia",
                ModelloDocumento = "Banco 80 mm",
                Operatore = "Admin",
                StatoRuntime = "Pubblicato Banco",
                NumeroCompleto = $"101/{DateTime.Today.Year}",
                DataTesto = DateTime.Today.ToString("dd/MM/yyyy"),
                PagamentoLabel = "Buoni",
                DocumentoLabel = "Vendita al banco",
                AnnoVisuale = DateTime.Today.Year.ToString(),
                ProgressivoVenditaLabel = $"Progressivo vendita 101/{DateTime.Today.Year}"
            },
            Cliente = new FastReportPreviewCustomer
            {
                ClienteOid = 1,
                Nominativo = "Cliente generico",
                Indirizzo = "Corso Vittorio Emanuele 90",
                Cap = "76121",
                Citta = "Barletta",
                Provincia = "BT",
                PartitaIva = "IT07003640724",
                CodiceFiscale = "RSSMRA80A01H501Z",
                Telefono = "340 56 15 907",
                Email = "info@svapobat.it",
                CodiceCartaFedelta = "8051234500001",
                IndirizzoCompleto = "Corso Vittorio Emanuele 90 - 76121 Barletta (BT)",
                ContattiCompleti = "Tel. 340 56 15 907 - Email info@svapobat.it",
                FiscaleCompleto = "IT07003640724 - RSSMRA80A01H501Z",
                PuntiPrecedentiVisuale = "n.d.",
                PuntiAttualiVisuale = "0"
            },
            Righe = rows,
            Pagamenti =
            [
                new FastReportPreviewPayment
                {
                    Tipo = "Contanti",
                    Importo = 11.50m,
                    ImportoVisuale = "11,50"
                }
            ],
            Totali = new FastReportPreviewTotals
            {
                TotaleDocumento = rows.Sum(item => item.ImportoRiga),
                TotaleImponibile = 9.43m,
                TotaleIva = 2.07m,
                TotalePagato = 11.50m,
                TotaleDocumentoVisuale = "11,50",
                TotalePagatoVisuale = "11,50",
                ScontoPercentualeVisuale = "0",
                ScontoImportoVisuale = "0,00 EUR",
                ContantiVisuale = "0,00 EUR",
                PagatoCartaVisuale = "0,00 EUR",
                PagamentoPrincipaleLabel = "Buoni",
                PagamentoPrincipaleImportoVisuale = "11,50 EUR",
                RestoVisuale = "0,00 EUR"
            }
        };
    }

    private static FastReportRuntimeActionResult BuildFailureResult(
        string actionLabel,
        string layoutFileName,
        Exception exception)
    {
        return new FastReportRuntimeActionResult
        {
            IsSupported = true,
            Succeeded = false,
            Message = $"{actionLabel} FastReport fallita per '{layoutFileName}': {exception.Message}"
        };
    }
}
