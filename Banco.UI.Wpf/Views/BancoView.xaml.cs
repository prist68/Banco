using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using Banco.Vendita.Customers;
using Banco.Vendita.Pos;
using Banco.Vendita.Points;
using Banco.UI.Shared.Grid;
using Banco.UI.Shared.Input;
using Banco.UI.Wpf.Interactions;
using Banco.UI.Wpf.Services;
using Banco.UI.Wpf.ViewModels;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.UI.Wpf.Views;

public partial class BancoView : UserControl
{
    private bool _columnsInitialized;
    private BancoViewModel? _attachedViewModel;
    private bool _isSynchronizingDocumentoSelection;
    private Pos80PreviewWindow? _previewPos80Window;
    private readonly StringBuilder _articleSearchInputBuffer = new();
    private DateTime _articleSearchFirstInputAtUtc;
    private DateTime _articleSearchLastInputAtUtc;
    private readonly DispatcherTimer _manualArticleLookupTimer;
    private bool _isArticleLookupDialogOpen;
    private bool _suppressManualArticleLookupScheduling;

    private bool _isLayoutDirty;
    private Dictionary<string, double> _layoutPredefinitoColonne = new(StringComparer.OrdinalIgnoreCase);
    private DataGridColumnManager? _columnManager;
    private SharedGridContextMenuController? _contextMenuController;
    private readonly Dictionary<string, DataGridColumn> _gridColumns = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyList<GridColumnDefinition> BancoColumnDefinitions =
    [
        new() { Key = "Codice", Header = "Codice", IsVisibleByDefault = true, DefaultWidth = 110, DefaultDisplayIndex = 0, Group = "Documento", Description = "Codice articolo della riga.", MinWidth = 90, IsFrozen = true, TextAlignment = GridColumnContentAlignment.Left },
        new() { Key = "Descrizione", Header = "Descrizione", IsVisibleByDefault = true, DefaultWidth = 350, DefaultDisplayIndex = 1, Group = "Documento", Description = "Descrizione operativa della riga.", MinWidth = 180, IsFrozen = true, TextAlignment = GridColumnContentAlignment.Left },
        new() { Key = "UnitaMisura", Header = "Um", IsVisibleByDefault = true, DefaultWidth = 54, DefaultDisplayIndex = 2, Group = "Documento", Description = "Unita` di misura della riga.", MinWidth = 48, MaxWidth = 72, TextAlignment = GridColumnContentAlignment.Center },
        new() { Key = "Quantita", Header = "Quantita`", IsVisibleByDefault = true, DefaultWidth = 78, DefaultDisplayIndex = 3, Group = "Prezzi e q.ta", Description = "Quantita` della riga.", IsNumeric = true, MinWidth = 70, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "Prezzo", Header = "Valore Ivato", IsVisibleByDefault = true, DefaultWidth = 98, DefaultDisplayIndex = 4, Group = "Prezzi e q.ta", Description = "Prezzo unitario ivato.", IsNumeric = true, MinWidth = 88, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "Sconto", Header = "Sc1 %", IsVisibleByDefault = true, DefaultWidth = 72, DefaultDisplayIndex = 5, Group = "Prezzi e q.ta", Description = "Sconto percentuale della riga.", IsNumeric = true, MinWidth = 64, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "Importo", Header = "Importo", IsVisibleByDefault = true, DefaultWidth = 92, DefaultDisplayIndex = 6, Group = "Prezzi e q.ta", Description = "Importo totale della riga.", IsNumeric = true, MinWidth = 86, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "Disponibilita", Header = "Disp.", IsVisibleByDefault = true, DefaultWidth = 76, DefaultDisplayIndex = 7, Group = "Controllo", Description = "Disponibilita` residua di riferimento.", IsNumeric = true, MinWidth = 68, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "Iva", Header = "Iva", IsVisibleByDefault = true, DefaultWidth = 62, DefaultDisplayIndex = 8, Group = "Controllo", Description = "Aliquota IVA della riga.", MinWidth = 54, MaxWidth = 72, TextAlignment = GridColumnContentAlignment.Center },
        new() { Key = "TipoRiga", Header = "Tipo", IsVisibleByDefault = true, DefaultWidth = 92, DefaultDisplayIndex = 9, Group = "Controllo", Description = "Tipo di riga del documento.", MinWidth = 84, TextAlignment = GridColumnContentAlignment.Center },
        new() { Key = "Azioni", Header = "Azioni", IsVisibleByDefault = true, DefaultWidth = 44, DefaultDisplayIndex = 10, Group = "Controllo", Description = "Colonna operativa fissa delle azioni rapide.", CanHide = false, IsLocked = true, MinWidth = 44, MaxWidth = 44, TextAlignment = GridColumnContentAlignment.Center, PresetKey = "operativa" }
    ];

    public BancoView()
    {
        InitializeComponent();
        _manualArticleLookupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _manualArticleLookupTimer.Tick += ManualArticleLookupTimer_OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (!_columnsInitialized)
        {
            await viewModel.EnsureLayoutInitializedAsync();

            CodiceColumn.Width = new DataGridLength(viewModel.CodiceColumnWidth);
            DescrizioneColumn.Width = new DataGridLength(viewModel.DescrizioneColumnWidth);
            QuantitaColumn.Width = new DataGridLength(viewModel.QuantitaColumnWidth);
            PrezzoColumn.Width = new DataGridLength(viewModel.PrezzoColumnWidth);
            IvaColumn.Width = new DataGridLength(viewModel.IvaColumnWidth);
            UnitaMisuraColumn.Width = new DataGridLength(viewModel.TipoColumnWidth);
            ScontoColumn.Width = new DataGridLength(viewModel.ScontoColumnWidth);
            ImportoColumn.Width = new DataGridLength(viewModel.ImportoColumnWidth);
            AzioniColumn.Width = new DataGridLength(viewModel.AzioniColumnWidth);

            ApplyFixedDisplayIndexes();

            // Memorizza il layout di default reale prima di eventuale override XML.
            MemorizzaLayoutPredefinitoCorrente();

            ApplyColumnVisibility(viewModel);

            RegisterWidthChange(CodiceColumn, "Codice");
            RegisterWidthChange(DescrizioneColumn, "Descrizione");
            RegisterWidthChange(QuantitaColumn, "Quantita");
            RegisterWidthChange(PrezzoColumn, "Prezzo");
            RegisterWidthChange(IvaColumn, "Iva");
            RegisterWidthChange(UnitaMisuraColumn, "UnitaMisura");
            RegisterWidthChange(ScontoColumn, "Sconto");
            RegisterWidthChange(ImportoColumn, "Importo");
            RegisterWidthChange(AzioniColumn, "Azioni");

            _columnsInitialized = true;
        }

        EnsureColumnInfrastructure(viewModel);
        AttachViewModel(viewModel);
        FocusDefaultInput(force: false);
    }

    private void ApplyFixedDisplayIndexes()
    {
        CodiceColumn.DisplayIndex = 0;
        DescrizioneColumn.DisplayIndex = 1;
        UnitaMisuraColumn.DisplayIndex = 2;
        QuantitaColumn.DisplayIndex = 3;
        PrezzoColumn.DisplayIndex = 4;
        ScontoColumn.DisplayIndex = 5;
        ImportoColumn.DisplayIndex = 6;
        DisponibilitaColumn.DisplayIndex = 7;
        IvaColumn.DisplayIndex = 8;
        TipoRigaColumn.DisplayIndex = 9;
        AzioniColumn.DisplayIndex = 10;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();

        if (e.NewValue is BancoViewModel viewModel)
        {
            AttachViewModel(viewModel);

            if (_columnsInitialized)
            {
                ApplyColumnVisibility(viewModel);
            }
        }
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        _suppressManualArticleLookupScheduling = true;

        if (_previewPos80Window is not null && _previewPos80Window.IsLoaded)
        {
            _previewPos80Window.Close();
        }

        _contextMenuController?.Dispose();
        _contextMenuController = null;
        DetachViewModel();
    }

