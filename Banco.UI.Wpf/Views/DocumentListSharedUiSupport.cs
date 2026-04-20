using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Banco.UI.Wpf.Infrastructure.GridColumns;
using Banco.UI.Wpf.Shell;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Views;

internal sealed class DocumentListSharedUiSupport
{
    private readonly Func<DocumentListViewModel?> _resolveViewModel;
    private readonly Func<Window?> _resolveOwnerWindow;
    private readonly Func<DataGridColumnManager?> _resolveColumnManager;
    private readonly Action _applyColumnVisibility;
    private readonly Action _refreshFooterRow;
    private readonly Action _syncFooterColumnsFromGrid;
    private readonly Func<Task> _deleteSelectedLocalDocumentsAsync;
    private readonly Func<DocumentGridRowViewModel, Task> _deleteRowAsync;
    private readonly Action _syncGridSelectionToViewModel;

    public DocumentListSharedUiSupport(
        Func<DocumentListViewModel?> resolveViewModel,
        Func<Window?> resolveOwnerWindow,
        Func<DataGridColumnManager?> resolveColumnManager,
        Action applyColumnVisibility,
        Action refreshFooterRow,
        Action syncFooterColumnsFromGrid,
        Func<Task> deleteSelectedLocalDocumentsAsync,
        Func<DocumentGridRowViewModel, Task> deleteRowAsync,
        Action syncGridSelectionToViewModel)
    {
        _resolveViewModel = resolveViewModel;
        _resolveOwnerWindow = resolveOwnerWindow;
        _resolveColumnManager = resolveColumnManager;
        _applyColumnVisibility = applyColumnVisibility;
        _refreshFooterRow = refreshFooterRow;
        _syncFooterColumnsFromGrid = syncFooterColumnsFromGrid;
        _deleteSelectedLocalDocumentsAsync = deleteSelectedLocalDocumentsAsync;
        _deleteRowAsync = deleteRowAsync;
        _syncGridSelectionToViewModel = syncGridSelectionToViewModel;
    }

    public SharedGridContextMenuController CreateContextMenuController(DataGrid grid, string gridKey)
    {
        var columnManager = _resolveColumnManager();
        if (columnManager is null)
        {
            throw new InvalidOperationException("Column manager non inizializzato.");
        }

        return new SharedGridContextMenuController(new SharedGridContextMenuOptions
        {
            Grid = grid,
            GridKey = gridKey,
            ColumnManager = columnManager,
            IncludeAppearanceMenuOnBody = true,
            Actions =
            [
                new SharedGridContextAction
                {
                    Key = "documenti.delete-selected",
                    Header = "Cancella selezionati",
                    Surfaces = [SharedGridContextSurface.Body],
                    IsVisible = context => context.SelectedItems.Count > 0,
                    IsEnabled = _ => _resolveViewModel()?.CanDeleteSelectedLocalDocuments == true,
                    ExecuteAsync = async _ => await _deleteSelectedLocalDocumentsAsync()
                }
            ]
        });
    }

    public void HandleViewModelPropertyChanged(string? propertyName)
    {
        if (propertyName is not null && propertyName.StartsWith("Is", StringComparison.Ordinal) && propertyName.EndsWith("ColumnVisible", StringComparison.Ordinal))
        {
            _applyColumnVisibility();
            _syncFooterColumnsFromGrid();
        }

        _refreshFooterRow();
    }

    public void HandleRootPreviewKeyDown(KeyEventArgs e, IInputElement totalsPanel)
    {
        var viewModel = _resolveViewModel();
        if (Keyboard.Modifiers != ModifierKeys.Control || viewModel is null)
        {
            return;
        }

        if (e.Key == Key.T)
        {
            Keyboard.Focus(totalsPanel);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && viewModel.ToggleUnscontrinatiModeCommand.CanExecute(null))
        {
            viewModel.ToggleUnscontrinatiModeCommand.Execute(null);
            _applyColumnVisibility();
            _syncFooterColumnsFromGrid();
            _refreshFooterRow();
            e.Handled = true;
        }
    }

