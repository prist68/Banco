using Avalonia.Controls;
using Avalonia.Interactivity;
using Banco.Core.Infrastructure;
using Banco.Core.LocalStore;
using Banco.UI.Avalonia.Banco.Services;
using Banco.UI.Avalonia.Banco.ViewModels;
using Banco.UI.Avalonia.Banco.Views;
using Banco.UI.Avalonia.Lab.DesignSystem;
using Banco.UI.Avalonia.Lab.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Fiscal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.UI.Avalonia.Lab;

public sealed partial class MainWindow : Window
{
    private BancoAvaloniaThemeKind _currentTheme = BancoAvaloniaThemeKind.Light;
    private ServiceProvider? _bancoServiceProvider;
    private BancoSaleView? _bancoSaleView;
    private DesktopPrototypeViewModel ViewModel => (DesktopPrototypeViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new DesktopPrototypeViewModel();
        ApplyTheme(BancoAvaloniaThemeKind.Light);
    }

    private void ThemeToggle_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme == BancoAvaloniaThemeKind.Light
            ? BancoAvaloniaThemeKind.Dark
            : BancoAvaloniaThemeKind.Light;
        ApplyTheme(_currentTheme);
    }

    private void ApplyTheme(BancoAvaloniaThemeKind theme)
    {
        BancoAvaloniaTheme.Apply(this, theme);
        ThemeGlyph.Text = BancoAvaloniaTheme.GetToggleLabel(theme);
    }

    private void AddWidget_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string widgetId })
        {
            ViewModel.AddWidget(widgetId);
        }
    }

    private void CloseWidget_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DashboardWidget widget })
        {
            ViewModel.CloseWidget(widget);
        }
    }

    private void MoveWidgetUp_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DashboardWidget widget })
        {
            ViewModel.MoveWidgetUp(widget);
        }
    }

    private void MoveWidgetDown_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DashboardWidget widget })
        {
            ViewModel.MoveWidgetDown(widget);
        }
    }

    private void ResetWidgets_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.ResetWidgets();
    }

    private void OpenAvaloniaBanco_OnClick(object? sender, RoutedEventArgs e)
    {
        _bancoSaleView ??= new BancoSaleView(GetBancoServiceProvider().GetRequiredService<BancoSaleViewModel>());
        InternalWorkspaceHost.Content = _bancoSaleView;
        InternalWorkspace.IsVisible = true;
    }

    private void CloseInternalWorkspace_OnClick(object? sender, RoutedEventArgs e)
    {
        InternalWorkspace.IsVisible = false;
        InternalWorkspaceHost.Content = null;
    }

    private ServiceProvider GetBancoServiceProvider()
    {
        if (_bancoServiceProvider is not null)
        {
            return _bancoServiceProvider;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(_ => BuildBancoConfiguration());
        services.AddInfrastructureServices();
        services.AddLocalStoreServices();
        services.AddSingleton<IBancoDocumentWorkflowService, BancoDocumentWorkflowService>();
        services.AddSingleton<IPointsRewardRuleService, AvaloniaPointsRewardRuleService>();
        services.AddSingleton<IPointsCustomerBalanceService, AvaloniaPointsCustomerBalanceService>();
        services.AddSingleton<IBancoSalePrintService, UnsupportedBancoSalePrintService>();
        services.AddSingleton<BancoSaleDataFacade>();
        services.AddSingleton<BancoSaleViewModel>();
        _bancoServiceProvider = services.BuildServiceProvider();
        return _bancoServiceProvider;
    }

    private static IConfiguration BuildBancoConfiguration()
    {
        var bancoProjectDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../Banco.UI.Avalonia.Banco"));

        return new ConfigurationBuilder()
            .SetBasePath(bancoProjectDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
    }
}
