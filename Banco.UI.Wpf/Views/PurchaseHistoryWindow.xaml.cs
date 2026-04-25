using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Banco.UI.Shared.Grid;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Articles;
using Banco.Vendita.Customers;

namespace Banco.UI.Wpf.Views;

public partial class PurchaseHistoryWindow : Window
{
    private readonly Dictionary<string, DataGridColumn> _gridColumns = new(StringComparer.OrdinalIgnoreCase);
    private DataGridColumnManager? _columnManager;
    private SharedGridContextMenuController? _contextMenuController;
    private bool _columnsInitialized;

    public PurchaseHistoryWindow(PurchaseHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PurchaseHistoryViewModel viewModel)
        {
            return;
        }

        EnsureGridColumns();
        if (!_columnsInitialized)
        {
            _columnManager = new DataGridColumnManager(
                viewModel.ColumnDefinitions,
                viewModel.GetGridLayoutAsync,
                viewModel.SaveGridLayoutAsync,
                viewModel.GetColumnVisibility,
                viewModel.GetColumnDisplayIndex,
                viewModel.GetColumnWidth,
                viewModel.ToggleColumnVisibilityAsync,
                viewModel.SaveColumnDisplayIndexAsync,
                viewModel.SaveColumnWidthAsync,
                ApplyColumnVisibility,
                applyFrozenColumnCount: count => PurchaseHistoryGrid.FrozenColumnCount = count);

            await _columnManager.InitializeAsync(_gridColumns);
            _contextMenuController = new SharedGridContextMenuController(new SharedGridContextMenuOptions
            {
                Grid = PurchaseHistoryGrid,
                GridKey = "RicercaAcquisti",
                ColumnManager = _columnManager,
                IncludeAppearanceMenuOnHeader = true,
                IncludeAppearanceMenuOnBody = true,
                Actions = []
            });
            ApplyColumnVisibility();
            _columnsInitialized = true;
        }

        await viewModel.ForceRefreshAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _contextMenuController?.Dispose();
        _contextMenuController = null;
    }

    private void EnsureGridColumns()
    {
        if (_gridColumns.Count > 0)
        {
            return;
        }

        _gridColumns["DataDocumento"] = DataDocumentoColumn;
        _gridColumns["TipoDocumento"] = TipoDocumentoColumn;
        _gridColumns["CodiceArticolo"] = CodiceArticoloColumn;
        _gridColumns["DescrizioneArticolo"] = DescrizioneArticoloColumn;
        _gridColumns["FornitoreNominativo"] = FornitoreNominativoColumn;
        _gridColumns["RiferimentoFattura"] = RiferimentoFatturaColumn;
        _gridColumns["Quantita"] = QuantitaColumn;
        _gridColumns["PrezzoUnitario"] = PurchaseHistoryGrid.Columns[7];
        _gridColumns["TotaleRiga"] = PurchaseHistoryGrid.Columns[8];
    }

    private void ApplyColumnVisibility()
    {
        if (DataContext is not PurchaseHistoryViewModel viewModel)
        {
            return;
        }

        foreach (var pair in _gridColumns)
        {
            pair.Value.Visibility = viewModel.GetColumnVisibility(pair.Key)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void ArticleLookupGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchaseHistoryViewModel viewModel
            && ArticleLookupGrid.SelectedItem is GestionaleArticleSearchResult articolo)
        {
            viewModel.SelectArticleFromLookup(articolo);
            ArticleFilterTextBox.Focus();
            ArticleFilterTextBox.SelectAll();
        }
    }

    private void ArticleLookupGrid_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && DataContext is PurchaseHistoryViewModel viewModel
            && ArticleLookupGrid.SelectedItem is GestionaleArticleSearchResult articolo)
        {
            e.Handled = true;
            viewModel.SelectArticleFromLookup(articolo);
            ArticleFilterTextBox.Focus();
            ArticleFilterTextBox.SelectAll();
        }
    }

    private void ArticleFilterTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchaseHistoryViewModel viewModel
            || !viewModel.IsArticlePopupOpen
            || ArticleLookupGrid.Items.Count == 0)
        {
            return;
        }

        if (e.Key != Key.Down && e.Key != Key.Up)
        {
            return;
        }

        e.Handled = true;

        var targetIndex = e.Key == Key.Down
            ? 0
            : ArticleLookupGrid.Items.Count - 1;

        ArticleLookupGrid.SelectedIndex = targetIndex;
        ArticleLookupGrid.UpdateLayout();
        ArticleLookupGrid.ScrollIntoView(ArticleLookupGrid.SelectedItem);
        ArticleLookupGrid.Focus();

        if (ArticleLookupGrid.ItemContainerGenerator.ContainerFromIndex(targetIndex) is DataGridRow row)
        {
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    private void SupplierLookupList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchaseHistoryViewModel viewModel
            && SupplierLookupList.SelectedItem is GestionaleCustomerSummary fornitore)
        {
            viewModel.SelectSupplierFromLookup(fornitore);
            SupplierFilterTextBox.Focus();
            SupplierFilterTextBox.SelectAll();
        }
    }

    private void SupplierLookupList_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && DataContext is PurchaseHistoryViewModel viewModel
            && SupplierLookupList.SelectedItem is GestionaleCustomerSummary fornitore)
        {
            e.Handled = true;
            viewModel.SelectSupplierFromLookup(fornitore);
            SupplierFilterTextBox.Focus();
            SupplierFilterTextBox.SelectAll();
        }
    }
}