    private void OnFocusArticleSearchRequested()
    {
        FocusDefaultInput(force: true);
    }

    private async void ManualArticleLookupTimer_OnTick(object? sender, EventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        if (_suppressManualArticleLookupScheduling || _isArticleLookupDialogOpen || DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        var searchText = viewModel.SearchArticoloText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText) || !ArticleSearchTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        await viewModel.OpenManualArticleLookupAsync(searchText);
    }

    private void EnsureColumnInfrastructure(BancoViewModel viewModel)
    {
        if (_gridColumns.Count == 0)
        {
            _gridColumns["Codice"] = CodiceColumn;
            _gridColumns["Descrizione"] = DescrizioneColumn;
            _gridColumns["UnitaMisura"] = UnitaMisuraColumn;
            _gridColumns["Quantita"] = QuantitaColumn;
            _gridColumns["Prezzo"] = PrezzoColumn;
            _gridColumns["Sconto"] = ScontoColumn;
            _gridColumns["Importo"] = ImportoColumn;
            _gridColumns["Disponibilita"] = DisponibilitaColumn;
            _gridColumns["Iva"] = IvaColumn;
            _gridColumns["TipoRiga"] = TipoRigaColumn;
            _gridColumns["Azioni"] = AzioniColumn;
        }

        _columnManager ??= new DataGridColumnManager(
            BancoColumnDefinitions,
            () => Task.FromResult(CreateBancoGridLayoutSettings()),
            _ => Task.CompletedTask,
            key => ResolveColumnVisibility(viewModel, key),
            ResolveBancoDisplayIndex,
            key => ResolveColumnWidth(viewModel, key),
            key => viewModel.SetColumnVisibilityAsync(key, !ResolveColumnVisibility(viewModel, key)),
            (_, _) => Task.CompletedTask,
            viewModel.SaveColumnWidthAsync,
            () => ApplyColumnVisibility(viewModel),
            applyFrozenColumnCount: count => DocumentoRowsGrid.FrozenColumnCount = count);

        _ = _columnManager.InitializeAsync(_gridColumns);

        _contextMenuController ??= new SharedGridContextMenuController(new SharedGridContextMenuOptions
        {
            Grid = DocumentoRowsGrid,
            GridKey = "Banco",
            ColumnManager = _columnManager,
            IncludeAppearanceMenuOnBody = true,
            Actions =
            [
                new SharedGridContextAction
                {
                    Key = "banco.reorder.add",
                    Header = "Aggiungi a lista riordino",
                    InputGestureText = "*",
                    Surfaces = [SharedGridContextSurface.Body],
                    IsVisible = context => context.Item is RigaDocumentoLocaleViewModel,
                    IsEnabled = context => CanManageReorder(context.Item as RigaDocumentoLocaleViewModel, shouldBeInList: false),
                    ExecuteAsync = async _ => await viewModel.AddSelectedRowToReorderListAsync()
                },
                new SharedGridContextAction
                {
                    Key = "banco.reorder.remove",
                    Header = "Rimuovi da lista riordino",
                    InputGestureText = "/",
                    Surfaces = [SharedGridContextSurface.Body],
                    IsVisible = context => context.Item is RigaDocumentoLocaleViewModel,
                    IsEnabled = context => CanManageReorder(context.Item as RigaDocumentoLocaleViewModel, shouldBeInList: true),
                    ExecuteAsync = async _ => await viewModel.RemoveSelectedRowFromReorderListAsync()
                },
                new SharedGridContextAction
                {
                    Key = "banco.grid.reset",
                    Header = "Ripristina layout griglia",
                    BeginGroup = true,
                    Surfaces = [SharedGridContextSurface.Header, SharedGridContextSurface.Body],
                    IsEnabled = _ => _isLayoutDirty,
                    ExecuteAsync = _ =>
                    {
                        RipristinaLayoutPredefinitoInterno();
                        _isLayoutDirty = false;
                        return Task.CompletedTask;
                    }
                },
                new SharedGridContextAction
                {
                    Key = "banco.grid.default",
                    Header = "Ripristina layout predefinito",
                    Surfaces = [SharedGridContextSurface.Header, SharedGridContextSurface.Body],
                    ExecuteAsync = _ =>
                    {
                        RipristinaLayoutPredefinitoInterno();
                        _isLayoutDirty = true;
                        return Task.CompletedTask;
                    }
                }
            ]
        });
    }

    private static bool CanManageReorder(RigaDocumentoLocaleViewModel? row, bool shouldBeInList)
    {
        if (row is null || !row.Model.ArticoloOid.HasValue || row.Model.ArticoloOid.Value <= 0)
        {
            return false;
        }

        return shouldBeInList ? row.IsInReorderList : !row.IsInReorderList;
    }

    private GridLayoutSettings CreateBancoGridLayoutSettings()
    {
        var settings = new GridLayoutSettings();
        foreach (var definition in BancoColumnDefinitions)
        {
            settings.Columns[definition.Key] = new Banco.Vendita.Configuration.GridColumnLayoutState
            {
                Width = DocumentoRowsGrid.Columns.FirstOrDefault(column => _gridColumns.TryGetValue(definition.Key, out var tracked) && ReferenceEquals(column, tracked))?.ActualWidth > 0
                    ? DocumentoRowsGrid.Columns.First(column => _gridColumns.TryGetValue(definition.Key, out var tracked) && ReferenceEquals(column, tracked)).ActualWidth
                    : definition.DefaultWidth,
                DisplayIndex = _attachedViewModel?.GetColumnDisplayIndex(definition.Key) ?? definition.DefaultDisplayIndex,
                IsVisible = _attachedViewModel is not null && ResolveColumnVisibility(_attachedViewModel, definition.Key)
            };
        }

        return settings;
    }

    private static bool ResolveColumnVisibility(BancoViewModel viewModel, string key) => key switch
    {
        "Codice" => viewModel.ShowCodiceColumn,
        "Descrizione" => viewModel.ShowDescrizioneColumn,
        "Quantita" => viewModel.ShowQuantitaColumn,
        "Disponibilita" => viewModel.ShowDisponibilitaColumn,
        "Prezzo" => viewModel.ShowPrezzoColumn,
        "Sconto" => viewModel.ShowScontoColumn,
        "Importo" => viewModel.ShowImportoColumn,
        "Iva" => viewModel.ShowIvaColumn,
        "UnitaMisura" => viewModel.ShowUnitaMisuraColumn,
        "TipoRiga" => viewModel.ShowTipoRigaColumn,
        "Azioni" => viewModel.ShowAzioniColumn,
        _ => true
    };

    private static int ResolveBancoDisplayIndex(string key) => key switch
    {
        "Codice" => 0,
        "Descrizione" => 1,
        "UnitaMisura" => 2,
        "Quantita" => 3,
        "Prezzo" => 4,
        "Sconto" => 5,
        "Importo" => 6,
        "Disponibilita" => 7,
        "Iva" => 8,
        "TipoRiga" => 9,
        "Azioni" => 10,
        _ => 0
    };

