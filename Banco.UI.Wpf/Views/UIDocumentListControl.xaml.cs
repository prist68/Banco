using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using Banco.UI.Shared.Grid;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Views;

public partial class UIDocumentListControl : UserControl
{
    private DataGridColumnManager? _columnManager;
    private SharedGridContextMenuController? _contextMenuController;
    private DataGridColumnManager? _detailColumnManager;
    private SharedGridContextMenuController? _detailContextMenuController;
    private bool _columnsInitialized;
    private DocumentListViewModel? _attachedViewModel;
    private Window? _hostWindow;
    private ScrollViewer? _documentsGridScrollViewer;
    private ScrollViewer? _footerGridScrollViewer;
    private bool _isSyncingPeriodFilterSelection;
    private readonly Dictionary<string, DataGridColumn> _gridColumns;
    private readonly Dictionary<string, DataGridColumn> _footerColumns;
    private readonly Dictionary<string, DataGridColumn> _detailGridColumns;
    private readonly DocumentListSharedUiSupport _sharedUiSupport;
    private readonly IReadOnlyList<GestionaleLookupOption> _periodFilterOptions;

    public UIDocumentListControl()
    {
        InitializeComponent();
        _gridColumns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
        {
            ["Status"] = StatusColumn,
            ["Origine"] = OrigineColumn,
            ["Oid"] = OidColumn,
            ["Documento"] = DocumentoColumn,
            ["Data"] = DataColumn,
            ["Operatore"] = OperatoreColumn,
            ["Cliente"] = ClienteColumn,
            ["Totale"] = TotaleColumn,
            ["Punti"] = PuntiColumn,
            ["PagContanti"] = PagContantiColumn,
            ["PagCarta"] = PagCartaColumn,
            ["Cortesia"] = CortesiaColumn,
            ["PagWeb"] = PagWebColumn,
            ["PagBuoni"] = PagBuoniColumn,
            ["PagSospeso"] = PagSospesoColumn,
            ["ResiduoPagamento"] = ResiduoColumn,
            ["DaFiscalizzare"] = DaFiscalizzareColumn,
            ["StatoDocumento"] = StatoColumn,
            ["Scontrino"] = ScontrinoColumn,
            ["Actions"] = ActionsColumn
        };
        _footerColumns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
        {
            ["Status"] = FooterStatusColumn,
            ["Origine"] = FooterOrigineColumn,
            ["Oid"] = FooterOidColumn,
            ["Documento"] = FooterDocumentoColumn,
            ["Data"] = FooterDataColumn,
            ["Operatore"] = FooterOperatoreColumn,
            ["Cliente"] = FooterClienteColumn,
            ["Totale"] = FooterTotaleColumn,
            ["Punti"] = FooterPuntiColumn,
            ["PagContanti"] = FooterPagContantiColumn,
            ["PagCarta"] = FooterPagCartaColumn,
            ["Cortesia"] = FooterCortesiaColumn,
            ["PagWeb"] = FooterPagWebColumn,
            ["PagBuoni"] = FooterPagBuoniColumn,
            ["PagSospeso"] = FooterPagSospesoColumn,
            ["ResiduoPagamento"] = FooterResiduoColumn,
            ["DaFiscalizzare"] = FooterDaFiscalizzareColumn,
            ["StatoDocumento"] = FooterStatoColumn,
            ["Scontrino"] = FooterScontrinoColumn,
            ["Actions"] = FooterActionsColumn
        };
        _detailGridColumns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
        {
            ["OrdineRiga"] = DetailOrdineRigaColumn,
            ["CodiceArticolo"] = DetailCodiceArticoloColumn,
            ["Descrizione"] = DetailDescrizioneColumn,
            ["Quantita"] = DetailQuantitaColumn,
            ["PrezzoUnitario"] = DetailPrezzoUnitarioColumn,
            ["ScontoPercentuale"] = DetailScontoPercentualeColumn,
            ["ImportoRiga"] = DetailImportoRigaColumn
        };
        _sharedUiSupport = new DocumentListSharedUiSupport(
            () => DataContext as DocumentListViewModel,
            () => Window.GetWindow(this),
            () => _columnManager,
            ApplyColumnVisibility,
            RefreshFooterRow,
            SyncFooterColumnsFromGrid,
            DeleteSelectedLocalDocumentsCoreAsync,
            DeleteRowCoreAsync,
            SyncGridSelectionToViewModel);
        _periodFilterOptions =
        [
            new GestionaleLookupOption { Oid = 1, Label = "Personalizzato" },
            new GestionaleLookupOption { Oid = 2, Label = "Oggi" },
            new GestionaleLookupOption { Oid = 3, Label = "Settimana" },
            new GestionaleLookupOption { Oid = 4, Label = "Mese" }
        ];
        PeriodFilterPicker.ItemsSource = _periodFilterOptions;
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(Root_OnPreviewKeyDown), true);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_columnsInitialized || DataContext is not DocumentListViewModel viewModel)
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
            ApplyColumnVisibility,
            viewModel.GetColumnContentAlignment,
            viewModel.SaveColumnContentAlignmentAsync,
            ApplyColumnAlignments,
            applyFrozenColumnCount: count => DocumentsGrid.FrozenColumnCount = count);

        _detailColumnManager = new DataGridColumnManager(
            viewModel.DetailColumnDefinitions,
            viewModel.GetDetailGridLayoutAsync,
            viewModel.SaveDetailGridLayoutAsync,
            viewModel.GetDetailColumnVisibility,
            viewModel.GetDetailColumnDisplayIndex,
            viewModel.GetDetailColumnWidth,
            viewModel.ToggleDetailColumnVisibilityAsync,
            viewModel.SaveDetailColumnDisplayIndexAsync,
            viewModel.SaveDetailColumnWidthAsync,
            ApplyDetailColumnVisibility,
            applyFrozenColumnCount: count => DetailRowsGrid.FrozenColumnCount = count);

        await _columnManager.InitializeAsync(_gridColumns);
        await _detailColumnManager.InitializeAsync(_detailGridColumns);
        _contextMenuController = _sharedUiSupport.CreateContextMenuController(DocumentsGrid, "Documenti");
        _detailContextMenuController = new SharedGridContextMenuController(new SharedGridContextMenuOptions
        {
            Grid = DetailRowsGrid,
            GridKey = "DocumentiDettaglio",
            ColumnManager = _detailColumnManager,
            IncludeAppearanceMenuOnHeader = false,
            IncludeAppearanceMenuOnBody = false,
            Actions = []
        });
        ApplyColumnVisibility();
        ApplyColumnAlignments();
        ApplyDetailColumnVisibility();
        RefreshFooterRow();
        SyncFooterColumnsFromGrid();
        InitializeGridViewportSync();
        ApplySplitterProportions(viewModel);
        viewModel.ResetUIDocumentPresentationMode();
        AttachViewModel(viewModel);
        AttachHostWindow();
        SyncPeriodFilterSelection();
        UpdateSelectionActionButtonsState();
        _columnsInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _contextMenuController?.Dispose();
        _contextMenuController = null;
        _detailContextMenuController?.Dispose();
        _detailContextMenuController = null;
        DetachGridViewportSync();
        DetachViewModel();
        DetachHostWindow();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.OldValue, e.NewValue))
        {
            return;
        }

        DetachViewModel();

        if (e.NewValue is DocumentListViewModel viewModel && IsLoaded)
        {
            viewModel.ResetUIDocumentPresentationMode();
            AttachViewModel(viewModel);
            AttachHostWindow();
            ApplyColumnVisibility();
            ApplyColumnAlignments();
            ApplyDetailColumnVisibility();
            RefreshFooterRow();
            SyncFooterColumnsFromGrid();
            SyncPeriodFilterSelection();
            UpdateSelectionActionButtonsState();
        }
    }

    private void AttachHostWindow()
    {
        var hostWindow = Window.GetWindow(this);
        if (ReferenceEquals(_hostWindow, hostWindow))
        {
            return;
        }

        DetachHostWindow();
        _hostWindow = hostWindow;
        if (_hostWindow is not null)
        {
            _hostWindow.PreviewKeyDown += HostWindow_OnPreviewKeyDown;
        }
    }

    private void DetachHostWindow()
    {
        if (_hostWindow is null)
        {
            return;
        }

        _hostWindow.PreviewKeyDown -= HostWindow_OnPreviewKeyDown;
        _hostWindow = null;
    }

    private void HostWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible || e.Handled)
        {
            return;
        }

        HandleDocumentShortcutKey(e);
    }

    private void AttachViewModel(DocumentListViewModel viewModel)
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
        _sharedUiSupport.HandleViewModelPropertyChanged(e.PropertyName);

        if (e.PropertyName is nameof(DocumentListViewModel.IsFilterOggiActive)
            or nameof(DocumentListViewModel.IsFilterSettimanaActive)
            or nameof(DocumentListViewModel.IsFilterMeseCorrenteActive)
            or nameof(DocumentListViewModel.OggiButtonLabel)
            or nameof(DocumentListViewModel.SettimanaButtonLabel)
            or nameof(DocumentListViewModel.MeseCorrenteButtonLabel)
            or nameof(DocumentListViewModel.DataDa)
            or nameof(DocumentListViewModel.DataA))
        {
            Dispatcher.Invoke(SyncPeriodFilterSelection);
        }

        if (e.PropertyName is nameof(DocumentListViewModel.UIDocumentPresentationMode)
            or nameof(DocumentListViewModel.IsUICortesiaColumnVisible)
            or nameof(DocumentListViewModel.IsUICartaSummaryVisible)
            or nameof(DocumentListViewModel.IsUICortesiaSummaryVisible))
        {
            Dispatcher.Invoke(() =>
            {
                ApplyColumnVisibility();
                ApplyColumnAlignments();
                RefreshFooterRow();
                SyncFooterColumnsFromGrid();
            });
        }

        if (e.PropertyName is nameof(DocumentListViewModel.IsDetailOrdineRigaColumnVisible)
            or nameof(DocumentListViewModel.IsDetailCodiceColumnVisible)
            or nameof(DocumentListViewModel.IsDetailDescrizioneColumnVisible)
            or nameof(DocumentListViewModel.IsDetailQuantitaColumnVisible)
            or nameof(DocumentListViewModel.IsDetailPrezzoColumnVisible)
            or nameof(DocumentListViewModel.IsDetailScontoColumnVisible)
            or nameof(DocumentListViewModel.IsDetailImportoColumnVisible))
        {
            Dispatcher.Invoke(ApplyDetailColumnVisibility);
        }

        if (e.PropertyName is nameof(DocumentListViewModel.CanDeleteSelectedLocalDocuments)
            or nameof(DocumentListViewModel.SelectedDocument)
            or nameof(DocumentListViewModel.SelectedDocumentsCount))
        {
            Dispatcher.Invoke(UpdateSelectionActionButtonsState);
        }
    }

    private void SyncPeriodFilterSelection()
    {
        if (PeriodFilterPicker is null || DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        var selectedLabel = "Personalizzato";
        if (viewModel.IsFilterOggiActive)
        {
            selectedLabel = "Oggi";
        }
        else if (viewModel.IsFilterSettimanaActive)
        {
            selectedLabel = "Settimana";
        }
        else if (viewModel.IsFilterMeseCorrenteActive)
        {
            selectedLabel = "Mese";
        }

        _isSyncingPeriodFilterSelection = true;
        try
        {
            PeriodFilterPicker.DisplayText = selectedLabel;
            PeriodFilterPicker.SelectedItem = _periodFilterOptions.FirstOrDefault(option =>
                string.Equals(option.Label, selectedLabel, StringComparison.Ordinal));
        }
        finally
        {
            _isSyncingPeriodFilterSelection = false;
        }
    }

    private void InitializeGridViewportSync()
    {
        DetachGridViewportSync();
        _documentsGridScrollViewer = DocumentListSharedUiSupport.FindDescendant<ScrollViewer>(DocumentsGrid);
        _footerGridScrollViewer = DocumentListSharedUiSupport.FindDescendant<ScrollViewer>(FooterGrid);

        if (_documentsGridScrollViewer is null || _footerGridScrollViewer is null)
        {
            return;
        }

        _documentsGridScrollViewer.ScrollChanged += DocumentsGridScrollViewer_OnScrollChanged;
        SyncFooterColumnsFromGrid();
    }

    private void DetachGridViewportSync()
    {
        if (_documentsGridScrollViewer is not null)
        {
            _documentsGridScrollViewer.ScrollChanged -= DocumentsGridScrollViewer_OnScrollChanged;
        }

        _documentsGridScrollViewer = null;
        _footerGridScrollViewer = null;
    }

    private void DocumentsGridScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange == 0 && e.ViewportWidthChange == 0)
        {
            return;
        }

        SyncFooterColumnsFromGrid();
    }

    private void ApplySplitterProportions(DocumentListViewModel viewModel)
    {
        if (viewModel.ListPanelWidth > 0)
        {
            ListColumnDef.Width = new GridLength(viewModel.ListPanelWidth, GridUnitType.Star);
        }

        if (viewModel.DetailPanelWidth > 0 && viewModel.IsDetailExpanded)
        {
            DetailColumnDef.Width = new GridLength(viewModel.DetailPanelWidth, GridUnitType.Star);
        }
    }

    private void ApplyColumnVisibility()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        ApplyColumnVisibility("Status", viewModel.IsStatusColumnVisible);
        ApplyColumnVisibility("Origine", viewModel.IsOrigineColumnVisible);
        ApplyColumnVisibility("Oid", viewModel.IsOidColumnVisible);
        ApplyColumnVisibility("Documento", viewModel.IsDocumentoColumnVisible);
        ApplyColumnVisibility("Data", viewModel.IsDataColumnVisible);
        ApplyColumnVisibility("Operatore", viewModel.IsOperatoreColumnVisible);
        ApplyColumnVisibility("Cliente", viewModel.IsClienteColumnVisible);
        ApplyColumnVisibility("Totale", viewModel.IsTotaleColumnVisible);
        ApplyColumnVisibility("Punti", viewModel.IsPuntiColumnVisible);
        ApplyColumnVisibility("PagContanti", viewModel.IsPagContantiColumnVisible);
        ApplyColumnVisibility("PagCarta", viewModel.IsPagCartaColumnVisible);
        ApplyColumnVisibility("Cortesia", viewModel.IsUICortesiaColumnVisible);
        ApplyColumnVisibility("PagWeb", viewModel.IsPagWebColumnVisible);
        ApplyColumnVisibility("PagBuoni", viewModel.IsPagBuoniColumnVisible);
        ApplyColumnVisibility("PagSospeso", viewModel.IsPagSospesoColumnVisible);
        ApplyColumnVisibility("ResiduoPagamento", viewModel.IsResiduoColumnVisible);
        ApplyColumnVisibility("DaFiscalizzare", viewModel.IsDaFiscalizzareColumnVisible);
        ApplyColumnVisibility("StatoDocumento", viewModel.IsStatoColumnVisible);
        ApplyColumnVisibility("Scontrino", viewModel.IsScontrinoColumnVisible);
        ApplyColumnVisibility("Actions", true);
    }

    private void ApplyDetailColumnVisibility()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        ApplyDetailColumnVisibility("OrdineRiga", viewModel.IsDetailOrdineRigaColumnVisible);
        ApplyDetailColumnVisibility("CodiceArticolo", viewModel.IsDetailCodiceColumnVisible);
        ApplyDetailColumnVisibility("Descrizione", viewModel.IsDetailDescrizioneColumnVisible);
        ApplyDetailColumnVisibility("Quantita", viewModel.IsDetailQuantitaColumnVisible);
        ApplyDetailColumnVisibility("PrezzoUnitario", viewModel.IsDetailPrezzoColumnVisible);
        ApplyDetailColumnVisibility("ScontoPercentuale", viewModel.IsDetailScontoColumnVisible);
        ApplyDetailColumnVisibility("ImportoRiga", viewModel.IsDetailImportoColumnVisible);
    }

    private void ApplyColumnVisibility(string key, bool isVisible)
    {
        if (_gridColumns.TryGetValue(key, out var gridColumn))
        {
            gridColumn.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_footerColumns.TryGetValue(key, out var footerColumn))
        {
            footerColumn.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplyDetailColumnVisibility(string key, bool isVisible)
    {
        if (_detailGridColumns.TryGetValue(key, out var gridColumn))
        {
            gridColumn.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplyColumnAlignments()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        foreach (var pair in _gridColumns)
        {
            if (string.Equals(pair.Key, "Actions", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var alignment = viewModel.GetColumnContentAlignment(pair.Key);
            ApplyColumnAlignment(pair.Value, alignment);
            if (_footerColumns.TryGetValue(pair.Key, out var footerColumn))
            {
                ApplyColumnAlignment(footerColumn, alignment, applyHeaderAlignment: false);
            }
        }
    }

    private void ApplyColumnAlignment(DataGridColumn column, GridColumnContentAlignment alignment, bool applyHeaderAlignment = true)
    {
        if (applyHeaderAlignment)
        {
            column.HeaderStyle = alignment switch
            {
                GridColumnContentAlignment.Left => (Style?)FindResource("UIDocumentHeaderLeftStyle"),
                GridColumnContentAlignment.Right => (Style?)FindResource("UIDocumentHeaderRightStyle"),
                _ => (Style?)FindResource("UIDocumentHeaderCenterStyle")
            };

            _contextMenuController?.EnsureHeaderContextMenuForColumn(column);
        }

        if (column is DataGridTextColumn textColumn)
        {
            textColumn.ElementStyle = alignment switch
            {
                GridColumnContentAlignment.Left => (Style?)FindResource("UIDocumentCellTextLeftStyle"),
                GridColumnContentAlignment.Right => (Style?)FindResource("UIDocumentCellTextRightStyle"),
                _ => (Style?)FindResource("UIDocumentCellTextCenterStyle")
            };
        }
    }

    private void RefreshFooterRow()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        FooterGrid.ItemsSource = viewModel.UIFooterRows;
        FooterGrid.Items.Refresh();
    }

    private void SyncFooterColumnsFromGrid()
    {
        DocumentListSharedUiSupport.SyncFooterColumnsFromGrid(_gridColumns, _footerColumns, _documentsGridScrollViewer, _footerGridScrollViewer);
    }

    private void DocumentsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DocumentListViewModel viewModel ||
            viewModel.SelectedDocument is null ||
            !viewModel.OpenDocumentInBancoCommand.CanExecute(null))
        {
            return;
        }

        viewModel.OpenDocumentInBancoCommand.Execute(null);
    }

    private void DocumentsGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateSelectedDocuments(DocumentsGrid.SelectedItems.Cast<DocumentGridRowViewModel>());
        UpdateSelectionActionButtonsState();
    }

    private void DocumentsGrid_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SyncFooterColumnsFromGrid();
        RefreshFooterRow();
    }

    private void Root_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        HandleDocumentShortcutKey(e);
        if (e.Handled)
        {
            return;
        }

        if (IsStandardTextCommandContext())
        {
            _sharedUiSupport.HandleRootPreviewKeyDown(e, TotalsPanel);
            return;
        }

        _sharedUiSupport.HandleRootPreviewKeyDown(e, TotalsPanel);
    }

    private void HandleDocumentShortcutKey(KeyEventArgs e)
    {
        var effectiveKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && effectiveKey == Key.F9)
        {
            viewModel.CycleUIDocumentPresentationMode();
            ApplyColumnVisibility();
            RefreshFooterRow();
            SyncFooterColumnsFromGrid();
            SyncGridSelectionToViewModel();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && effectiveKey == Key.Escape)
        {
            viewModel.ResetUIDocumentPresentationMode();
            ApplyColumnVisibility();
            RefreshFooterRow();
            SyncFooterColumnsFromGrid();
            SyncGridSelectionToViewModel();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && effectiveKey == Key.F2 &&
            viewModel.OpenDocumentInBancoCommand.CanExecute(null))
        {
            viewModel.OpenDocumentInBancoCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static bool IsStandardTextCommandContext()
    {
        var focusedElement = Keyboard.FocusedElement as DependencyObject;
        if (focusedElement is null)
        {
            return false;
        }

        if (focusedElement is TextBoxBase or PasswordBox or ComboBox)
        {
            return true;
        }

        return FindAncestor<Selector>(focusedElement) is ComboBox;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private async void NewBancoDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _sharedUiSupport.HandleNewBancoDocumentAsync();
    }

    private void PeriodFilterPicker_OnSelectedItemChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPeriodFilterSelection || DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        var selectedLabel = (PeriodFilterPicker.SelectedItem as GestionaleLookupOption)?.Label;
        switch (selectedLabel)
        {
            case "Personalizzato":
                viewModel.UseCustomDateRangeFilter();
                break;
            case "Oggi":
                if (viewModel.FilterOggiCommand.CanExecute(null))
                {
                    viewModel.FilterOggiCommand.Execute(null);
                }

                break;
            case "Settimana":
                if (viewModel.FilterSettimanaCommand.CanExecute(null))
                {
                    viewModel.FilterSettimanaCommand.Execute(null);
                }

                break;
            case "Mese":
                if (viewModel.FilterMeseCorrenteCommand.CanExecute(null))
                {
                    viewModel.FilterMeseCorrenteCommand.Execute(null);
                }

                break;
            default:
                break;
        }

        SyncPeriodFilterSelection();
    }

    private void DatePicker_OnDateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
    {
        _sharedUiSupport.HandleDateValidationError(sender, e);
    }

    private async void DeleteSelectedLocalDocumentsButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _sharedUiSupport.HandleDeleteSelectedAsync(DocumentsGrid);
    }

    private void OpenDocumentInBancoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DocumentListViewModel viewModel ||
            !viewModel.OpenDocumentInBancoCommand.CanExecute(null))
        {
            return;
        }

        viewModel.OpenDocumentInBancoCommand.Execute(null);
    }

    private Task DeleteSelectedLocalDocumentsCoreAsync()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return Task.CompletedTask;
        }

        return viewModel.DeleteSelectedLocalDocumentsAsync();
    }

    private async void DeleteRowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: DocumentGridRowViewModel row } ||
            DataContext is not DocumentListViewModel)
        {
            return;
        }

        await _sharedUiSupport.HandleDeleteRowAsync(row, DocumentsGrid);
    }

    private Task DeleteRowCoreAsync(DocumentGridRowViewModel row)
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return Task.CompletedTask;
        }

        return viewModel.DeleteDocumentRowAsync(row);
    }

    private void SyncGridSelectionToViewModel()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        DocumentListSharedUiSupport.SyncGridSelectionToViewModel(DocumentsGrid, viewModel);
        UpdateSelectionActionButtonsState();
    }

    private void UpdateSelectionActionButtonsState()
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            if (DeleteSelectedButton is not null)
            {
                DeleteSelectedButton.IsEnabled = false;
            }

            if (OpenInBancoButton is not null)
            {
                OpenInBancoButton.IsEnabled = false;
            }

            return;
        }

        if (DeleteSelectedButton is not null)
        {
            DeleteSelectedButton.IsEnabled = DocumentsGrid.SelectedItems.Count > 0 && viewModel.CanDeleteSelectedLocalDocuments;
        }

        if (OpenInBancoButton is not null)
        {
            OpenInBancoButton.IsEnabled = viewModel.SelectedDocument is not null &&
                                          DocumentsGrid.SelectedItems.Count > 0 &&
                                          viewModel.OpenDocumentInBancoCommand.CanExecute(null);
        }
    }

    private async void Splitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is not DocumentListViewModel viewModel)
        {
            return;
        }

        var result = MessageBox.Show(
            "Salvare il layout corrente delle proporzioni pannello?",
            "Conferma salvataggio layout",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        viewModel.ListPanelWidth = ListColumnDef.Width.Value;
        viewModel.DetailPanelWidth = DetailColumnDef.Width.Value;
        await viewModel.SaveSplitterProportionsAsync();
    }
}
