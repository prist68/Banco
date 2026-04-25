using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Banco.Core.Infrastructure;
using Banco.Core.LocalStore;
using Banco.UI.Avalonia.Banco.Services;
using Banco.UI.Avalonia.Banco.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Fiscal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Banco.UI.Avalonia.Banco;

public sealed partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current!)._host?.Services ?? throw new InvalidOperationException("Host applicativo non inizializzato.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = BuildHost();
            await _host.StartAsync();
            desktop.MainWindow = _host.Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.SetBasePath(AppContext.BaseDirectory);
                configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices(services =>
            {
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                });

                services.AddInfrastructureServices();
                services.AddLocalStoreServices();
                services.AddSingleton<IBancoDocumentWorkflowService, BancoDocumentWorkflowService>();
                services.AddSingleton<IPointsRewardRuleService, AvaloniaPointsRewardRuleService>();
                services.AddSingleton<IPointsCustomerBalanceService, AvaloniaPointsCustomerBalanceService>();
                services.AddSingleton<IBancoSalePrintService, UnsupportedBancoSalePrintService>();
                services.AddSingleton<BancoSaleDataFacade>();
                services.AddSingleton<BancoSaleViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
}
