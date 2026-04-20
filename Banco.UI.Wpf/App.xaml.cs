using System.Windows;
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
using Banco.UI.Wpf.Services;
using Banco.UI.Wpf.PosModule;
using Banco.UI.Wpf.Shell;
using Banco.UI.Wpf.ViewModels;
using Banco.UI.Wpf.WinEcrModule;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Banco.UI.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
                services.AddStampaServices();

                services.AddSingleton<BackupImportDialogService>();
                services.AddSingleton<IBancoDocumentWorkflowService, BancoDocumentWorkflowService>();
                services.AddSingleton<INavigationRegistry, ShellNavigationRegistry>();
                services.AddSingleton<SidebarHostViewModel>();
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<BancoViewModel>();
                services.AddSingleton<ReorderListViewModel>();
                services.AddSingleton<MagazzinoArticleViewModel>();
                services.AddSingleton<DocumentListViewModel>();
                services.AddSingleton<UIDocumentListHostViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<BackupImportViewModel>();
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

        var configurationService = _host.Services.GetRequiredService<IApplicationConfigurationService>();
        var settings = await configurationService.LoadAsync();
        var posProcessLogService = _host.Services.GetRequiredService<IPosProcessLogService>();
        posProcessLogService.Info("APP", "Avvio applicazione completato. Bootstrap host inizializzato.");
        posProcessLogService.Info("APP", $"Configurazione POS: {settings.PosIntegration.PosIpAddress}:{settings.PosIntegration.PosPort}");
        posProcessLogService.Info("APP", $"Configurazione WinEcr: {settings.WinEcrIntegration.AutoRunCommandFilePath} -> {settings.WinEcrIntegration.AutoRunErrorFilePath}");

        var localStoreBootstrapper = _host.Services.GetRequiredService<ILocalStoreBootstrapper>();
        await localStoreBootstrapper.InitializeAsync(settings.LocalStore);
        posProcessLogService.Info("APP", $"LocalStore inizializzato in {settings.LocalStore.DatabasePath}");

        // Ripristina la palette colori righe griglia scelta dall'utente.
        BancoGrigliaColoriService.CaricaEApplica();
        BancoGridHeaderColorService.CaricaEApplica("Banco");
        BancoGridHeaderColorService.CaricaEApplica("Documenti");
        BancoGridHeaderColorService.CaricaEApplica("Riordino");

        var shellWindow = _host.Services.GetRequiredService<ShellWindow>();
        shellWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var posProcessLogService = _host.Services.GetService<IPosProcessLogService>();
            posProcessLogService?.Info("APP", "Chiusura applicazione richiesta.");
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