    private static double ResolveColumnWidth(BancoViewModel viewModel, string key) => key switch
    {
        "Codice" => viewModel.CodiceColumnWidth,
        "Descrizione" => viewModel.DescrizioneColumnWidth,
        "Quantita" => viewModel.QuantitaColumnWidth,
        "Disponibilita" => 70,
        "Prezzo" => viewModel.PrezzoColumnWidth,
        "Sconto" => viewModel.ScontoColumnWidth,
        "Importo" => viewModel.ImportoColumnWidth,
        "Iva" => viewModel.IvaColumnWidth,
        "UnitaMisura" => viewModel.TipoColumnWidth,
        "TipoRiga" => viewModel.TipoRigaColumnWidth,
        "Azioni" => viewModel.AzioniColumnWidth,
        _ => 80
    };

    private void OnPos80PreviewRequested(string previewPath)
    {
        var owner = Window.GetWindow(this);

        if (_previewPos80Window is not null && _previewPos80Window.IsLoaded)
        {
            _previewPos80Window.Close();
        }

        _previewPos80Window = new Pos80PreviewWindow(previewPath)
        {
            Owner = owner
        };

        _previewPos80Window.Closed += (_, _) => _previewPos80Window = null;
        _previewPos80Window.ShowDialog();
    }

    private async void OnOpenCashRegisterOptionsRequested()
    {
        if (_attachedViewModel is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var defaults = await _attachedViewModel.LoadCashRegisterDialogDefaultsAsync();
        var window = new CashRegisterOptionsDialogWindow(
            _attachedViewModel.DocumentoLocaleCorrente,
            defaults.DeviceSerialNumber,
            defaults.ReceiptPrefix)
        {
            Owner = owner
        };

        if (window.ShowDialog() == true && window.Selection is not null)
        {
            await _attachedViewModel.RegistraOperazioneCassaAsync(window.Selection);
        }
    }

    private async Task<string?> OnPos80PrintRequestedAsync(string printPath, string? printerName)
    {
        var owner = Window.GetWindow(this);
        return await Pos80PrintWindow.PrintAsync(owner, printPath, printerName);
    }

    private void FocusDefaultInput(bool force)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible || !IsEnabled)
            {
                return;
            }

            if (!force && IsKeyboardFocusWithin)
            {
                return;
            }

            if (DataContext is BancoViewModel viewModel && !viewModel.CanModifyDocument)
            {
                FocusDocumentoRowsGrid(selectFirstColumn: true);
                return;
            }

