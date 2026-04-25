using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Banco.UI.Avalonia.Controls.Controls;
using Banco.UI.Avalonia.Banco.DesignSystem;
using Banco.UI.Avalonia.Banco.ViewModels;
using System.Text;

namespace Banco.UI.Avalonia.Banco.Views;

public sealed partial class BancoSaleView : UserControl
{
    private BancoSaleThemeKind _theme = BancoSaleThemeKind.Light;
    private readonly StringBuilder _articleSearchInputBuffer = new();
    private readonly Dictionary<BancoSaleRowViewModel, CancellationTokenSource> _gridArticleSearchDebounces = new();
    private readonly DispatcherTimer _manualArticleLookupTimer;
    private BancoDataGridContextMenu? _saleRowsGridContextMenu;
    private DateTime _articleSearchFirstInputAtUtc;
    private DateTime _articleSearchLastInputAtUtc;
    private bool _isArticleLookupDialogOpen;

    public BancoSaleView()
    {
        InitializeComponent();
        _manualArticleLookupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _manualArticleLookupTimer.Tick += ManualArticleLookupTimer_OnTick;
        _saleRowsGridContextMenu = BancoDataGridContextMenu.Attach(SaleRowsGrid);
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            BancoSalePalette.Apply(window, _theme);
        }
    }

    public BancoSaleView(BancoSaleViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.ArticleQuantitySelectionRequested = OpenArticleQuantitySelectionAsync;
        viewModel.NegativeAvailabilityDecisionRequested = OpenNegativeAvailabilityDecisionAsync;
    }

    private void ThemeToggle_OnClick(object? sender, RoutedEventArgs e)
    {
        _theme = _theme == BancoSaleThemeKind.Light
            ? BancoSaleThemeKind.Dark
            : BancoSaleThemeKind.Light;
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            BancoSalePalette.Apply(window, _theme);
        }
    }

    private void BancoSaleView_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        var command = e.Key switch
        {
            Key.F4 => viewModel.SaveDocumentCommand,
            Key.F6 => viewModel.PrintPos80Command,
            Key.F7 => viewModel.PreviewPos80Command,
            Key.F8 => viewModel.CourtesyCommand,
            Key.F9 => viewModel.ReceiptCommand,
            _ => null
        };

        if (command is null || !command.CanExecute(null))
        {
            return;
        }

        command.Execute(null);
        e.Handled = true;
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
            if (e.Key is Key.Back or Key.Delete or Key.Left or Key.Right or Key.Home or Key.End or Key.Tab or Key.Escape)
            {
                ResetArticleSearchInputTracking();
                if (e.Key == Key.Escape)
                {
                    _manualArticleLookupTimer.Stop();
                }
            }

            return;
        }

        e.Handled = true;
        _manualArticleLookupTimer.Stop();
        var isScannerSubmission = IsProbableArticleScannerSubmission();
        ResetArticleSearchInputTracking();

        if (isScannerSubmission)
        {
            await viewModel.TryInsertDirectArticleAsync();
            return;
        }

        await OpenManualLookupAsync(viewModel);
    }

    private void ArticleSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isArticleLookupDialogOpen || sender is not TextBox textBox || DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        var searchText = viewModel.ArticleSearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText) || !textBox.IsKeyboardFocusWithin)
        {
            _manualArticleLookupTimer.Stop();
            return;
        }

        _manualArticleLookupTimer.Stop();
        _manualArticleLookupTimer.Start();
    }

    private void ArticleSearchTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        ResetArticleSearchInputTracking();
    }

    private async void ManualArticleLookupTimer_OnTick(object? sender, EventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        if (_isArticleLookupDialogOpen || DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        await OpenManualLookupAsync(viewModel);
    }

    private void PaymentTypeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string paymentType } && DataContext is BancoSaleViewModel viewModel)
        {
            viewModel.ApplyPaymentAmount(paymentType);
        }
    }

    private async Task<decimal?> OpenArticleQuantitySelectionAsync(
        global::Banco.Vendita.Articles.GestionaleArticleSearchResult article,
        global::Banco.Vendita.Articles.GestionaleArticlePricingDetail pricingDetail,
        decimal defaultQuantity)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return defaultQuantity;
        }

        var dialog = new ArticleQuantitySelectionDialog(article, pricingDetail, defaultQuantity);
        BancoSalePalette.Apply(dialog, _theme);
        var result = await dialog.ShowDialog<bool>(owner);
        return result ? dialog.SelectedQuantity : null;
    }

    private async Task<NegativeAvailabilityDecision> OpenNegativeAvailabilityDecisionAsync(
        global::Banco.Vendita.Articles.GestionaleArticleSearchResult article,
        decimal requestedQuantity)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return NegativeAvailabilityDecision.ScaricaComunque;
        }

        var dialog = new NegativeAvailabilityDialog(article, requestedQuantity);
        BancoSalePalette.Apply(dialog, _theme);
        var result = await dialog.ShowDialog<bool>(owner);
        return result ? dialog.Decision : NegativeAvailabilityDecision.Annulla;
    }

    private void DeleteRowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BancoSaleRowViewModel row } && DataContext is BancoSaleViewModel viewModel)
        {
            CancelGridArticleSearch(row);
            viewModel.RemoveRow(row);
        }
    }

    private void SaleRowsDataGrid_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Multiply:
                _ = viewModel.AddSelectedRowToReorderListAsync();
                e.Handled = true;
                break;
            case Key.Divide:
                _ = viewModel.RemoveSelectedRowFromReorderListAsync();
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                viewModel.ChangeSelectedRowQuantity(1);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                viewModel.ChangeSelectedRowQuantity(-1);
                e.Handled = true;
                break;
            case Key.Delete when viewModel.SelectedRow is not null:
                CancelGridArticleSearch(viewModel.SelectedRow);
                viewModel.ClearArticleCode(viewModel.SelectedRow);
                e.Handled = true;
                break;
            case Key.Insert:
                viewModel.AddManualRow();
                e.Handled = true;
                break;
        }
    }

    private void SaleRowsDataGrid_OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        e.Row.Background = ResolveAlternatingRowBackground(e.Row.Index);
    }

    private IBrush ResolveAlternatingRowBackground(int rowIndex)
    {
        if (_theme == BancoSaleThemeKind.Dark)
        {
            return SolidColorBrush.Parse(rowIndex % 2 == 0 ? "#18263A" : "#1A2A40");
        }

        return SolidColorBrush.Parse(rowIndex % 2 == 0 ? "#FFFFFF" : "#EAF4FF");
    }

    private void GridRowEditor_OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is Control { DataContext: BancoSaleRowViewModel row } &&
            DataContext is BancoSaleViewModel viewModel)
        {
            viewModel.SelectedRow = row;
        }
    }

    private void GridArticleCodeTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: BancoSaleRowViewModel row } textBox ||
            DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        if (!textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        var searchText = textBox.Text?.Trim() ?? string.Empty;
        CancelGridArticleSearch(row);
        if (searchText.Length < 3)
        {
            return;
        }

        var debounce = new CancellationTokenSource();
        _gridArticleSearchDebounces[row] = debounce;
        _ = ResolveGridArticleAfterDelayAsync(viewModel, row, searchText, debounce.Token);
    }

    private async void GridArticleCodeTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: BancoSaleRowViewModel row } textBox ||
            DataContext is not BancoSaleViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            CancelGridArticleSearch(row);
            viewModel.ClearArticleCode(row);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Insert)
        {
            viewModel.AddManualRow();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Multiply)
        {
            viewModel.SelectedRow = row;
            _ = viewModel.AddSelectedRowToReorderListAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Divide)
        {
            viewModel.SelectedRow = row;
            _ = viewModel.RemoveSelectedRowFromReorderListAsync();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Add or Key.OemPlus)
        {
            viewModel.SelectedRow = row;
            viewModel.ChangeSelectedRowQuantity(1);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Subtract or Key.OemMinus)
        {
            viewModel.SelectedRow = row;
            viewModel.ChangeSelectedRowQuantity(-1);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        CancelGridArticleSearch(row);
        var searchText = textBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        await OpenGridLookupAsync(viewModel, row, searchText);
    }

    private async Task OpenManualLookupAsync(BancoSaleViewModel viewModel)
    {
        var searchText = viewModel.ArticleSearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        if (_isArticleLookupDialogOpen)
        {
            return;
        }

        var lookupViewModel = new ArticleLookupDialogViewModel(
            viewModel.DataFacade,
            searchText,
            viewModel.SelectedPriceListOid);
        var dialog = new ArticleLookupDialog(lookupViewModel);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        BancoSalePalette.Apply(dialog, _theme);
        _isArticleLookupDialogOpen = true;
        try
        {
            var result = await dialog.ShowDialog<bool>(owner);
            if (result && dialog.SelectedArticle is not null)
            {
                viewModel.InsertArticleFromLookup(dialog.SelectedArticle);
            }
        }
        finally
        {
            _isArticleLookupDialogOpen = false;
        }
    }

    private async Task ResolveGridArticleAfterDelayAsync(
        BancoSaleViewModel viewModel,
        BancoSaleRowViewModel row,
        string searchText,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await ResolveGridArticleOrOpenLookupAsync(viewModel, row, searchText, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ResolveGridArticleOrOpenLookupAsync(
        BancoSaleViewModel viewModel,
        BancoSaleRowViewModel row,
        string searchText,
        CancellationToken cancellationToken)
    {
        var lookupResult = await viewModel.DataFacade.SearchArticlesAsync(searchText, viewModel.SelectedPriceListOid, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        switch (lookupResult.Articles.Count)
        {
            case 0:
                await viewModel.ResolveArticleForRowAsync(row, searchText);
                return;
            case 1:
                viewModel.InsertArticleIntoRow(row, lookupResult.Articles[0]);
                return;
            default:
                viewModel.StatusMessage = $"Trovati {lookupResult.Articles.Count} articoli simili: seleziona dalla modale.";
                await OpenGridLookupAsync(viewModel, row, searchText);
                return;
        }
    }

    private async Task OpenGridLookupAsync(
        BancoSaleViewModel viewModel,
        BancoSaleRowViewModel row,
        string searchText)
    {
        if (_isArticleLookupDialogOpen)
        {
            return;
        }

        var lookupViewModel = new ArticleLookupDialogViewModel(
            viewModel.DataFacade,
            searchText,
            viewModel.SelectedPriceListOid);
        var dialog = new ArticleLookupDialog(lookupViewModel);
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        BancoSalePalette.Apply(dialog, _theme);
        _isArticleLookupDialogOpen = true;
        try
        {
            var result = await dialog.ShowDialog<bool>(owner);
            if (result && dialog.SelectedArticle is not null)
            {
                viewModel.InsertArticleIntoRow(row, dialog.SelectedArticle);
            }
        }
        finally
        {
            _isArticleLookupDialogOpen = false;
        }
    }

    private void CancelGridArticleSearch(BancoSaleRowViewModel row)
    {
        if (!_gridArticleSearchDebounces.Remove(row, out var debounce))
        {
            return;
        }

        debounce.Cancel();
        debounce.Dispose();
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