    public async Task HandleNewBancoDocumentAsync()
    {
        if (_resolveOwnerWindow() is not ShellWindow shellWindow)
        {
            return;
        }

        await shellWindow.RequestNewBancoDocumentAsync();
    }

    public void HandleDateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        if (sender is not DatePicker datePicker || string.IsNullOrWhiteSpace(e.Text))
        {
            return;
        }

        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "ddMMyyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy"];
        if (DateTime.TryParseExact(e.Text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            datePicker.SelectedDate = parsed;
            e.ThrowException = false;
        }
    }

    public async Task HandleDeleteSelectedAsync(DataGrid documentsGrid)
    {
        var viewModel = _resolveViewModel();
        if (viewModel is null || !viewModel.CanDeleteSelectedLocalDocuments)
        {
            return;
        }

        var deletableSelectedCount = viewModel.SelectedDeletableDocumentsCount;
        var dialogMessage = deletableSelectedCount == 1
            ? "Vuoi cancellare definitivamente il documento selezionato?"
            : $"Vuoi cancellare definitivamente i {deletableSelectedCount} documenti selezionati?";

        var dialog = new ConfirmationDialogWindow(
            "Documenti / eliminazione",
            "Conferma cancellazione massiva",
            dialogMessage,
            "Cancella selezionati",
            "Indietro",
            "L'operazione cancella i documenti non scontrinati dal DB legacy e rimuove gli eventuali riferimenti locali collegati.")
        {
            Owner = _resolveOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _deleteSelectedLocalDocumentsAsync();
        _syncGridSelectionToViewModel();
        documentsGrid.Focus();
    }

    public async Task HandleDeleteRowAsync(DocumentGridRowViewModel row, DataGrid documentsGrid)
    {
        var viewModel = _resolveViewModel();
        if (viewModel is null || !viewModel.CanDeleteDocumentRow(row))
        {
            return;
        }

        var dialog = new ConfirmationDialogWindow(
            "Documenti / eliminazione",
            "Conferma cancellazione",
            "Vuoi cancellare definitivamente questo documento?",
            "Elimina",
            "Indietro",
            "L'azione cancella il documento non scontrinato dal DB legacy e riallinea subito lista, dettaglio e footer.")
        {
            Owner = _resolveOwnerWindow()
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _deleteRowAsync(row);
        _syncGridSelectionToViewModel();
        documentsGrid.Focus();
    }

    public static void SyncFooterColumnsFromGrid(
        IReadOnlyDictionary<string, DataGridColumn> gridColumns,
        IReadOnlyDictionary<string, DataGridColumn> footerColumns,
        ScrollViewer? gridScrollViewer,
        ScrollViewer? footerScrollViewer)
    {
        foreach (var pair in gridColumns)
        {
            if (!footerColumns.TryGetValue(pair.Key, out var footerColumn))
            {
                continue;
            }

            var sourceColumn = pair.Value;
            var width = sourceColumn.ActualWidth > 0 ? sourceColumn.ActualWidth : sourceColumn.Width.DisplayValue;
            footerColumn.Width = width > 0 ? new DataGridLength(width) : sourceColumn.Width;
            footerColumn.DisplayIndex = sourceColumn.DisplayIndex;
            footerColumn.Visibility = sourceColumn.Visibility;
        }

        if (gridScrollViewer is not null && footerScrollViewer is not null)
        {
            footerScrollViewer.ScrollToHorizontalOffset(gridScrollViewer.HorizontalOffset);
        }
    }

    public static void SyncGridSelectionToViewModel(DataGrid documentsGrid, DocumentListViewModel viewModel)
    {
        var selectedRow = viewModel.SelectedDocument;
        documentsGrid.SelectedItems.Clear();

        if (selectedRow is null)
        {
            documentsGrid.SelectedItem = null;
            return;
        }

        documentsGrid.SelectedItem = selectedRow;
        documentsGrid.ScrollIntoView(selectedRow);
    }

    public static T? FindDescendant<T>(DependencyObject? current) where T : DependencyObject
    {
        if (current is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
        {
            var child = VisualTreeHelper.GetChild(current, index);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