            ArticleSearchTextBox.Focus();
            ArticleSearchTextBox.SelectAll();
            Keyboard.Focus(ArticleSearchTextBox);
        }, DispatcherPriority.Input);
    }

    private void FocusDocumentoRowsGrid(bool selectFirstColumn)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible || !IsEnabled || DocumentoRowsGrid.Items.Count == 0)
            {
                return;
            }

            var targetItem = DocumentoRowsGrid.SelectedItem ?? DocumentoRowsGrid.Items[0];
            if (targetItem is null)
            {
                return;
            }

            var targetColumn = selectFirstColumn
                ? GetFirstNavigableDocumentoColumn()
                : ResolveNavigableColumnOrFallback(DocumentoRowsGrid.CurrentCell.Column);

            DocumentoRowsGrid.SelectedItem = targetItem;
            DocumentoRowsGrid.SelectedIndex = DocumentoRowsGrid.Items.IndexOf(targetItem);
            DocumentoRowsGrid.Focus();
            Keyboard.Focus(DocumentoRowsGrid);

            EnsureSelectionAndCurrentCellConsistency(targetColumn);

            DocumentoRowsGrid.UpdateLayout();
        }, DispatcherPriority.Input);
    }

    private List<DataGridColumn> GetNavigableDocumentoColumns()
    {
        var columns = new List<DataGridColumn>();

        foreach (var column in DocumentoRowsGrid.Columns)
        {
            if (column.Visibility != Visibility.Visible)
            {
                continue;
            }

            if (IsDecorativeDocumentoColumn(column))
            {
                continue;
            }

            columns.Add(column);
        }

        return columns
            .OrderBy(column => column.DisplayIndex)
            .ToList();
    }

    private DataGridColumn? GetFirstNavigableDocumentoColumn()
    {
        return GetNavigableDocumentoColumns().FirstOrDefault();
    }

    private DataGridColumn? ResolveNavigableColumnOrFallback(DataGridColumn? currentColumn)
    {
        var navigableColumns = GetNavigableDocumentoColumns();
        if (navigableColumns.Count == 0)
        {
            return null;
        }

        if (currentColumn is not null && navigableColumns.Contains(currentColumn))
        {
            return currentColumn;
        }

        return navigableColumns[0];
    }

    private bool IsDecorativeDocumentoColumn(DataGridColumn column)
    {
        return ReferenceEquals(column, AzioniColumn);
    }

    private void CancelDocumentoGridEditAndFocusSearch()
    {
        CancelDocumentoGridEdit();
        FocusDefaultInput(force: true);
    }

    private void CancelDocumentoGridEditAndKeepGridFocus()
    {
        CancelDocumentoGridEdit();
        _ = EnsureSelectionAndCurrentCellConsistency();
        DocumentoRowsGrid.Focus();
        Keyboard.Focus(DocumentoRowsGrid);
    }

    private void CancelDocumentoGridEdit()
    {
        // ESC annulla edit corrente in modo esplicito senza commit implicito.
        DocumentoRowsGrid.CancelEdit(DataGridEditingUnit.Cell);
        DocumentoRowsGrid.CancelEdit(DataGridEditingUnit.Row);
    }

    private static bool IsIncreaseQuantityKey(KeyEventArgs e)
    {
        return e.Key == Key.Add
            || (e.Key == Key.OemPlus && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    private static bool IsDecreaseQuantityKey(KeyEventArgs e)
    {
        return e.Key == Key.Subtract || e.Key == Key.OemMinus;
    }

    private static bool IsAddToReorderKey(KeyEventArgs e)
    {
        return e.Key == Key.Multiply
            || (e.Key == Key.D8 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    private static bool IsRemoveFromReorderKey(KeyEventArgs e)
    {
        return e.Key == Key.Divide || e.Key == Key.Oem2;
    }

    private bool IsTextEditingContext()
    {
        if (Keyboard.FocusedElement is PasswordBox || Keyboard.FocusedElement is ComboBox)
        {
            return true;
        }

        if (Keyboard.FocusedElement is not TextBoxBase textBox)
        {
            return false;
        }

        // Nel box ricerca articolo vuoto consentiamo +/- per il flusso rapido banco.
        if (ReferenceEquals(textBox, ArticleSearchTextBox) && string.IsNullOrWhiteSpace(ArticleSearchTextBox.Text))
        {
            return false;
        }

        return true;
    }

    private bool TryHandleQuantityHotkeys(BancoViewModel viewModel, KeyEventArgs e)
    {
        if (IsTextEditingContext())
        {
            return false;
        }

        var isIncrease = IsIncreaseQuantityKey(e);
        var isDecrease = IsDecreaseQuantityKey(e);
        if (!isIncrease && !isDecrease)
        {
            return false;
        }

        if (!EnsureSelectionAndCurrentCellConsistency() || viewModel.RigaSelezionata is null)
        {
            return true;
        }

        var command = isIncrease
            ? viewModel.IncrementaQuantitaRigaSelezionataCommand
            : viewModel.DecrementaQuantitaRigaSelezionataCommand;

        if (!command.CanExecute(null))
        {
            return true;
        }

        command.Execute(null);
        _ = EnsureSelectionAndCurrentCellConsistency();
        DocumentoRowsGrid.Focus();
        Keyboard.Focus(DocumentoRowsGrid);
        return true;
    }

    private async Task<bool> TryHandleReorderHotkeysAsync(BancoViewModel viewModel, KeyEventArgs e)
    {
        var isAddToReorder = IsAddToReorderKey(e);
        var isRemoveFromReorder = IsRemoveFromReorderKey(e);
        if (!isAddToReorder && !isRemoveFromReorder)
        {
            return false;
        }

        if (!IsFocusInsideDocumentoGrid())
        {
            return false;
        }

        if (IsDocumentoGridCellEditing())
        {
            CancelDocumentoGridEdit();
        }

        if (!EnsureSelectionAndCurrentCellConsistency() || viewModel.RigaSelezionata is null)
        {
            return false;
        }

        if (isAddToReorder)
        {
            var handled = await viewModel.AddSelectedRowToReorderListAsync();
            if (handled)
            {
                _ = EnsureSelectionAndCurrentCellConsistency();
                DocumentoRowsGrid.Focus();
                Keyboard.Focus(DocumentoRowsGrid);
            }

            return handled;
        }

        if (isRemoveFromReorder)
        {
            var handled = await viewModel.RemoveSelectedRowFromReorderListAsync();
            if (handled)
            {
                _ = EnsureSelectionAndCurrentCellConsistency();
                DocumentoRowsGrid.Focus();
                Keyboard.Focus(DocumentoRowsGrid);
            }

            return handled;
        }

        return false;
    }

    private bool IsFocusInsideDocumentoGrid()
    {
        if (ReferenceEquals(Keyboard.FocusedElement, DocumentoRowsGrid) || DocumentoRowsGrid.IsKeyboardFocusWithin)
        {
            return true;
        }

        return Keyboard.FocusedElement is DependencyObject dependencyObject
            && TrovaAntenato<DataGridCell>(dependencyObject) is not null;
    }

    private bool IsDocumentoGridCellEditing()
    {
        if (Keyboard.FocusedElement is not DependencyObject dependencyObject)
        {
            return false;
        }

        if (ReferenceEquals(dependencyObject, ArticleSearchTextBox))
        {
            return false;
        }

        if (TrovaAntenato<DataGridCell>(dependencyObject) is not DataGridCell cell)
        {
            return false;
        }

        return cell.IsEditing || dependencyObject is TextBoxBase || dependencyObject is ComboBox;
    }

    private bool EnsureSelectionAndCurrentCellConsistency(DataGridColumn? preferredColumn = null)
    {
        if (_isSynchronizingDocumentoSelection)
        {
            return DocumentoRowsGrid.Items.Count > 0;
        }

        if (DocumentoRowsGrid.Items.Count == 0)
        {
            DocumentoRowsGrid.CurrentCell = default;
            return false;
        }

        _isSynchronizingDocumentoSelection = true;

        try
        {
            var currentCell = DocumentoRowsGrid.CurrentCell;
            var currentCellItem = currentCell.Item;
            var selectedItem = DocumentoRowsGrid.SelectedItem;

            if (selectedItem is null
                && currentCellItem is not null
                && DocumentoRowsGrid.Items.IndexOf(currentCellItem) >= 0)
            {
                selectedItem = currentCellItem;
            }

            selectedItem ??= DocumentoRowsGrid.Items[0];
            if (selectedItem is null)
            {
                DocumentoRowsGrid.CurrentCell = default;
                return false;
            }

            if (!ReferenceEquals(DocumentoRowsGrid.SelectedItem, selectedItem))
            {
                DocumentoRowsGrid.SelectedItem = selectedItem;
            }

            var targetColumn = ResolveNavigableColumnOrFallback(preferredColumn ?? currentCell.Column);
            if (targetColumn is null)
            {
                DocumentoRowsGrid.ScrollIntoView(selectedItem);
                return true;
            }

            var currentCellOnSelectedRow = currentCell.Column is not null
                && ReferenceEquals(currentCell.Item, selectedItem)
                && ReferenceEquals(currentCell.Column, targetColumn);

            if (!currentCellOnSelectedRow)
            {
                DocumentoRowsGrid.CurrentCell = new DataGridCellInfo(selectedItem, targetColumn);
            }

            DocumentoRowsGrid.ScrollIntoView(selectedItem, targetColumn);
            return true;
        }
        finally
        {
            _isSynchronizingDocumentoSelection = false;
        }
    }

    private bool MoveDocumentoRowsSelection(int deltaRows)
    {
        if (!EnsureSelectionAndCurrentCellConsistency())
        {
            return false;
        }

        var currentItem = DocumentoRowsGrid.SelectedItem ?? DocumentoRowsGrid.Items[0];
        var currentIndex = DocumentoRowsGrid.Items.IndexOf(currentItem);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var targetIndex = currentIndex + deltaRows;
        if (targetIndex < 0)
        {
            targetIndex = 0;
        }
        else if (targetIndex >= DocumentoRowsGrid.Items.Count)
        {
            targetIndex = DocumentoRowsGrid.Items.Count - 1;
        }

        if (targetIndex < 0 || targetIndex >= DocumentoRowsGrid.Items.Count)
        {
            return false;
        }

        var targetItem = DocumentoRowsGrid.Items[targetIndex];
        if (targetItem is null)
        {
            return false;
        }

        var targetColumn = ResolveNavigableColumnOrFallback(DocumentoRowsGrid.CurrentCell.Column);
        DocumentoRowsGrid.Focus();
        DocumentoRowsGrid.SelectedItem = targetItem;
        EnsureSelectionAndCurrentCellConsistency(targetColumn);
        Keyboard.Focus(DocumentoRowsGrid);
        return true;
    }

    private bool MoveDocumentoRowsColumn(int deltaColumns)
    {
        if (!EnsureSelectionAndCurrentCellConsistency())
        {
            return false;
        }

        var columns = GetNavigableDocumentoColumns();
        if (columns.Count == 0)
        {
            return false;
        }

        var currentColumn = ResolveNavigableColumnOrFallback(DocumentoRowsGrid.CurrentCell.Column);
        var currentIndex = 0;
        if (currentColumn is not null)
        {
            currentIndex = columns.IndexOf(currentColumn);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }
        }

        var targetIndex = currentIndex + deltaColumns;
        targetIndex = Math.Max(0, Math.Min(columns.Count - 1, targetIndex));

        var targetColumn = columns[targetIndex];
        var targetItem = DocumentoRowsGrid.SelectedItem ?? DocumentoRowsGrid.Items[0];
        if (targetItem is null)
        {
            return false;
        }

        DocumentoRowsGrid.SelectedItem = targetItem;
        EnsureSelectionAndCurrentCellConsistency(targetColumn);

        // Focalizza la cella effettiva affinché il focus visivo appaia sulla cella target.
        _ = Dispatcher.InvokeAsync(() => FocusCellaCorrente(), DispatcherPriority.Input);
        return true;
    }

    private void FocusCellaCorrente()
    {
        var cellInfo = DocumentoRowsGrid.CurrentCell;
        if (cellInfo.Item is null || cellInfo.Column is null)
        {
            return;
        }

        DocumentoRowsGrid.ScrollIntoView(cellInfo.Item, cellInfo.Column);

        var riga = DocumentoRowsGrid.ItemContainerGenerator.ContainerFromItem(cellInfo.Item) as DataGridRow;
        if (riga is null)
        {
            return;
        }

        var presenter = TrovaFiglio<DataGridCellsPresenter>(riga);
        if (presenter is null)
        {
            return;
        }

        for (var i = 0; i < presenter.Items.Count; i++)
        {
            if (presenter.ItemContainerGenerator.ContainerFromIndex(i) is DataGridCell cella
                && ReferenceEquals(cella.Column, cellInfo.Column))
            {
                cella.Focus();
                return;
            }
        }
    }

    private static T? TrovaFiglio<T>(DependencyObject elemento) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(elemento); i++)
        {
            var figlio = System.Windows.Media.VisualTreeHelper.GetChild(elemento, i);
            if (figlio is T trovato)
            {
                return trovato;
            }

            var risultato = TrovaFiglio<T>(figlio);
            if (risultato is not null)
            {
                return risultato;
            }
        }

        return null;
    }

    private static T? TrovaAntenato<T>(DependencyObject? elemento) where T : DependencyObject
    {
        while (elemento is not null)
        {
            if (elemento is T trovato)
            {
                return trovato;
            }

            elemento = System.Windows.Media.VisualTreeHelper.GetParent(elemento);
        }

        return null;
    }

    private void RegisterWidthChange(DataGridColumn column, string key)
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        descriptor?.AddValueChanged(column, async (_, _) =>
        {
            if (!_columnsInitialized || DataContext is not BancoViewModel viewModel)
            {
                return;
            }

            var width = column.ActualWidth;
            if (width <= 0)
            {
                return;
            }

            _isLayoutDirty = true;
            await viewModel.SaveColumnWidthAsync(key, width);
        });
    }

    private void RegisterDisplayIndexChange(DataGridColumn column, string key)
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
        descriptor?.AddValueChanged(column, async (_, _) => await PersistColumnDisplayIndexAsync(key, column.DisplayIndex));
    }

    private void MemorizzaLayoutPredefinitoCorrente()
    {
        _layoutPredefinitoColonne = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Codice"] = LarghezzaColonna(CodiceColumn),
            ["Descrizione"] = LarghezzaColonna(DescrizioneColumn),
            ["Quantita"] = LarghezzaColonna(QuantitaColumn),
            ["Prezzo"] = LarghezzaColonna(PrezzoColumn),
            ["Iva"] = LarghezzaColonna(IvaColumn),
            ["UnitaMisura"] = LarghezzaColonna(UnitaMisuraColumn),
            ["Sconto"] = LarghezzaColonna(ScontoColumn),
            ["Importo"] = LarghezzaColonna(ImportoColumn),
            ["TipoRiga"] = LarghezzaColonna(TipoRigaColumn),
            ["Azioni"] = LarghezzaColonna(AzioniColumn),
        };
    }

    private DataGridColumn? GetColonnaPerChiave(string chiave) => chiave switch
    {
        "Codice" => CodiceColumn,
        "Descrizione" => DescrizioneColumn,
        "Quantita" => QuantitaColumn,
        "Prezzo" => PrezzoColumn,
        "Iva" => IvaColumn,
        "UnitaMisura" => UnitaMisuraColumn,
        "Sconto" => ScontoColumn,
        "Importo" => ImportoColumn,
        "TipoRiga" => TipoRigaColumn,
        "Azioni" => AzioniColumn,
        _ => null
    };

    private static double LarghezzaColonna(DataGridColumn colonna)
    {
        return colonna.Width.IsAbsolute ? colonna.Width.Value : colonna.ActualWidth;
    }

    private void SalvaModificheGrigliaMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        _isLayoutDirty = false;
    }

    private void RipristinaLayoutGrigliaMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        RipristinaLayoutPredefinitoInterno();
        _isLayoutDirty = false;
    }

    private void RipristinaLayoutPredefinitoMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        RipristinaLayoutPredefinitoInterno();
        _isLayoutDirty = true;
    }

    private void RipristinaLayoutPredefinitoInterno()
    {
        if (_layoutPredefinitoColonne.Count == 0)
        {
            MemorizzaLayoutPredefinitoCorrente();
        }

        foreach (var (chiave, larghezza) in _layoutPredefinitoColonne)
        {
            var colonna = GetColonnaPerChiave(chiave);
            if (colonna is not null)
            {
                colonna.Width = new DataGridLength(larghezza);
            }
        }
    }

    private void DocumentoRowsGrid_OnPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is not TextBox textBox)
        {
            return;
        }

        if (!PosAmountTextBoxBehavior.GetIsEnabled(textBox))
        {
            return;
        }

        // Le celle importo della griglia devono entrare in edit come i campi pagamento:
        // focus pieno sulla textbox e cursore sempre in coda al valore formattato.
        var initialDigitText = e.EditingEventArgs is TextCompositionEventArgs textArgs
            && !string.IsNullOrWhiteSpace(textArgs.Text)
            && textArgs.Text.All(char.IsDigit)
            ? textArgs.Text
            : null;

        if (initialDigitText is not null && e.EditingEventArgs is TextCompositionEventArgs initialTextArgs)
        {
            initialTextArgs.Handled = true;
        }

        _ = textBox.Dispatcher.BeginInvoke(() =>
        {
            if (!textBox.IsVisible || !textBox.IsEnabled)
            {
                return;
            }

            textBox.Focus();
            Keyboard.Focus(textBox);
            textBox.SelectionLength = 0;
            textBox.CaretIndex = textBox.Text?.Length ?? 0;

            // Il primo digit che apre l'editing della cella deve passare dal behavior POS
            // prima che il DataGrid lo trasformi in testo intero nella textbox.
            if (initialDigitText is not null
            && PosAmountTextBoxBehavior.GetIsEnabled(textBox))
            {
            PosAmountTextBoxBehavior.ResetToZero(textBox);
            PosAmountTextBoxBehavior.TryProcessDigitInput(textBox, initialDigitText);
                textBox.SelectionLength = 0;
                textBox.CaretIndex = textBox.Text?.Length ?? 0;
            }
        }, DispatcherPriority.Input);
    }

    // -------------------------------------------------------------------------

    private async Task PersistColumnDisplayIndexAsync(string key, int displayIndex)
    {
        if (!_columnsInitialized || DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        await viewModel.SaveColumnDisplayIndexAsync(key, displayIndex);
    }

    private void ApplyColumnVisibility(BancoViewModel viewModel)
    {
        CodiceColumn.Visibility = viewModel.ShowCodiceColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        DescrizioneColumn.Visibility = viewModel.ShowDescrizioneColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        QuantitaColumn.Visibility = viewModel.ShowQuantitaColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        DisponibilitaColumn.Visibility = viewModel.ShowDisponibilitaColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        PrezzoColumn.Visibility = viewModel.ShowPrezzoColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        IvaColumn.Visibility = viewModel.ShowIvaColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        UnitaMisuraColumn.Visibility = viewModel.ShowUnitaMisuraColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        ScontoColumn.Visibility = viewModel.ShowScontoColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        ImportoColumn.Visibility = viewModel.ShowImportoColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        TipoRigaColumn.Visibility = viewModel.ShowTipoRigaColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        AzioniColumn.Visibility = viewModel.ShowAzioniColumn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    private void AttachViewModel(BancoViewModel viewModel)
    {
        if (ReferenceEquals(_attachedViewModel, viewModel))
        {
            return;
        }

        _attachedViewModel = viewModel;
        _attachedViewModel.FocusArticleSearchRequested += OnFocusArticleSearchRequested;
        _attachedViewModel.OpenCashRegisterRequested += OnOpenCashRegisterOptionsRequested;
        _attachedViewModel.Pos80PreviewRequested += OnPos80PreviewRequested;
        _attachedViewModel.Pos80PrintRequested = OnPos80PrintRequestedAsync;
        _attachedViewModel.OpenStoricoAcquistiRequested += OnOpenStoricoAcquistiRequested;
        _attachedViewModel.OpenPurchaseHistoryRequested += OnOpenPurchaseHistoryRequested;
        _attachedViewModel.ArticleLookupRequested = OnArticleLookupRequestedAsync;
        _attachedViewModel.ArticleVariantSelectionRequested = OnArticleVariantSelectionRequestedAsync;
        _attachedViewModel.PromotionConfirmationRequested += OnPromotionConfirmationRequestedAsync;
        _attachedViewModel.PosManualWarningRequested += OnPosManualWarningRequestedAsync;
        _attachedViewModel.ArticleQuantitySelectionRequested += OnArticleQuantitySelectionRequestedAsync;
        _attachedViewModel.NegativeAvailabilityDecisionRequested += OnNegativeAvailabilityDecisionRequestedAsync;
        _attachedViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel is null)
        {
            return;
        }

        _attachedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _attachedViewModel.OpenStoricoAcquistiRequested -= OnOpenStoricoAcquistiRequested;
        _attachedViewModel.OpenPurchaseHistoryRequested -= OnOpenPurchaseHistoryRequested;
        _attachedViewModel.FocusArticleSearchRequested -= OnFocusArticleSearchRequested;
        _attachedViewModel.OpenCashRegisterRequested -= OnOpenCashRegisterOptionsRequested;
        _attachedViewModel.Pos80PreviewRequested -= OnPos80PreviewRequested;
        _attachedViewModel.ArticleLookupRequested = null;
        _attachedViewModel.ArticleVariantSelectionRequested = null;
        _attachedViewModel.Pos80PrintRequested = null;
        _attachedViewModel.PromotionConfirmationRequested -= OnPromotionConfirmationRequestedAsync;
        _attachedViewModel.PosManualWarningRequested -= OnPosManualWarningRequestedAsync;
        _attachedViewModel.ArticleQuantitySelectionRequested -= OnArticleQuantitySelectionRequestedAsync;
        _attachedViewModel.NegativeAvailabilityDecisionRequested -= OnNegativeAvailabilityDecisionRequestedAsync;
        _attachedViewModel = null;
    }

    private void OnOpenStoricoAcquistiRequested(StoricoAcquistiViewModel storicoVm)
    {
        var window = new StoricoAcquistiWindow(storicoVm)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private void OnOpenPurchaseHistoryRequested(PurchaseHistoryViewModel storicoVm)
    {
        var window = new PurchaseHistoryWindow(storicoVm)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }

    private Task<GestionaleArticleSearchResult?> OnArticleLookupRequestedAsync(ArticleLookupRequest request)
    {
        var lookupViewModel = ActivatorUtilities.CreateInstance<ArticleLookupViewModel>(App.Services, request);
        var window = new ArticleLookupWindow(lookupViewModel)
        {
            Owner = Window.GetWindow(this)
        };

        _isArticleLookupDialogOpen = true;
        _manualArticleLookupTimer.Stop();

        try
        {
            var dialogResult = window.ShowDialog();
            return Task.FromResult(dialogResult == true ? window.SelectedArticle : null);
        }
        finally
        {
            _isArticleLookupDialogOpen = false;
            _suppressManualArticleLookupScheduling = true;
            _ = Dispatcher.BeginInvoke(() =>
            {
                _suppressManualArticleLookupScheduling = false;
                ArticleSearchTextBox.Focus();
                ArticleSearchTextBox.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private Task<GestionaleArticleSearchResult?> OnArticleVariantSelectionRequestedAsync(
        GestionaleArticleSearchResult parentArticle,
        IReadOnlyList<GestionaleArticleSearchResult> variants)
    {
        var dialog = new ArticleVariantSelectionDialogWindow(parentArticle, variants)
        {
            Owner = Window.GetWindow(this)
        };

        var dialogResult = dialog.ShowDialog();
        return Task.FromResult(dialogResult == true ? dialog.SelectedVariant : null);
    }

    private Task<PointsRewardRule?> OnPromotionConfirmationRequestedAsync(PromotionEvaluationResult evaluation)
    {
        var rewardRules = evaluation.EligibleRewardRules.Count > 0
            ? evaluation.EligibleRewardRules
            : evaluation.RewardRule is null
                ? []
                : [evaluation.RewardRule];

        if (rewardRules.Count <= 1)
        {
            var selectedRule = rewardRules.FirstOrDefault();
            var dialog = new ConfirmationDialogWindow(
                "Banco / premio",
                evaluation.Title,
                evaluation.Message,
                "Applica premio",
                "Non adesso",
                selectedRule is null ? string.Empty : $"Premio: {selectedRule.RewardDescription}")
            {
                Owner = Window.GetWindow(this)
            };

            return Task.FromResult(dialog.ShowDialog() == true ? selectedRule : null);
        }

        var selectionDialog = new RewardSelectionDialogWindow(
            "Banco / premio",
            "Scegli il premio da applicare",
            "Il cliente ha raggiunto piu` soglie premio. Seleziona il premio da applicare sul documento corrente.",
            rewardRules)
        {
            Owner = Window.GetWindow(this)
        };

        return Task.FromResult(selectionDialog.ShowDialog() == true ? selectionDialog.SelectedRewardRule : null);
    }

    private Task<decimal?> OnArticleQuantitySelectionRequestedAsync(
        GestionaleArticleSearchResult articolo,
        GestionaleArticlePricingDetail dettaglioPrezzi,
        decimal quantitaPredefinita)
    {
        var dialog = new ArticleQuantitySelectionDialogWindow(articolo, dettaglioPrezzi, quantitaPredefinita)
        {
            Owner = Window.GetWindow(this)
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.SelectedQuantity : null);
    }

    private Task<NegativeAvailabilityDecision> OnNegativeAvailabilityDecisionRequestedAsync(
        GestionaleArticleSearchResult articolo,
        decimal quantitaRichiesta)
    {
        var dialog = new NegativeAvailabilityDialogWindow(articolo, quantitaRichiesta)
        {
            Owner = Window.GetWindow(this)
        };

        return Task.FromResult(dialog.ShowDialog() == true
            ? dialog.Decision
            : NegativeAvailabilityDecision.Annulla);
    }

    private Task<PosManualWarningChoice> OnPosManualWarningRequestedAsync(PosPaymentResult result)
    {
        var dialog = new PosManualWarningDialogWindow(result.Message)
        {
            Owner = Window.GetWindow(this)
        };

        return Task.FromResult(dialog.ShowDialog() == true
            ? dialog.Choice
            : PosManualWarningChoice.TornaScheda);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not BancoViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName is nameof(BancoViewModel.ShowCodiceColumn)
            or nameof(BancoViewModel.ShowDescrizioneColumn)
            or nameof(BancoViewModel.ShowQuantitaColumn)
            or nameof(BancoViewModel.ShowDisponibilitaColumn)
            or nameof(BancoViewModel.ShowPrezzoColumn)
            or nameof(BancoViewModel.ShowIvaColumn)
            or nameof(BancoViewModel.ShowUnitaMisuraColumn)
            or nameof(BancoViewModel.ShowScontoColumn)
            or nameof(BancoViewModel.ShowImportoColumn)
            or nameof(BancoViewModel.ShowTipoRigaColumn)
            or nameof(BancoViewModel.ShowAzioniColumn))
        {
            Dispatcher.Invoke(() => ApplyColumnVisibility(viewModel));
        }

    }

    private void PaymentButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        var tipoPagamento = button.CommandParameter as string;
        if (string.IsNullOrWhiteSpace(tipoPagamento))
        {
            return;
        }

        viewModel.ApplicaImportoPagamentoDiretto(tipoPagamento);
        e.Handled = true;
    }

    // Click sull'intera riga pagamento (escluso il TextBox che gestisce l'input manuale)
    private void PagamentoRiga_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Se il click arriva da un TextBox lascia passare per input manuale
        if (e.OriginalSource is DependencyObject source && TrovaParente<TextBox>(source) is not null)
        {
            return;
        }

        if (sender is not FrameworkElement element || DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        var tipo = element.Tag as string;
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            viewModel.ApplicaImportoPagamentoDiretto(tipo);
            e.Handled = true;
        }
    }

    private void PaymentTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is BancoViewModel viewModel)
        {
            viewModel.CommitPaymentInputsToDocument();
        }
    }

    private void PaymentTextBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var usePosAmountBehavior = PosAmountTextBoxBehavior.GetIsEnabled(textBox);
        if (!textBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            textBox.Focus();
        }

        if (usePosAmountBehavior)
        {
            textBox.SelectionLength = 0;
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
            return;
        }

        textBox.SelectAll();
    }

    private void PaymentTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        viewModel.CommitPaymentInputsToDocument();
        e.Handled = true;
        if (sender is Control control)
        {
            control.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    // Risale l'albero visuale fino a trovare un elemento del tipo richiesto
    private static T? TrovaParente<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T found)
            {
                return found;
            }

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private async void ScontrinoButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteScontrinoShortcutAsync();
    }

    private async void CortesiaButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCortesiaShortcutAsync();
    }

    private async void AnteprimaPos80BuoniButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel || !viewModel.CanEmettiCortesia)
        {
            return;
        }

        await viewModel.AnteprimaPos80BuoniAsync();
    }

    private async void SalvaDocumentoButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteSalvaShortcutAsync();
    }

    private void AzzeraContenutoButton_OnClick(object sender, RoutedEventArgs e)
    {
        HandleAzzeraContenutoAction();
    }

    private void HandleAzzeraContenutoAction()
    {
        if (DataContext is not BancoViewModel viewModel || !viewModel.CanAzzeraContenuto)
        {
            return;
        }

        var dialog = new AzzeraContenutoChoiceDialogWindow
        {
            Owner = Window.GetWindow(this)
        };

        var dialogResult = dialog.ShowDialog();
        if (dialogResult != true)
        {
            return;
        }

        switch (dialog.Choice)
        {
            case AzzeraContenutoChoice.AggiungiArticolo:
                viewModel.PrepareForArticleInsertionAfterAnnullaPrompt();
                break;

            case AzzeraContenutoChoice.Annulla:
                viewModel.AzzeraDocumentoCommand.Execute(null);
                break;
        }
    }

    private async void CancellaSchedaButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel || !viewModel.CanCancellaScheda)
        {
            return;
        }

        var confirmDialog = new ConfirmationDialogWindow(
            "Banco / scheda",
            "Conferma cancellazione vendita",
            "Vuoi cancellare definitivamente la vendita corrente?",
            "Cancella vendita",
            "Indietro",
            viewModel.HasRiferimentiUfficialiDocumentoCorrente
                ? "L'operazione cancella il documento dal db_diltech e apre subito una nuova scheda pulita."
                : "L'operazione annulla il lavoro corrente e apre subito una nuova scheda pulita.")
        {
            Owner = Window.GetWindow(this)
        };

        if (confirmDialog.ShowDialog() != true)
        {
            return;
        }

        await viewModel.CancellaSchedaAsync();
    }

    private async void ArticleSearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            ResetArticleSearchInputTracking();
            e.Handled = true;
            FocusDocumentoRowsGrid(selectFirstColumn: true);
            return;
        }

        if (e.Key is Key.Back or Key.Delete or Key.Left or Key.Right or Key.Home or Key.End or Key.Tab or Key.Escape)
        {
            ResetArticleSearchInputTracking();
            if (e.Key == Key.Escape)
            {
                _manualArticleLookupTimer.Stop();
            }
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        _manualArticleLookupTimer.Stop();
        var isScannerSubmission = IsProbableArticleScannerSubmission();
        ResetArticleSearchInputTracking();
        e.Handled = true;
        if (isScannerSubmission)
        {
            await viewModel.ExecuteArticleSearchAsync(fromScanner: true);
            return;
        }

        await viewModel.OpenManualArticleLookupAsync(viewModel.SearchArticoloText);
    }

    private void ArticleSearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressManualArticleLookupScheduling || _isArticleLookupDialogOpen || DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (!viewModel.CanModifyDocument)
        {
            _manualArticleLookupTimer.Stop();
            return;
        }

        var searchText = viewModel.SearchArticoloText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _manualArticleLookupTimer.Stop();
            return;
        }

        _manualArticleLookupTimer.Stop();
        _manualArticleLookupTimer.Start();
    }

    private void ArticleSearchTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text) || Keyboard.Modifiers != ModifierKeys.None)
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

    private void ArticleSearchTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        ResetArticleSearchInputTracking();
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

    private async void CustomerSearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Down && viewModel.IsCustomerPopupOpen && CustomerLookupList.Items.Count > 0)
        {
            if (CustomerLookupList.SelectedItem is null)
            {
                CustomerLookupList.SelectedIndex = 0;
            }

            e.Handled = true;
            _ = Dispatcher.BeginInvoke(() =>
            {
                CustomerLookupList.Focus();
                if (CustomerLookupList.SelectedItem is not null)
                {
                    CustomerLookupList.ScrollIntoView(CustomerLookupList.SelectedItem);
                }
            }, DispatcherPriority.Input);
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await viewModel.ExecuteCustomerSearchAsync();
    }

    private void CustomerSearchTextBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
            e.Handled = true;
        }
    }

    private void CustomerSearchTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        _ = textBox.Dispatcher.BeginInvoke(() => textBox.SelectAll(), DispatcherPriority.Input);
    }

    private void CustomerSearchTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel ||
            sender is not TextBox textBox)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(textBox.Text))
        {
            return;
        }

        if (e.NewFocus is DependencyObject destination &&
            TrovaParente<ListBoxItem>(destination) is not null)
        {
            return;
        }

        viewModel.RipristinaTestoClienteCorrente();
    }

    private void CustomerLookupList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel ||
            CustomerLookupList.SelectedItem is not GestionaleCustomerSummary cliente)
        {
            return;
        }

        viewModel.SelezionaClienteDaPopup(cliente, applyImmediately: true);
        CustomerSearchTextBox.Focus();
    }

    private void CustomerLookupList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = TrovaParente<ListBoxItem>(source);
        if (item?.DataContext is not GestionaleCustomerSummary cliente)
        {
            return;
        }

        viewModel.SelezionaClienteDaPopup(cliente, applyImmediately: true);
        CustomerSearchTextBox.Focus();
        e.Handled = true;
    }

    private void CustomerLookupList_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            DataContext is not BancoViewModel viewModel ||
            CustomerLookupList.SelectedItem is not GestionaleCustomerSummary cliente)
        {
            return;
        }

        e.Handled = true;
        viewModel.SelezionaClienteDaPopup(cliente, applyImmediately: true);
        CustomerSearchTextBox.Focus();
    }

    private async void DocumentoRowsGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (IsDocumentoGridCellEditing())
            {
                CancelDocumentoGridEditAndKeepGridFocus();
            }
            else
            {
                CancelDocumentoGridEditAndFocusSearch();
            }
            return;
        }

        if (await TryHandleReorderHotkeysAsync(viewModel, e))
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBoxBase)
        {
            return;
        }

        if (TryHandleQuantityHotkeys(viewModel, e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Insert)
        {
            e.Handled = viewModel.InserisciRigaManuale();
            if (e.Handled)
            {
                _ = Dispatcher.BeginInvoke(() =>
                {
                    DocumentoRowsGrid.Focus();
                    DocumentoRowsGrid.CurrentCell = new DataGridCellInfo(
                        DocumentoRowsGrid.SelectedItem,
                        DescrizioneColumn);
                    DocumentoRowsGrid.BeginEdit();
                }, DispatcherPriority.Input);
            }

            return;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = MoveDocumentoRowsSelection(-1);
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = MoveDocumentoRowsSelection(1);
            return;
        }

        if (e.Key == Key.Left)
        {
            e.Handled = MoveDocumentoRowsColumn(-1);
            return;
        }

        if (e.Key == Key.Right)
        {
            e.Handled = MoveDocumentoRowsColumn(1);
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = viewModel.ConvertiRigaSelezionataInManuale();
        }
    }

    private async void DocumentoRowsGrid_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        e.Handled = await TryHandleReorderTextInputAsync(viewModel, e.Text, forceGlobal: false);
    }

    private void DocumentoRowsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = EnsureSelectionAndCurrentCellConsistency();
    }

    private void DocumentoRowsGrid_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = EnsureSelectionAndCurrentCellConsistency();
    }

    private async void BancoView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (IsDocumentoGridCellEditing())
            {
                CancelDocumentoGridEditAndKeepGridFocus();
            }
            else
            {
                CancelDocumentoGridEditAndFocusSearch();
            }
            return;
        }

        if (await TryHandleReorderHotkeysAsync(viewModel, e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F1)
        {
            e.Handled = true;
            viewModel.ApriStoricoAcquistiArticoloDaTastiera();
            return;
        }

        if (e.Key == Key.F2)
        {
            if (viewModel.NuovoDocumentoCommand.CanExecute(null))
            {
                e.Handled = true;
                viewModel.NuovoDocumentoCommand.Execute(null);
            }

            return;
        }

        if (e.Key == Key.F4)
        {
            e.Handled = true;
            await ExecuteSalvaShortcutAsync();
            return;
        }

        if (e.Key == Key.F3)
        {
            HandleAzzeraContenutoAction();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            e.Handled = true;
            await ExecuteCortesiaShortcutAsync();
            return;
        }

        if (e.Key == Key.F8)
        {
            e.Handled = true;
            await ExecuteScontrinoShortcutAsync();
            return;
        }

        if (e.Key == Key.F10)
        {
            if (viewModel.CanPrintPos80)
            {
                e.Handled = true;
                await viewModel.StampaPos80Async();
            }

            return;
        }

        if (e.Key == Key.Insert)
        {
            if (viewModel.InserisciRigaManuale())
            {
                e.Handled = true;
                _ = Dispatcher.BeginInvoke(() =>
                {
                    DocumentoRowsGrid.Focus();
                    DocumentoRowsGrid.CurrentCell = new DataGridCellInfo(
                        DocumentoRowsGrid.SelectedItem,
                        DescrizioneColumn);
                    DocumentoRowsGrid.BeginEdit();
                }, DispatcherPriority.Input);
            }

            return;
        }

        if (TryHandleQuantityHotkeys(viewModel, e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete
            && Keyboard.FocusedElement is not TextBoxBase
            && Keyboard.FocusedElement is not PasswordBox)
        {
            e.Handled = viewModel.ConvertiRigaSelezionataInManuale();
        }
    }

    private async Task ExecuteScontrinoShortcutAsync()
    {
        if (DataContext is not BancoViewModel viewModel || !viewModel.CanEmettiScontrino)
        {
            return;
        }

        if (!BancoDefaultPaymentInteractionHelper.EnsureDefaultCashPayment(viewModel, Window.GetWindow(this), "Scontrino"))
        {
            return;
        }

        if (viewModel.RichiedeConfermaRistampa)
        {
            var dialog = new ConfirmationDialogWindow(
                "Banco / fiscale",
                "Conferma ristampa",
                "Il documento risulta gia` inviato alla stampa fiscale. Vuoi confermare la richiesta di ristampa?",
                "Conferma ristampa",
                "Annulla",
                "L'operazione richiede una nuova emissione del documento.")
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await viewModel.EmettiScontrinoAsync(confermaRistampa: true);
            return;
        }

        await viewModel.EmettiScontrinoAsync();
    }

    private async Task ExecuteCortesiaShortcutAsync()
    {
        if (DataContext is not BancoViewModel viewModel || !viewModel.CanEmettiCortesia)
        {
            return;
        }

        if (!BancoDefaultPaymentInteractionHelper.EnsureDefaultCashPayment(viewModel, Window.GetWindow(this), "Cortesia"))
        {
            return;
        }

        await viewModel.EmettiCortesiaAsync();
    }

    private async Task ExecuteSalvaShortcutAsync()
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        await BancoSaveInteractionHelper.ExecuteOfficialSaveAsync(viewModel, Window.GetWindow(this));
    }

    private async void BancoView_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (DataContext is not BancoViewModel viewModel)
        {
            return;
        }

        e.Handled = await TryHandleReorderTextInputAsync(viewModel, e.Text, forceGlobal: true);
    }

    private async Task<bool> TryHandleReorderTextInputAsync(BancoViewModel viewModel, string? text, bool forceGlobal)
    {
        if (text is not "*" and not "/")
        {
            return false;
        }

        var focusInsideGrid = IsFocusInsideDocumentoGrid();
        if (!forceGlobal && !focusInsideGrid)
        {
            return false;
        }

        var previousFocus = focusInsideGrid ? null : Keyboard.FocusedElement;

        if (IsDocumentoGridCellEditing())
        {
            CancelDocumentoGridEdit();
        }

        var hasSelection = EnsureSelectionAndCurrentCellConsistency() && viewModel.RigaSelezionata is not null;
        if (hasSelection)
        {
            if (text == "*")
            {
                await viewModel.AddSelectedRowToReorderListAsync();
            }
            else
            {
                await viewModel.RemoveSelectedRowFromReorderListAsync();
            }
        }

        if (focusInsideGrid)
        {
            _ = EnsureSelectionAndCurrentCellConsistency();
            DocumentoRowsGrid.Focus();
            Keyboard.Focus(DocumentoRowsGrid);
            return true;
        }

        if (previousFocus is UIElement { IsVisible: true, Focusable: true } uiElement)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                uiElement.Focus();
                Keyboard.Focus(uiElement);
            }, DispatcherPriority.Input);
        }
        else if (previousFocus is ContentElement contentElement)
        {
            _ = Dispatcher.BeginInvoke(() => Keyboard.Focus(contentElement), DispatcherPriority.Input);
        }

        return true;
    }

    // Clip griglia documento: applicato al primo layout e ad ogni resize
}
