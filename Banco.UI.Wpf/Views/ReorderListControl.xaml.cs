using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Banco.UI.Wpf.Infrastructure.GridColumns;
using Banco.UI.Wpf.Services;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Articles;

namespace Banco.UI.Wpf.Views;

public partial class ReorderListControl : UserControl
{
    private DataGridColumnManager? _columnManager;
    private readonly Dictionary<string, DataGridColumn> _gridColumns;
    private ReorderListViewModel? _attachedViewModel;
    private bool _columnsInitialized;

    public ReorderListControl()
    {
        InitializeComponent();
        _gridColumns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ordinato"] = OrdinatoColumn,
            ["Codice"] = CodiceColumn,
            ["Descrizione"] = DescrizioneColumn,
            ["Quantita"] = QuantitaColumn,
            ["QuantitaDaOrdinare"] = QuantitaDaOrdinareColumn,
            ["UnitaMisura"] = UnitaMisuraColumn,
            ["FornitoreSuggerito"] = FornitoreSuggeritoColumn,
            ["FornitoreSelezionato"] = FornitoreSelezionatoColumn,
            ["Prezzo"] = PrezzoColumn,
            ["Motivo"] = MotivoColumn,
            ["Operatore"] = OperatoreColumn,
            ["Data"] = DataColumn,
            ["Note"] = NoteColumn
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_columnsInitialized || DataContext is not ReorderListViewModel viewModel)
        {
            return;
        }

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
            ApplyColumnVisibility);

        await _columnManager.InitializeAsync(_gridColumns);
        ApplyColumnVisibility();
        AttachViewModel(viewModel);
        _columnsInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachViewModel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.OldValue, e.NewValue))
        {
            return;
        }

        DetachViewModel();

        if (e.NewValue is ReorderListViewModel viewModel && IsLoaded)
        {
            AttachViewModel(viewModel);
        }
    }

    private void AttachViewModel(ReorderListViewModel viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel))
        {
            return;
        }

        DetachViewModel();
        _attachedViewModel = viewModel;
        _attachedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel is null)
        {
            return;
        }

        _attachedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _attachedViewModel = null;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null && e.PropertyName.StartsWith("Is") && e.PropertyName.EndsWith("ColumnVisible"))
        {
            ApplyColumnVisibility();
        }
    }

    private void ApplyColumnVisibility()
    {
        if (DataContext is not ReorderListViewModel viewModel)
        {
            return;
        }

        ApplyColumnVisibility("Ordinato", viewModel.IsOrdinatoColumnVisible);
        ApplyColumnVisibility("Codice", viewModel.IsCodiceColumnVisible);
        ApplyColumnVisibility("Descrizione", viewModel.IsDescrizioneColumnVisible);
        ApplyColumnVisibility("Quantita", viewModel.IsQuantitaColumnVisible);
        ApplyColumnVisibility("QuantitaDaOrdinare", viewModel.IsQuantitaDaOrdinareColumnVisible);
        ApplyColumnVisibility("UnitaMisura", viewModel.IsUnitaMisuraColumnVisible);
        ApplyColumnVisibility("FornitoreSuggerito", viewModel.IsFornitoreSuggeritoColumnVisible);
        ApplyColumnVisibility("FornitoreSelezionato", viewModel.IsFornitoreSelezionatoColumnVisible);
        ApplyColumnVisibility("Prezzo", viewModel.IsPrezzoColumnVisible);
        ApplyColumnVisibility("Motivo", viewModel.IsMotivoColumnVisible);
        ApplyColumnVisibility("Operatore", viewModel.IsOperatoreColumnVisible);
        ApplyColumnVisibility("Data", viewModel.IsDataColumnVisible);
        ApplyColumnVisibility("Note", viewModel.IsNoteColumnVisible);
    }

    private void ApplyColumnVisibility(string key, bool isVisible)
    {
        if (_gridColumns.TryGetValue(key, out var column))
        {
            column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void ColumnsMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || _columnManager is null)
        {
            return;
        }

        await _columnManager.PopulateContextMenuAsync(menu);
        BancoGridMenuService.AppendColorMenu(menu, "Riordino");
    }

    private void ReorderGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ReorderListViewModel viewModel)
        {
            viewModel.SelectedItem = ReorderGrid.SelectedItem as ReorderGridRowViewModel;
            viewModel.SetSelectedRows(ReorderGrid.SelectedItems.Cast<ReorderGridRowViewModel>().ToList());
        }
    }

    private void ReorderGrid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var origin = e.OriginalSource as DependencyObject;
        if (origin is null || FindAncestor<DataGridColumnHeader>(origin) is not null)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(origin);
        if (row?.Item is not ReorderGridRowViewModel item)
        {
            return;
        }

        ReorderGrid.Focus();
        if (!ReorderGrid.SelectedItems.Contains(item))
        {
            ReorderGrid.SelectedItems.Clear();
            ReorderGrid.SelectedItem = item;
        }

        var column = FindAncestor<DataGridCell>(origin)?.Column
                     ?? ReorderGrid.Columns.Where(current => current.Visibility == Visibility.Visible).OrderBy(current => current.DisplayIndex).FirstOrDefault();
        if (column is not null)
        {
            ReorderGrid.CurrentCell = new DataGridCellInfo(item, column);
            ReorderGrid.ScrollIntoView(item, column);
        }
    }

    private async void OrderedCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not ReorderGridRowViewModel row || DataContext is not ReorderListViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedItem = row;
        if (checkBox.IsChecked == true)
        {
            if (viewModel.MarkOrderedCommand.CanExecute(null))
            {
                viewModel.MarkOrderedCommand.Execute(null);
            }
        }
        else if (viewModel.MarkPendingCommand.CanExecute(null))
        {
            viewModel.MarkPendingCommand.Execute(null);
        }
    }

    private void ArticleSearchTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ReorderListViewModel viewModel ||
            e.Key != Key.Down ||
            !viewModel.IsArticlePopupOpen ||
            ArticleLookupGrid.Items.Count == 0)
        {
            return;
        }

        ArticleLookupGrid.SelectedIndex = 0;
        ArticleLookupGrid.Focus();
        if (ArticleLookupGrid.ItemContainerGenerator.ContainerFromIndex(0) is DataGridRow row)
        {
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        e.Handled = true;
    }

    private void ArticleLookupGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReorderListViewModel viewModel &&
            ArticleLookupGrid.SelectedItem is GestionaleArticleSearchResult articolo)
        {
            viewModel.SelectArticleFromLookup(articolo);
            ArticleSearchTextBox.Focus();
            ArticleSearchTextBox.SelectAll();
        }
    }

    private void ArticleLookupGrid_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ReorderListViewModel viewModel ||
            ArticleLookupGrid.SelectedItem is not GestionaleArticleSearchResult articolo)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.SelectArticleFromLookup(articolo);
            ArticleSearchTextBox.Focus();
            ArticleSearchTextBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.IsArticlePopupOpen = false;
            ArticleSearchTextBox.Focus();
            ArticleSearchTextBox.SelectAll();
            e.Handled = true;
        }
    }

    private void ReorderGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var origin = e.OriginalSource as DependencyObject;
        if (origin is null || FindAncestor<DataGridColumnHeader>(origin) is not null)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(origin);
        var cell = FindAncestor<DataGridCell>(origin);
        if (row?.Item is not ReorderGridRowViewModel item || cell is null)
        {
            return;
        }

        ReorderGrid.Focus();
        if (!ReferenceEquals(ReorderGrid.SelectedItem, item))
        {
            ReorderGrid.SelectedItem = item;
        }

        ReorderGrid.CurrentCell = new DataGridCellInfo(item, cell.Column);

        if (ReferenceEquals(cell.Column, QuantitaDaOrdinareColumn))
        {
            if (!cell.IsEditing)
            {
                ReorderGrid.BeginEdit(e);
            }

            _ = Dispatcher.BeginInvoke(() =>
            {
                var textBox = FindChild<TextBox>(cell);
                if (textBox is null)
                {
                    return;
                }

                textBox.Focus();
                textBox.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);

            return;
        }

        if (!ReferenceEquals(cell.Column, FornitoreSelezionatoColumn))
        {
            return;
        }

        if (!cell.IsEditing)
        {
            ReorderGrid.BeginEdit(e);
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            var comboBox = FindChild<ComboBox>(cell);
            if (comboBox is null)
            {
                return;
            }

            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SupplierComboBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.Items.Count > 0)
        {
            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
        }
    }

    private async void SupplierComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: ReorderGridRowViewModel row } comboBox ||
            DataContext is not ReorderListViewModel viewModel ||
            comboBox.SelectedItem is not ReorderSupplierOptionViewModel supplier)
        {
            return;
        }

        await viewModel.UpdateSelectedSupplierAsync(row, supplier);
    }

    private async void ReorderGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit ||
            !ReferenceEquals(e.Column, QuantitaDaOrdinareColumn) ||
            e.Row.Item is not ReorderGridRowViewModel row ||
            DataContext is not ReorderListViewModel viewModel ||
            e.EditingElement is not TextBox textBox)
        {
            return;
        }

        await PersistQuantityEditAsync(textBox, row, viewModel);
    }

    private void ReorderGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            DataContext is not ReorderListViewModel ||
            !ReferenceEquals(ReorderGrid.CurrentCell.Column, QuantitaDaOrdinareColumn))
        {
            return;
        }

        ReorderGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ReorderGrid.CommitEdit(DataGridEditingUnit.Row, true);
        e.Handled = true;
    }

    private async void QuantityToOrderTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox ||
            textBox.DataContext is not ReorderGridRowViewModel row ||
            DataContext is not ReorderListViewModel viewModel)
        {
            return;
        }

        await PersistQuantityEditAsync(textBox, row, viewModel);
    }

    private async void QuantityToOrderTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            sender is not TextBox textBox ||
            textBox.DataContext is not ReorderGridRowViewModel row ||
            DataContext is not ReorderListViewModel viewModel)
        {
            return;
        }

        await PersistQuantityEditAsync(textBox, row, viewModel);
        ReorderGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ReorderGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void MarkOrderedMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReorderListViewModel viewModel && viewModel.MarkOrderedCommand.CanExecute(null))
        {
            viewModel.MarkOrderedCommand.Execute(null);
        }
    }

    private void MarkPendingMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReorderListViewModel viewModel && viewModel.MarkPendingCommand.CanExecute(null))
        {
            viewModel.MarkPendingCommand.Execute(null);
        }
    }

    private void RemoveMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReorderListViewModel viewModel || !viewModel.RemoveCommand.CanExecute(null))
        {
            return;
        }

        var dialog = new ConfirmationDialogWindow(
            "Lista riordino",
            "Rimozione articolo",
            "Vuoi rimuovere l'articolo selezionato dalla lista riordino?",
            "Rimuovi",
            "Indietro",
            "L'azione rimuove solo il supporto locale di riordino e non modifica il documento Banco.")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.RemoveCommand.Execute(null);
        }
    }

    private void RemoveSupplierDraftButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReorderListViewModel viewModel || !viewModel.RemoveSupplierDraftCommand.CanExecute(null))
        {
            return;
        }

        var supplierName = viewModel.SelectedSupplierDraft?.SupplierName ?? "lista selezionata";
        var dialog = new ConfirmationDialogWindow(
            "Lista riordino",
            "Rimozione lista fornitore",
            $"Vuoi rimuovere la lista fornitore {supplierName} con tutte le sue righe locali?",
            "Elimina lista",
            "Indietro",
            "L'azione rimuove il gruppo fornitore dalla lista riordino locale e non crea o annulla documenti su FM.")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            viewModel.RemoveSupplierDraftCommand.Execute(null);
        }
    }

    private void EditSelectedRowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ReorderListViewModel viewModel || viewModel.SelectedItem is null)
        {
            return;
        }

        BeginEditQuantityCell(viewModel.SelectedItem);
    }

    private static bool TryParseQuantity(string? rawValue, out decimal quantity)
    {
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.CurrentCulture, out quantity) ||
               decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity);
    }

    private static decimal NormalizeQuantity(ReorderGridRowViewModel row, string? rawValue)
    {
        if (TryParseQuantity(rawValue, out var parsedQuantity))
        {
            return parsedQuantity <= 0 ? 1 : parsedQuantity;
        }

        return row.QuantitaDaOrdinare <= 0 ? row.Quantita : row.QuantitaDaOrdinare;
    }

    private static async Task PersistQuantityEditAsync(
        TextBox textBox,
        ReorderGridRowViewModel row,
        ReorderListViewModel viewModel)
    {
        var quantityToOrder = NormalizeQuantity(row, textBox.Text);
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        await viewModel.UpdateQuantityToOrderAsync(row, quantityToOrder);
    }

    private void BeginEditQuantityCell(ReorderGridRowViewModel row)
    {
        ReorderGrid.Focus();
        ReorderGrid.SelectedItem = row;
        ReorderGrid.ScrollIntoView(row, QuantitaDaOrdinareColumn);
        ReorderGrid.CurrentCell = new DataGridCellInfo(row, QuantitaDaOrdinareColumn);
        ReorderGrid.BeginEdit();

        _ = Dispatcher.BeginInvoke(() =>
        {
            var dataGridRow = ReorderGrid.ItemContainerGenerator.ContainerFromItem(row) as DataGridRow;
            var cell = FindVisualChildInRow<DataGridCell>(dataGridRow, QuantitaDaOrdinareColumn.DisplayIndex);
            var textBox = FindChild<TextBox>(cell);
            if (textBox is null)
            {
                return;
            }

            textBox.Focus();
            textBox.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private static T? FindAncestor<T>(DependencyObject? origin)
        where T : DependencyObject
    {
        while (origin is not null)
        {
            if (origin is T found)
            {
                return found;
            }

            origin = System.Windows.Media.VisualTreeHelper.GetParent(origin);
        }

        return null;
    }

    private static T? FindChild<T>(DependencyObject? origin)
        where T : DependencyObject
    {
        if (origin is null)
        {
            return null;
        }

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(origin); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(origin, i);
            if (child is T found)
            {
                return found;
            }

            var nested = FindChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindVisualChildInRow<T>(DependencyObject? origin, int displayIndex)
        where T : DependencyObject
    {
        if (origin is null)
        {
            return null;
        }

        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(origin); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(origin, i);
            if (child is DataGridCellsPresenter presenter)
            {
                var cell = presenter.ItemContainerGenerator.ContainerFromIndex(displayIndex) as T;
                if (cell is not null)
                {
                    return cell;
                }
            }

            var nested = FindVisualChildInRow<T>(child, displayIndex);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
