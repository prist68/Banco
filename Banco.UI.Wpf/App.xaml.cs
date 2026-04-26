using System.Windows;
using Banco.AI;
using Banco.Backup;
using Banco.Backup.ViewModels;
using Banco.Magazzino.ViewModels;
using Banco.Core.Contracts.Navigation;
using Banco.Stampa;
using Banco.Sidebar.ViewModels;
using Banco.Punti.Services;
using Banco.Punti.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Fiscal;
using Banco.Core.Infrastructure;
using Banco.Core.LocalStore;
using Banco.UI.Shared.Selection;
using Banco.UI.Shared.Services;
using Banco.UI.Wpf.DesktopModule;
using Banco.UI.Wpf.DashboardModule;
using Banco.UI.Wpf.ArchiveSettingsModule;
using Banco.UI.Wpf.PosModule;
using Banco.UI.Wpf.Shell;
using Banco.UI.Wpf.Services;
using Banco.UI.Wpf.ViewModels;
using Banco.UI.Wpf.WinEcrModule;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows.Threading;
using Banco.Vendita.Configuration;

namespace Banco.UI.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private bool _isShuttingDown;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services ?? throw new InvalidOperationException("Host applicativo non inizializzato.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.SetBasePath(AppContext.BaseDirectory);
                configurationBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                });

                services.AddInfrastructureServices();
                services.AddLocalStoreServices();
                services.AddAiServices();
                services.AddBackupServices();
                services.AddStampaServices();

                services.AddSingleton<BackupImportDialogService>();
                services.AddSingleton<IBackupDialogService>(sp => sp.GetRequiredService<BackupImportDialogService>());
                services.AddSingleton<IBancoDocumentWorkflowService, BancoDocumentWorkflowService>();
                services.AddSingleton<INavigationRegistry, ShellNavigationRegistry>();
                services.AddSingleton<SidebarHostViewModel>();
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<DesktopHomeViewModel>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<BancoViewModel>();
                services.AddSingleton<ReorderListViewModel>();
                services.AddSingleton<ArticleManagementViewModel>();
                services.AddTransient<ArticleImageManagementViewModel>();
                services.AddTransient<ArticleSecondaryCategoryManagementViewModel>();
                services.AddSingleton<MagazzinoArticleViewModel>();
                services.AddSingleton<DocumentListViewModel>();
                services.AddSingleton<UIDocumentListHostViewModel>();
                services.AddSingleton<ArchiveGeneralSettingsViewModel>();
                services.AddSingleton<SqliteSettingsViewModel>();
                services.AddSingleton<ArchiveSettingsHostViewModel>();
                services.AddSingleton<FastReportStudioViewModel>();
                services.AddSingleton<PosConfigurationViewModel>();
                services.AddSingleton<WinEcrConfigurationViewModel>();
                services.AddSingleton<DiagnosticsViewModel>();
                services.AddSingleton<ThemeManagementViewModel>();
                services.AddSingleton<IGestionalePointsPromotionEligibilityService, GestionalePointsPromotionEligibilityService>();
                services.AddSingleton<IPointsRewardRuleService, PointsRewardRuleService>();
                services.AddSingleton<IPointsCustomerBalanceService, PointsCustomerBalanceService>();
                services.AddSingleton<IPointsPromotionEvaluationService, PointsPromotionEvaluationService>();
                services.AddSingleton<IPointsPromotionDocumentService, PointsPromotionDocumentService>();
                services.AddSingleton<IPointsPromotionHistoryService, PointsPromotionHistoryService>();
                services.AddSingleton<PuntiViewModel>();
                services.AddSingleton<ShellWindow>();
            })
            .Build();

        await _host.StartAsync();

        if (e.Args.Any(argument => string.Equals(argument, BackupCommandLineArguments.RunScheduledBackup, StringComparison.OrdinalIgnoreCase)))
        {
            await RunScheduledBackupModeAsync();
            return;
        }

        var configurationService = _host.Services.GetRequiredService<IApplicationConfigurationService>();
        var settings = await configurationService.LoadAsync();
        var posProcessLogService = _host.Services.GetRequiredService<IPosProcessLogService>();
        posProcessLogService.Info("APP", "Avvio applicazione completato. Bootstrap host inizializzato.");
        posProcessLogService.Info("APP", $"Configurazione POS: {settings.PosIntegration.PosIpAddress}:{settings.PosIntegration.PosPort}");
        posProcessLogService.Info("APP", $"Configurazione WinEcr: {settings.WinEcrIntegration.AutoRunCommandFilePath} -> {settings.WinEcrIntegration.AutoRunErrorFilePath}");

        var localStoreBootstrapper = _host.Services.GetRequiredService<ILocalStoreBootstrapper>();
        await localStoreBootstrapper.InitializeAsync(settings.LocalStore);
        posProcessLogService.Info("APP", $"LocalStore inizializzato in {settings.LocalStore.DatabasePath}");

        var legacyReceiptAlignmentService = _host.Services.GetRequiredService<ILegacyReceiptAlignmentService>();
        var alignedReceipts = await legacyReceiptAlignmentService.AlignHistoricalReceiptsAsync();
        posProcessLogService.Info("APP", $"Riallineamento storico scontrini FM completato. Documenti aggiornati: {alignedReceipts}.");

        // Ripristina la palette colori righe griglia scelta dall'utente.
        BancoGrigliaColoriService.CaricaEApplica();
        BancoGridHeaderColorService.CaricaEApplica("Banco");
        BancoGridHeaderColorService.CaricaEApplica("Documenti");
        BancoGridHeaderColorService.CaricaEApplica("Riordino");

        // L'effetto luce corsa sulla griglia Banco causava crash WPF nella build installata
        // durante il cambio scheda: manteniamo il bordo/static highlight ma senza animazione.
        SelezioneLuceCorsaService.AnimazioneAbilitata = false;

        var shellWindow = _host.Services.GetRequiredService<ShellWindow>();
        shellWindow.Show();
    }

    private async Task RunScheduledBackupModeAsync()
    {
        var logService = _host!.Services.GetRequiredService<IPosProcessLogService>();
        logService.Info("ScheduledBackupRunner", "Avvio modalita` pianificata: esecuzione backup senza apertura shell.");

        try
        {
            var backupExportService = _host.Services.GetRequiredService<IGestionaleBackupExportService>();
            var result = await backupExportService.ExportAsync();
            logService.Info("ScheduledBackupRunner", $"Backup pianificato completato correttamente. File={result.BackupFilePath}.");
            Shutdown(0);
        }
        catch (Exception ex)
        {
            logService.Error("ScheduledBackupRunner", "Errore durante l'esecuzione pianificata del backup.", ex);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _isShuttingDown = true;

        if (_host is not null)
        {
            var posProcessLogService = _host.Services.GetService<IPosProcessLogService>();
            posProcessLogService?.Info("APP", "Chiusura applicazione richiesta.");
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!_isShuttingDown)
        {
            return;
        }

        if (e.Exception is not ArgumentNullException argumentNullException ||
            !string.Equals(argumentNullException.ParamName, "defaultDestinationValue", StringComparison.Ordinal))
        {
            return;
        }

        var posProcessLogService = _host?.Services.GetService<IPosProcessLogService>();
        posProcessLogService?.Warning(
            "APP",
            "Eccezione WPF in teardown ignorata durante la chiusura: defaultDestinationValue nullo nel framework.");
        e.Handled = true;
    }
}
