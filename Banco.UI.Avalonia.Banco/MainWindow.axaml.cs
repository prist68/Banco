using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Banco.UI.Avalonia.Banco.DesignSystem;
using Banco.UI.Avalonia.Banco.ViewModels;
using Banco.UI.Avalonia.Banco.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Banco.UI.Avalonia.Banco;

public sealed partial class MainWindow : Window
{
    private BancoSaleThemeKind _theme = BancoSaleThemeKind.Light;
    private readonly StringBuilder _articleSearchInputBuffer = new();
    private DateTime _articleSearchFirstInputAtUtc;
    private DateTime _articleSearchLastInputAtUtc;

    public MainWindow()
    {
        InitializeComponent();
        BancoSalePalette.Apply(this, _theme);
    }

    public MainWindow(BancoSaleViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private void ThemeToggle_OnClick(object? sender, RoutedEventArgs e)
    {
        _theme = _theme == BancoSaleThemeKind.Light
            ? BancoSaleThemeKind.Dark
            : BancoSaleThemeKind.Light;
        BancoSalePalette.Apply(this, _theme);
    }

    private void ArticleSearchTextBox_OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text))
        {
            ResetArticleSearchInputTracking();
            return;
        }

        var now = DateTime.UtcNow;
        if (_articleSearchInputBuffer.Length == 0 || now - _articleSearchLastInputAtUtc > TimeSpan.FromMilliseconds(120))
        {
            _articleSearchInputBuffer.Clear();
            _articleSearchFirstInputAtUtc = now;
        }

        foreach (var character in e.Text)
        {
            if (!char.IsControl(character))
            {
                _articleSearchInputBuffer.Append(character);
            }
        }

        _articleSearchLastInputAtUtc = now;
    }

    private async void ArticleSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        e.Handled = true;
        var isScannerSubmission = IsProbableArticleScannerSubmission();
        ResetArticleSearchInputTracking();

        if (isScannerSubmission)
        {
            await viewModel.TryInsertDirectArticleAsync();
            return;
        }

        await OpenManualLookupAsync(viewModel);
    }

    private async void OpenArticleLookupButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BancoSaleViewModel viewModel)
        {
            await OpenManualLookupAsync(viewModel);
        }
    }

    private void PaymentTypeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string paymentType } && DataContext is BancoSaleViewModel viewModel)
        {
            viewModel.ApplyPaymentAmount(paymentType);
        }
    }

    private async Task OpenManualLookupAsync(BancoSaleViewModel viewModel)
    {
        var searchText = viewModel.ArticleSearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var lookupViewModel = new ArticleLookupDialogViewModel(
            App.Services.GetRequiredService<Services.BancoSaleDataFacade>(),
            searchText);
        var dialog = new ArticleLookupDialog(lookupViewModel);
        BancoSalePalette.Apply(dialog, _theme);
        var result = await dialog.ShowDialog<bool>(this);
        if (result && dialog.SelectedArticle is not null)
        {
            viewModel.InsertArticleFromLookup(dialog.SelectedArticle);
        }
    }

    private bool IsProbableArticleScannerSubmission()
    {
        if (_articleSearchInputBuffer.Length < 8 || _articleSearchInputBuffer.Length > 18)
        {
            return false;
        }

        if (_articleSearchInputBuffer.ToString().Any(ch => !char.IsDigit(ch)))
        {
            return false;
        }

        var duration = _articleSearchLastInputAtUtc - _articleSearchFirstInputAtUtc;
        return duration <= TimeSpan.FromMilliseconds(350);
    }

    private void ResetArticleSearchInputTracking()
    {
        _articleSearchInputBuffer.Clear();
        _articleSearchFirstInputAtUtc = default;
        _articleSearchLastInputAtUtc = default;
    }
}
