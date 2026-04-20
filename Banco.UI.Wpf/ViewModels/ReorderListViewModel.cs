using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using Banco.Riordino;
using Banco.UI.Wpf.Infrastructure.GridColumns;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using Banco.Vendita.Documents;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ReorderListViewModel : ViewModelBase
{
    private readonly IReorderListRepository _repository;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosProcessLogService _logService;
    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IGestionaleSupplierOrderWriteService _supplierOrderWriteService;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private readonly SemaphoreSlim _supplierDraftSemaphore = new(1, 1);
    private readonly Dictionary<string, GridColumnLayoutState> _columns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICollectionView _itemsView;
    private CancellationTokenSource? _articleLookupCts;
    private string _statusMessage = "Caricamento lista riordino...";
    private string _listTitle = "Lista riordino";
    private string _listState = "Aperta";
    private string _searchArticleText = string.Empty;
    private Guid _currentListId;
    private ReorderGridRowViewModel? _selectedItem;
    private ReorderSupplierDraftViewModel? _selectedSupplierDraft;
    private GestionaleArticleSearchResult? _selectedArticleLookup;
    private ReorderListFilter _selectedFilter = ReorderListFilter.OpenOnly;
    private readonly List<ReorderGridRowViewModel> _selectedRows = [];
    private int _articleLookupRequestVersion;
    private bool _isArticlePopupOpen;
    private bool _isUpdatingArticleLookupText;
    private bool _isGroupedBySupplier = true;
    private bool _isLoading;
    private bool _groupingRefreshPending;

    public ReorderListViewModel(
        IReorderListRepository repository,
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService,
        IGestionaleDocumentReadService documentReadService,
        IGestionaleArticleReadService articleReadService,
        IGestionaleSupplierOrderWriteService supplierOrderWriteService)
    {
        _repository = repository;
        _configurationService = configurationService;
        _logService = logService;
        _documentReadService = documentReadService;
        _articleReadService = articleReadService;
        _supplierOrderWriteService = supplierOrderWriteService;

        Items = [];
        ArticleLookupResults = [];
        _itemsView = CollectionViewSource.GetDefaultView(Items);
        _itemsView.Filter = FilterItems;
        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        MarkOrderedCommand = new RelayCommand(async () => await SetOrderedAsync(true), () => CanConfirmSelectedRows);
        MarkPendingCommand = new RelayCommand(async () => await SetOrderedAsync(false), () => CanReopenSelectedRows);
        RemoveCommand = new RelayCommand(async () => await RemoveSelectedAsync(), () => CanRemoveSelectedRows);
        AddManualArticleCommand = new RelayCommand(async () => await AddManualArticleAsync(), () => CanAddManualArticle);
        ShowOpenOnlyCommand = new RelayCommand(() => SetFilter(ReorderListFilter.OpenOnly), () => !IsLoading && SelectedFilter != ReorderListFilter.OpenOnly);
        ShowAllCommand = new RelayCommand(() => SetFilter(ReorderListFilter.All), () => !IsLoading && SelectedFilter != ReorderListFilter.All);
        ShowOrderedOnlyCommand = new RelayCommand(() => SetFilter(ReorderListFilter.OrderedOnly), () => !IsLoading && SelectedFilter != ReorderListFilter.OrderedOnly);
        ToggleSupplierGroupingCommand = new RelayCommand(ToggleSupplierGrouping, () => !IsLoading);
        SelectSupplierDraftCommand = new RelayCommand<ReorderSupplierDraftViewModel?>(SelectSupplierDraft);
        MarkSupplierOrderDoneCommand = new RelayCommand(async () => await SetSupplierDraftStatusAsync(ReorderSupplierDraftStatus.Ordinata), () => CanMarkSupplierOrderDone);
        MarkSupplierOrderCreatedOnFmCommand = new RelayCommand(async () => await CreateSupplierOrderOnFmAsync(), () => CanMarkSupplierOrderCreatedOnFm);
        CloseSupplierOrderCommand = new RelayCommand(async () => await SetSupplierDraftStatusAsync(ReorderSupplierDraftStatus.Chiusa), () => CanCloseSupplierOrder);
        RemoveSupplierDraftCommand = new RelayCommand(async () => await RemoveSelectedSupplierDraftAsync(), () => CanRemoveSupplierDraft);

        _repository.CurrentListChanged += OnCurrentListChanged;
        ApplyGrouping();
        _ = RefreshAsync();
    }

    public ObservableCollection<ReorderGridRowViewModel> Items { get; }

    public ObservableCollection<GestionaleArticleSearchResult> ArticleLookupResults { get; }

    public ObservableCollection<ReorderSupplierDraftViewModel> SupplierDrafts { get; } = [];

    public RelayCommand RefreshCommand { get; }

    public RelayCommand MarkOrderedCommand { get; }

    public RelayCommand MarkPendingCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand AddManualArticleCommand { get; }

    public RelayCommand ShowOpenOnlyCommand { get; }

    public RelayCommand ShowAllCommand { get; }

    public RelayCommand ShowOrderedOnlyCommand { get; }

    public RelayCommand ToggleSupplierGroupingCommand { get; }

    public RelayCommand<ReorderSupplierDraftViewModel?> SelectSupplierDraftCommand { get; }

    public RelayCommand MarkSupplierOrderDoneCommand { get; }

    public RelayCommand MarkSupplierOrderCreatedOnFmCommand { get; }

    public RelayCommand CloseSupplierOrderCommand { get; }

    public RelayCommand RemoveSupplierDraftCommand { get; }

    public IReadOnlyList<GridColumnDefinition> ColumnDefinitions => ReorderColumnDefinitions.All;

    public ICollectionView ItemsView => _itemsView;

    public string Titolo => "Lista riordino";

    public string SearchArticleText
    {
        get => _searchArticleText;
        set
        {
            if (!SetProperty(ref _searchArticleText, value))
            {
                return;
            }

            if (_isUpdatingArticleLookupText)
            {
                return;
            }

            if (_selectedArticleLookup is not null &&
                !_selectedArticleLookup.DisplayLabel.Equals(value ?? string.Empty, StringComparison.Ordinal))
            {
                SelectedArticleLookup = null;
            }

            ScheduleArticleLookup();
        }
    }

    public string ListTitle
    {
        get => _listTitle;
        private set => SetProperty(ref _listTitle, value);
    }

    public string ListState
    {
        get => _listState;
        private set => SetProperty(ref _listState, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public GestionaleArticleSearchResult? SelectedArticleLookup
    {
        get => _selectedArticleLookup;
        private set
        {
            if (!SetProperty(ref _selectedArticleLookup, value))
            {
                return;
            }

            NotifyPropertyChanged(nameof(CanAddManualArticle));
            AddManualArticleCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsArticlePopupOpen
    {
        get => _isArticlePopupOpen;
        set => SetProperty(ref _isArticlePopupOpen, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                MarkOrderedCommand.RaiseCanExecuteChanged();
                MarkPendingCommand.RaiseCanExecuteChanged();
                RemoveCommand.RaiseCanExecuteChanged();
                AddManualArticleCommand.RaiseCanExecuteChanged();
                RaiseFilterCommandStateChanged();
                ToggleSupplierGroupingCommand.RaiseCanExecuteChanged();
                NotifySelectionStateChanged();
                NotifyPropertyChanged(nameof(CanAddManualArticle));
            }
        }
    }

    public ReorderGridRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                NotifySelectionStateChanged();
            }
        }
    }

    public int SelectedRowsCount => _selectedRows.Count;

    public bool HasSelectedRows => SelectedRowsCount > 0;

    public bool CanAddManualArticle => !IsLoading && SelectedArticleLookup is not null;

    public bool CanEditSelectedRow => !IsLoading && SelectedRowsCount == 1;

    public bool CanRemoveSelectedRows => !IsLoading && GetActionTargetRows().Count > 0;

    public bool CanConfirmSelectedRows => !IsLoading && GetActionTargetRows().Any(row => !row.IsOrdered);

    public bool CanReopenSelectedRows => !IsLoading && GetActionTargetRows().Any(row => row.IsOrdered);

    public ReorderListFilter SelectedFilter
    {
        get => _selectedFilter;
        private set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                _itemsView.Refresh();
                if (SelectedItem is not null && !_itemsView.Cast<object>().Contains(SelectedItem))
                {
                    SelectedItem = null;
                }

                NotifySummary();
                RaiseFilterCommandStateChanged();
                NotifyPropertyChanged(nameof(SelectedFilterLabel));
                NotifyPropertyChanged(nameof(IsOpenOnlyFilterSelected));
                NotifyPropertyChanged(nameof(IsAllFilterSelected));
                NotifyPropertyChanged(nameof(IsOrderedOnlyFilterSelected));
            }
        }
    }

    public string SelectedFilterLabel => SelectedFilter switch
    {
        ReorderListFilter.OpenOnly => "Da confermare",
        ReorderListFilter.OrderedOnly => "Confermate",
        _ => "Tutte"
    };

    public bool IsOpenOnlyFilterSelected => SelectedFilter == ReorderListFilter.OpenOnly;

    public bool IsAllFilterSelected => SelectedFilter == ReorderListFilter.All;

    public bool IsOrderedOnlyFilterSelected => SelectedFilter == ReorderListFilter.OrderedOnly;

    public bool IsGroupedBySupplier
    {
        get => _isGroupedBySupplier;
        private set
        {
            if (SetProperty(ref _isGroupedBySupplier, value))
            {
                ApplyGrouping();
                NotifySummary();
                NotifyPropertyChanged(nameof(GroupingButtonLabel));
            }
        }
    }

    public string GroupingButtonLabel => IsGroupedBySupplier ? "Vista piatta" : "Raggruppa per fornitore";

    public int OpenCount => _itemsView.Cast<ReorderGridRowViewModel>().Count(item => !item.IsOrdered);

    public int OrderedCount => _itemsView.Cast<ReorderGridRowViewModel>().Count(item => item.IsOrdered);

    public int VisibleCount => _itemsView.Cast<object>().Count();

    public int SupplierGroupCount => _itemsView.Groups?.Count ?? 0;

    public int SupplierDraftCount => SupplierDrafts.Count;

    public bool HasSupplierDrafts => SupplierDrafts.Count > 0;

    public ReorderSupplierDraftViewModel? SelectedSupplierDraft
    {
        get => _selectedSupplierDraft;
        private set
        {
            if (SetProperty(ref _selectedSupplierDraft, value))
            {
                NotifyPropertyChanged(nameof(HasSelectedSupplierDraft));
                NotifyPropertyChanged(nameof(SupplierDraftDetailTitle));
                NotifyPropertyChanged(nameof(SupplierDraftDetailStatus));
                MarkSupplierOrderDoneCommand.RaiseCanExecuteChanged();
                MarkSupplierOrderCreatedOnFmCommand.RaiseCanExecuteChanged();
                CloseSupplierOrderCommand.RaiseCanExecuteChanged();
                RemoveSupplierDraftCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedSupplierDraft => SelectedSupplierDraft is not null;

    public string SupplierDraftDetailTitle => SelectedSupplierDraft is null
        ? "Dettaglio lista fornitore"
        : $"{SelectedSupplierDraft.SupplierName} {SelectedSupplierDraft.CounterLabel}";

    public string SupplierDraftDetailStatus => SelectedSupplierDraft?.StatusLabel ?? "Nessuna lista selezionata";

    public bool CanMarkSupplierOrderDone => SelectedSupplierDraft is not null &&
                                            SelectedSupplierDraft.Status == ReorderSupplierDraftStatus.Aperta;

    public bool CanMarkSupplierOrderCreatedOnFm => SelectedSupplierDraft is not null &&
                                                   SelectedSupplierDraft.Status == ReorderSupplierDraftStatus.Ordinata;

    public bool CanCloseSupplierOrder => SelectedSupplierDraft is not null &&
                                         SelectedSupplierDraft.Status == ReorderSupplierDraftStatus.RegistrataSuFm;

    public bool CanRemoveSupplierDraft => SelectedSupplierDraft is not null &&
                                          SelectedSupplierDraft.Status != ReorderSupplierDraftStatus.RegistrataSuFm &&
                                          SelectedSupplierDraft.Status != ReorderSupplierDraftStatus.Chiusa;

    public string SummaryLabel => VisibleCount == 0
        ? "Nessun articolo ancora presente nella lista."
        : IsGroupedBySupplier
            ? $"{VisibleCount} righe visibili, {OpenCount} da confermare, {OrderedCount} confermate, {SupplierGroupCount} fornitori, {SupplierDraftCount} liste fornitore."
            : $"{VisibleCount} righe visibili, {OpenCount} da confermare, {OrderedCount} confermate, {SupplierDraftCount} liste fornitore.";

    public bool IsCodiceColumnVisible => GetColumnVisibility("Codice");
    public bool IsOrdinatoColumnVisible => GetColumnVisibility("Ordinato");
    public bool IsDescrizioneColumnVisible => GetColumnVisibility("Descrizione");
    public bool IsQuantitaColumnVisible => GetColumnVisibility("Quantita");
    public bool IsQuantitaDaOrdinareColumnVisible => GetColumnVisibility("QuantitaDaOrdinare");
    public bool IsUnitaMisuraColumnVisible => GetColumnVisibility("UnitaMisura");
    public bool IsFornitoreSuggeritoColumnVisible => GetColumnVisibility("FornitoreSuggerito");
    public bool IsFornitoreSelezionatoColumnVisible => GetColumnVisibility("FornitoreSelezionato");
    public bool IsPrezzoColumnVisible => GetColumnVisibility("Prezzo");
    public bool IsMotivoColumnVisible => GetColumnVisibility("Motivo");
    public bool IsOperatoreColumnVisible => GetColumnVisibility("Operatore");
    public bool IsDataColumnVisible => GetColumnVisibility("Data");
    public bool IsNoteColumnVisible => GetColumnVisibility("Note");

    public async Task RefreshAsync()
    {
        await _refreshSemaphore.WaitAsync();
        try
        {
            IsLoading = true;
            StatusMessage = "Aggiorno la lista riordino...";

            var snapshot = await _repository.GetCurrentListAsync();
            _currentListId = snapshot.List.Id;

            foreach (var existingItem in Items)
            {
                existingItem.PropertyChanged -= Row_OnPropertyChanged;
            }

            Items.Clear();
            foreach (var item in snapshot.Items.Select(ReorderGridRowViewModel.FromModel))
            {
                item.PropertyChanged += Row_OnPropertyChanged;
                Items.Add(item);
            }

            await LoadSupplierOptionsAsync();
            await RebuildSupplierDraftsAsync();

            ListTitle = snapshot.List.Titolo;
            ListState = snapshot.List.Stato == ReorderListStatus.Ordinata ? "Confermata" : "Aperta";
            StatusMessage = Items.Count == 0
                ? "Lista riordino pronta. Nessuna riga presente."
                : $"Lista riordino aggiornata: {Items.Count} righe.";

            ApplyGrouping();
            _itemsView.Refresh();
            NotifySummary();
            _logService.Info(nameof(ReorderListViewModel), $"Refresh lista riordino completato. Righe={Items.Count}, StatoLista={ListState}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore caricamento lista riordino: {ex.Message}";
            _logService.Error(nameof(ReorderListViewModel), "Errore durante il refresh della lista riordino.", ex);
        }
        finally
        {
            IsLoading = false;
            _refreshSemaphore.Release();
        }
    }

    public async Task<GridLayoutSettings> GetGridLayoutAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = GridLayoutMigration.GetOrCreateReorderListLayout(settings, ColumnDefinitions);
        SyncColumnsFromLayout(layout);
        return layout;
    }

    public async Task SaveGridLayoutAsync(GridLayoutSettings layout)
    {
        var settings = await _configurationService.LoadAsync();
        settings.GridLayouts[GridLayoutMigration.ReorderListGridId] = layout;
        await _configurationService.SaveAsync(settings);
    }

    public double GetColumnWidth(string key) => _columns.TryGetValue(key, out var state) ? state.Width : 120;

    public int GetColumnDisplayIndex(string key) => _columns.TryGetValue(key, out var state) ? state.DisplayIndex : 0;

    public bool GetColumnVisibility(string key) => _columns.TryGetValue(key, out var state) ? state.IsVisible : true;

    public async Task ToggleColumnVisibilityAsync(string key)
    {
        var layout = await GetGridLayoutAsync();
        if (!layout.Columns.TryGetValue(key, out var state))
        {
            return;
        }

        state.IsVisible = !state.IsVisible;
        _columns[key] = state;
        RaiseColumnVisibilityNotifications();
        await SaveGridLayoutAsync(layout);
    }

    public async Task SaveColumnWidthAsync(string key, double width)
    {
        if (width <= 0)
        {
            return;
        }

        var layout = await GetGridLayoutAsync();
        if (!layout.Columns.TryGetValue(key, out var state))
        {
            return;
        }

        state.Width = width;
        _columns[key] = state;
        await SaveGridLayoutAsync(layout);
    }

    public async Task SaveColumnDisplayIndexAsync(string key, int displayIndex)
    {
        var layout = await GetGridLayoutAsync();
        if (!layout.Columns.TryGetValue(key, out var state))
        {
            return;
        }

        state.DisplayIndex = displayIndex;
        _columns[key] = state;
        await SaveGridLayoutAsync(layout);
    }

    public async Task UpdateSelectedSupplierAsync(
        ReorderGridRowViewModel row,
        ReorderSupplierOptionViewModel? supplier)
    {
        if (row is null || supplier is null)
        {
            return;
        }

        await _repository.UpdateSelectedSupplierAsync(row.Id, supplier.Oid, supplier.Nome);
        row.SelectedSupplier = supplier;
        row.FornitoreSelezionato = supplier.Nome;
        if (supplier.PrezzoRiferimento > 0)
        {
            row.PrezzoSuggerito = supplier.PrezzoRiferimento;
        }

        ApplyGrouping();
        await RebuildSupplierDraftsAsync();
        NotifySummary();
    }

    public async Task UpdateQuantityToOrderAsync(ReorderGridRowViewModel row, decimal quantityToOrder)
    {
        ArgumentNullException.ThrowIfNull(row);

        var normalizedQuantity = quantityToOrder <= 0 ? 1 : quantityToOrder;
        await _repository.UpdateQuantityToOrderAsync(row.Id, normalizedQuantity);
        row.QuantitaDaOrdinare = normalizedQuantity;
        StatusMessage = $"Quantita` da ordinare aggiornata per {row.CodiceArticolo}.";
        await RebuildSupplierDraftsAsync();
        NotifySummary();
    }

    public void SetSelectedRows(IEnumerable<ReorderGridRowViewModel> rows)
    {
        _selectedRows.Clear();
        _selectedRows.AddRange(rows.Where(row => row is not null).Distinct());

        if ((_selectedItem is null || !_selectedRows.Contains(_selectedItem)) && _selectedRows.Count > 0)
        {
            _selectedItem = _selectedRows[0];
            NotifyPropertyChanged(nameof(SelectedItem));
        }

        NotifySelectionStateChanged();
    }

    public void SelectArticleFromLookup(GestionaleArticleSearchResult? articolo)
    {
        if (articolo is null)
        {
            return;
        }

        SelectedArticleLookup = articolo;
        _isUpdatingArticleLookupText = true;
        SearchArticleText = articolo.DisplayLabel;
        _isUpdatingArticleLookupText = false;
        IsArticlePopupOpen = false;
    }

    private async Task SetOrderedAsync(bool isOrdered)
    {
        var targetRows = GetActionTargetRows();
        if (targetRows.Count == 0)
        {
            return;
        }

        foreach (var row in targetRows)
        {
            await _repository.SetItemOrderedAsync(row.Id, isOrdered);
        }

        await RefreshAsync();
    }

    private async Task RemoveSelectedAsync()
    {
        var targetRows = GetActionTargetRows();
        if (targetRows.Count == 0)
        {
            return;
        }

        foreach (var row in targetRows)
        {
            await _repository.RemoveItemAsync(row.Id);
        }

        await RefreshAsync();
    }

    private List<ReorderGridRowViewModel> GetActionTargetRows()
    {
        if (_selectedRows.Count > 0)
        {
            return _selectedRows.ToList();
        }

        return SelectedItem is null ? [] : [SelectedItem];
    }

    private void Row_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ReorderGridRowViewModel.IsActionSelected))
        {
            return;
        }

        NotifySelectionStateChanged();
    }

    private void NotifySummary()
    {
        NotifyPropertyChanged(nameof(OpenCount));
        NotifyPropertyChanged(nameof(OrderedCount));
        NotifyPropertyChanged(nameof(VisibleCount));
        NotifyPropertyChanged(nameof(SupplierGroupCount));
        NotifyPropertyChanged(nameof(SupplierDraftCount));
        NotifyPropertyChanged(nameof(HasSupplierDrafts));
        NotifyPropertyChanged(nameof(HasSelectedSupplierDraft));
        NotifyPropertyChanged(nameof(SupplierDraftDetailTitle));
        NotifyPropertyChanged(nameof(SupplierDraftDetailStatus));
        NotifyPropertyChanged(nameof(SummaryLabel));
    }

    private async Task LoadSupplierOptionsAsync()
    {
        var rowsToLoad = Items
            .Where(item => item.ArticoloOid.HasValue && item.ArticoloOid.Value > 0)
            .ToList();

        foreach (var row in rowsToLoad)
        {
            var history = await _documentReadService.SearchArticlePurchaseHistoryAsync(
                new GestionaleArticlePurchaseSearchRequest
                {
                    ArticoloOid = row.ArticoloOid,
                    MaxResults = 200
                });

            var supplierOptions = history.Items
                .Where(item => item.FornitoreOid.HasValue && !string.IsNullOrWhiteSpace(item.FornitoreNominativo))
                .GroupBy(item => item.FornitoreOid!.Value)
                .Select(group =>
                {
                    var latest = group
                        .OrderByDescending(item => item.DataDocumento)
                        .ThenByDescending(item => item.DocumentoOid)
                        .First();
                    var bestPrice = group.Min(item => item.PrezzoUnitario > 0 ? item.PrezzoUnitario : decimal.MaxValue);
                    return new ReorderSupplierOptionViewModel
                    {
                        Oid = group.Key,
                        Nome = latest.FornitoreNominativo,
                        PrezzoRiferimento = bestPrice == decimal.MaxValue ? latest.PrezzoUnitario : bestPrice,
                        DataUltimoAcquisto = latest.DataDocumento
                    };
                })
                .OrderBy(option => option.PrezzoRiferimento <= 0 ? decimal.MaxValue : option.PrezzoRiferimento)
                .ThenByDescending(option => option.DataUltimoAcquisto)
                .ThenBy(option => option.Nome, StringComparer.OrdinalIgnoreCase)
                .ToList();

            row.SetSupplierOptions(supplierOptions);
        }
    }

    private void ScheduleArticleLookup()
    {
        _articleLookupCts?.Cancel();
        _articleLookupCts?.Dispose();

        if (string.IsNullOrWhiteSpace(SearchArticleText))
        {
            ArticleLookupResults.Clear();
            IsArticlePopupOpen = false;
            return;
        }

        var requestVersion = ++_articleLookupRequestVersion;
        var cts = new CancellationTokenSource();
        _articleLookupCts = cts;
        _ = ScheduleArticleLookupCoreAsync(cts.Token, requestVersion, SearchArticleText);
    }

    private async Task ScheduleArticleLookupCoreAsync(CancellationToken cancellationToken, int requestVersion, string searchText)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (cancellationToken.IsCancellationRequested || !IsArticleLookupRequestCurrent(requestVersion, searchText))
            {
                return;
            }

            var results = await _articleReadService.SearchArticlesAsync(searchText, cancellationToken: cancellationToken);
            if (!IsArticleLookupRequestCurrent(requestVersion, searchText))
            {
                return;
            }

            var normalizedResults = NormalizeArticleLookupResults(results);
            ArticleLookupResults.Clear();
            foreach (var result in normalizedResults)
            {
                ArticleLookupResults.Add(result);
            }

            IsArticlePopupOpen = ArticleLookupResults.Count > 0;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task AddManualArticleAsync()
    {
        if (SelectedArticleLookup is null)
        {
            return;
        }

        var articolo = SelectedArticleLookup;
        var pricingDetail = await _articleReadService.GetArticlePricingDetailAsync(articolo);
        var supplierSuggestion = await ResolveSuggestedSupplierAsync(articolo.Oid);
        var item = new ReorderListItem
        {
            ArticoloOid = articolo.Oid,
            CodiceArticolo = articolo.CodiceArticolo,
            Descrizione = articolo.DisplayLabel,
            Quantita = 1,
            QuantitaDaOrdinare = 1,
            UnitaMisura = pricingDetail?.UnitaMisuraPrincipale ?? "PZ",
            FornitoreSuggeritoOid = supplierSuggestion?.Oid,
            FornitoreSuggeritoNome = supplierSuggestion?.Nome ?? string.Empty,
            FornitoreSelezionatoOid = supplierSuggestion?.Oid,
            FornitoreSelezionatoNome = supplierSuggestion?.Nome ?? string.Empty,
            PrezzoSuggerito = supplierSuggestion?.PrezzoRiferimento,
            IvaOid = articolo.IvaOid,
            Motivo = ReorderReason.Manuale,
            Stato = ReorderItemStatus.DaOrdinare,
            Operatore = "Banco"
        };

        await _repository.AddOrIncrementItemAsync(item);
        _isUpdatingArticleLookupText = true;
        SearchArticleText = string.Empty;
        _isUpdatingArticleLookupText = false;
        SelectedArticleLookup = null;
        ArticleLookupResults.Clear();
        IsArticlePopupOpen = false;
        await RefreshAsync();
        StatusMessage = $"Articolo {articolo.CodiceArticolo} aggiunto alla lista riordino.";
    }

    private void SyncColumnsFromLayout(GridLayoutSettings layout)
    {
        _columns.Clear();
        foreach (var pair in layout.Columns)
        {
            _columns[pair.Key] = pair.Value;
        }

        foreach (var definition in ColumnDefinitions)
        {
            if (_columns.ContainsKey(definition.Key))
            {
                continue;
            }

            _columns[definition.Key] = new GridColumnLayoutState
            {
                Width = definition.DefaultWidth,
                DisplayIndex = definition.DefaultDisplayIndex,
                IsVisible = definition.IsVisibleByDefault
            };
        }

        RaiseColumnVisibilityNotifications();
    }

    private void RaiseColumnVisibilityNotifications()
    {
        NotifyPropertyChanged(nameof(IsCodiceColumnVisible));
        NotifyPropertyChanged(nameof(IsOrdinatoColumnVisible));
        NotifyPropertyChanged(nameof(IsDescrizioneColumnVisible));
        NotifyPropertyChanged(nameof(IsQuantitaColumnVisible));
        NotifyPropertyChanged(nameof(IsQuantitaDaOrdinareColumnVisible));
        NotifyPropertyChanged(nameof(IsUnitaMisuraColumnVisible));
        NotifyPropertyChanged(nameof(IsFornitoreSuggeritoColumnVisible));
        NotifyPropertyChanged(nameof(IsFornitoreSelezionatoColumnVisible));
        NotifyPropertyChanged(nameof(IsPrezzoColumnVisible));
        NotifyPropertyChanged(nameof(IsMotivoColumnVisible));
        NotifyPropertyChanged(nameof(IsOperatoreColumnVisible));
        NotifyPropertyChanged(nameof(IsDataColumnVisible));
        NotifyPropertyChanged(nameof(IsNoteColumnVisible));
    }

    private bool FilterItems(object item)
    {
        if (item is not ReorderGridRowViewModel row)
        {
            return false;
        }

        return SelectedFilter switch
        {
            ReorderListFilter.OpenOnly => !row.IsOrdered,
            ReorderListFilter.OrderedOnly => row.IsOrdered,
            _ => true
        };
    }

    private void SetFilter(ReorderListFilter filter)
    {
        SelectedFilter = filter;
    }

    private void ToggleSupplierGrouping()
    {
        IsGroupedBySupplier = !IsGroupedBySupplier;
    }

    private void ApplyGrouping()
    {
        if (IsItemsViewInEditTransaction())
        {
            ScheduleGroupingRefresh();
            return;
        }

        try
        {
            ApplyGroupingCore();
        }
        catch (InvalidOperationException ex) when (IsDeferredRefreshBlockedByEdit(ex))
        {
            ScheduleGroupingRefresh();
        }
    }

    private void ApplyGroupingCore()
    {
        using (_itemsView.DeferRefresh())
        {
            _itemsView.GroupDescriptions.Clear();
            _itemsView.SortDescriptions.Clear();

            if (IsGroupedBySupplier)
            {
                _itemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ReorderGridRowViewModel.FornitoreGruppoLabel)));
                _itemsView.SortDescriptions.Add(new SortDescription(nameof(ReorderGridRowViewModel.FornitoreGruppoLabel), ListSortDirection.Ascending));
            }

            _itemsView.SortDescriptions.Add(new SortDescription(nameof(ReorderGridRowViewModel.IsOrdered), ListSortDirection.Ascending));
            _itemsView.SortDescriptions.Add(new SortDescription(nameof(ReorderGridRowViewModel.Descrizione), ListSortDirection.Ascending));
            _itemsView.SortDescriptions.Add(new SortDescription(nameof(ReorderGridRowViewModel.DataInserimento), ListSortDirection.Ascending));
        }
    }

    private void ScheduleGroupingRefresh()
    {
        if (_groupingRefreshPending)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _groupingRefreshPending = true;
        _ = dispatcher.BeginInvoke(() =>
        {
            _groupingRefreshPending = false;
            ApplyGrouping();
            NotifySummary();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private bool IsItemsViewInEditTransaction()
    {
        return _itemsView is IEditableCollectionView editableView &&
               (editableView.IsAddingNew || editableView.IsEditingItem);
    }

    private static bool IsDeferredRefreshBlockedByEdit(InvalidOperationException ex)
    {
        return ex.Message.Contains("DeferRefresh", StringComparison.OrdinalIgnoreCase) &&
               (ex.Message.Contains("AddNew", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("EditItem", StringComparison.OrdinalIgnoreCase));
    }

    private async Task RebuildSupplierDraftsAsync()
    {
        await _supplierDraftSemaphore.WaitAsync();
        try
        {
            var selectedSupplierName = SelectedSupplierDraft?.SupplierName;
            var groups = Items
                .Where(item => item.IsOrdered)
                .GroupBy(item => item.FornitoreGruppoLabel, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var states = await _repository.GetOrCreateSupplierDraftStatesAsync(_currentListId, groups.Select(group => group.Key).ToList());
            var stateBySupplier = states
                .GroupBy(state => state.SupplierName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(state => state.UpdatedAt).ThenByDescending(state => state.LocalCounter).First(),
                    StringComparer.OrdinalIgnoreCase);

            var drafts = new List<ReorderSupplierDraftViewModel>(groups.Count);
            for (var index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                var rows = group
                    .OrderBy(item => item.Descrizione, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var state = stateBySupplier.TryGetValue(group.Key, out var currentState)
                    ? currentState
                    : new ReorderSupplierDraftState
                    {
                        ListId = _currentListId,
                        SupplierName = group.Key,
                        LocalCounter = index + 1,
                        DraftDate = DateTimeOffset.Now,
                        Status = ReorderSupplierDraftStatus.Aperta
                    };

                drafts.Add(new ReorderSupplierDraftViewModel
                {
                    ListId = _currentListId,
                    SupplierName = group.Key,
                    CounterLabel = $"#{state.LocalCounter:00}",
                    DateLabel = state.DraftDate.ToString("dd/MM/yyyy"),
                    RowCount = rows.Count,
                    TotalQuantityToOrder = rows.Sum(item => item.QuantitaDaOrdinare <= 0 ? item.Quantita : item.QuantitaDaOrdinare),
                    Status = state.Status,
                    FmDocumentOid = state.FmDocumentOid,
                    FmDocumentNumber = state.FmDocumentNumber,
                    FmDocumentYear = state.FmDocumentYear,
                    Rows = rows
                });
            }

            SupplierDrafts.Clear();
            foreach (var draft in drafts)
            {
                SupplierDrafts.Add(draft);
            }

            SelectedSupplierDraft = string.IsNullOrWhiteSpace(selectedSupplierName)
                ? SupplierDrafts.FirstOrDefault()
                : SupplierDrafts.FirstOrDefault(item =>
                    string.Equals(item.SupplierName, selectedSupplierName, StringComparison.OrdinalIgnoreCase))
                  ?? SupplierDrafts.FirstOrDefault();

            NotifySummary();
        }
        finally
        {
            _supplierDraftSemaphore.Release();
        }
    }

    private void SelectSupplierDraft(ReorderSupplierDraftViewModel? draft)
    {
        SelectedSupplierDraft = draft;
    }

    private async Task SetSupplierDraftStatusAsync(ReorderSupplierDraftStatus status)
    {
        if (SelectedSupplierDraft is null || _currentListId == Guid.Empty)
        {
            return;
        }

        await _repository.SetSupplierDraftStatusAsync(_currentListId, SelectedSupplierDraft.SupplierName, status);
        await RebuildSupplierDraftsAsync();
        StatusMessage = status switch
        {
            ReorderSupplierDraftStatus.Ordinata => $"Lista fornitore segnata come ordine fatto per {SelectedSupplierDraft?.SupplierName}.",
            ReorderSupplierDraftStatus.RegistrataSuFm => $"Lista fornitore segnata come creata su FM per {SelectedSupplierDraft?.SupplierName}.",
            ReorderSupplierDraftStatus.Chiusa => $"Lista fornitore chiusa per {SelectedSupplierDraft?.SupplierName}.",
            _ => StatusMessage
        };
    }

    private async Task RemoveSelectedSupplierDraftAsync()
    {
        if (SelectedSupplierDraft is null || _currentListId == Guid.Empty)
        {
            return;
        }

        var supplierName = SelectedSupplierDraft.SupplierName;
        await _repository.RemoveSupplierDraftAsync(_currentListId, supplierName);
        await RefreshAsync();
        StatusMessage = $"Lista fornitore rimossa per {supplierName}.";
    }

    private async Task CreateSupplierOrderOnFmAsync()
    {
        if (SelectedSupplierDraft is null || _currentListId == Guid.Empty)
        {
            return;
        }

        var supplierOid = SelectedSupplierDraft.Rows
            .Select(row => row.FornitoreGruppoOid)
            .FirstOrDefault(oid => oid.HasValue && oid.Value > 0);
        if (!supplierOid.HasValue || supplierOid.Value <= 0)
        {
            throw new InvalidOperationException("La lista fornitore selezionata non ha un OID fornitore valido per FM.");
        }

        var request = new GestionaleSupplierOrderRequest
        {
            SupplierOid = supplierOid.Value,
            SupplierName = SelectedSupplierDraft.SupplierName,
            OperatorName = SelectedSupplierDraft.Rows.Select(row => row.Operatore).FirstOrDefault(operatorName => !string.IsNullOrWhiteSpace(operatorName)) ?? "Banco",
            DocumentDate = DateTime.Today,
            Rows = SelectedSupplierDraft.Rows
                .Select((row, index) => new GestionaleSupplierOrderRow
                {
                    OrdineRiga = index + 1,
                    ArticoloOid = row.ArticoloOid,
                    CodiceArticolo = row.CodiceArticolo,
                    Descrizione = row.Descrizione,
                    UnitaMisura = row.UnitaMisura,
                    Quantita = row.QuantitaDaOrdinare <= 0 ? row.Quantita : row.QuantitaDaOrdinare,
                    PrezzoUnitario = row.PrezzoSuggerito ?? 0,
                    IvaOid = row.IvaOid
                })
                .ToList()
        };

        var result = await _supplierOrderWriteService.CreateSupplierOrderAsync(request);
        await _repository.SetSupplierDraftFmDocumentAsync(
            _currentListId,
            SelectedSupplierDraft.SupplierName,
            result.DocumentoGestionaleOid,
            result.NumeroDocumentoGestionale,
            result.AnnoDocumentoGestionale);
        await RebuildSupplierDraftsAsync();
        StatusMessage = $"Ordine fornitore FM creato: {result.NumeroDocumentoGestionale}/{result.AnnoDocumentoGestionale} per {request.SupplierName}.";
    }

    private void RaiseFilterCommandStateChanged()
    {
        ShowOpenOnlyCommand.RaiseCanExecuteChanged();
        ShowAllCommand.RaiseCanExecuteChanged();
        ShowOrderedOnlyCommand.RaiseCanExecuteChanged();
    }

    private void NotifySelectionStateChanged()
    {
        MarkOrderedCommand.RaiseCanExecuteChanged();
        MarkPendingCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        NotifyPropertyChanged(nameof(SelectedRowsCount));
        NotifyPropertyChanged(nameof(HasSelectedRows));
        NotifyPropertyChanged(nameof(CanEditSelectedRow));
        NotifyPropertyChanged(nameof(CanRemoveSelectedRows));
        NotifyPropertyChanged(nameof(CanConfirmSelectedRows));
        NotifyPropertyChanged(nameof(CanReopenSelectedRows));
    }

    private bool IsArticleLookupRequestCurrent(int requestVersion, string searchText)
    {
        return requestVersion == _articleLookupRequestVersion &&
               string.Equals(SearchArticleText, searchText, StringComparison.Ordinal);
    }

    private async Task<ReorderSupplierSuggestion?> ResolveSuggestedSupplierAsync(int articoloOid)
    {
        var history = await _documentReadService.SearchArticlePurchaseHistoryAsync(
            new GestionaleArticlePurchaseSearchRequest
            {
                ArticoloOid = articoloOid,
                MaxResults = 200
            });

        return history.Items
            .Where(item => item.FornitoreOid.HasValue && !string.IsNullOrWhiteSpace(item.FornitoreNominativo))
            .GroupBy(item => item.FornitoreOid!.Value)
            .Select(group =>
            {
                var latest = group
                    .OrderByDescending(item => item.DataDocumento)
                    .ThenByDescending(item => item.DocumentoOid)
                    .First();
                var bestPrice = group.Min(item => item.PrezzoUnitario > 0 ? item.PrezzoUnitario : decimal.MaxValue);
                return new ReorderSupplierSuggestion(
                    group.Key,
                    latest.FornitoreNominativo,
                    bestPrice == decimal.MaxValue ? latest.PrezzoUnitario : bestPrice,
                    latest.DataDocumento);
            })
            .OrderBy(option => option.PrezzoRiferimento <= 0 ? decimal.MaxValue : option.PrezzoRiferimento)
            .ThenByDescending(option => option.DataUltimoAcquisto)
            .ThenBy(option => option.Nome, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static IReadOnlyList<GestionaleArticleSearchResult> NormalizeArticleLookupResults(
        IReadOnlyList<GestionaleArticleSearchResult> results)
    {
        if (results.Count <= 1)
        {
            return results;
        }

        var parentOidsWithVariants = results
            .Where(item => item.IsVariante && item.ArticoloPadreOid.HasValue)
            .Select(item => item.ArticoloPadreOid!.Value)
            .ToHashSet();

        var keysWithVariants = results
            .Where(item => item.IsVariante)
            .Select(BuildArticleVariantGroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return results
            .Where(item => item.IsVariante || !ShouldHideParentArticle(item, parentOidsWithVariants, keysWithVariants))
            .OrderBy(item => item.CodiceArticolo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Descrizione, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.IsVariante ? 0 : 1)
            .ThenBy(item => item.VarianteDescrizione, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.VarianteNome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldHideParentArticle(
        GestionaleArticleSearchResult item,
        ISet<int> parentOidsWithVariants,
        ISet<string> keysWithVariants)
    {
        if (item.IsVariante)
        {
            return false;
        }

        return parentOidsWithVariants.Contains(item.Oid) ||
               keysWithVariants.Contains(BuildArticleVariantGroupKey(item));
    }

    private static string BuildArticleVariantGroupKey(GestionaleArticleSearchResult item)
    {
        return $"{item.CodiceArticolo.Trim().ToUpperInvariant()}|{item.Descrizione.Trim().ToUpperInvariant()}";
    }

    private void OnCurrentListChanged()
    {
        _ = RefreshAsync();
    }
}

internal sealed record ReorderSupplierSuggestion(int Oid, string Nome, decimal PrezzoRiferimento, DateTime DataUltimoAcquisto);

public enum ReorderListFilter
{
    OpenOnly = 0,
    All = 1,
    OrderedOnly = 2
}

internal static class ReorderColumnDefinitions
{
    public static IReadOnlyList<GridColumnDefinition> All { get; } =
    [
        new GridColumnDefinition { Key = "Ordinato", Header = "Ok", IsVisibleByDefault = true, DefaultWidth = 56, DefaultDisplayIndex = 0 },
        new GridColumnDefinition { Key = "Codice", Header = "Codice", IsVisibleByDefault = true, DefaultWidth = 90, DefaultDisplayIndex = 1 },
        new GridColumnDefinition { Key = "Descrizione", Header = "Descrizione", IsVisibleByDefault = true, DefaultWidth = 260, DefaultDisplayIndex = 2 },
        new GridColumnDefinition { Key = "Quantita", Header = "Qta`", IsVisibleByDefault = true, DefaultWidth = 70, DefaultDisplayIndex = 3, IsNumeric = true },
        new GridColumnDefinition { Key = "QuantitaDaOrdinare", Header = "Q.ta da ordinare", IsVisibleByDefault = true, DefaultWidth = 120, DefaultDisplayIndex = 4, IsNumeric = true },
        new GridColumnDefinition { Key = "UnitaMisura", Header = "U.M.", IsVisibleByDefault = true, DefaultWidth = 70, DefaultDisplayIndex = 5 },
        new GridColumnDefinition { Key = "FornitoreSuggerito", Header = "Fornitore suggerito", IsVisibleByDefault = true, DefaultWidth = 180, DefaultDisplayIndex = 6 },
        new GridColumnDefinition { Key = "FornitoreSelezionato", Header = "Fornitore scelto", IsVisibleByDefault = true, DefaultWidth = 180, DefaultDisplayIndex = 7 },
        new GridColumnDefinition { Key = "Prezzo", Header = "Prezzo", IsVisibleByDefault = true, DefaultWidth = 90, DefaultDisplayIndex = 8, IsNumeric = true },
        new GridColumnDefinition { Key = "Motivo", Header = "Motivo", IsVisibleByDefault = true, DefaultWidth = 120, DefaultDisplayIndex = 9 },
        new GridColumnDefinition { Key = "Operatore", Header = "Operatore", IsVisibleByDefault = true, DefaultWidth = 120, DefaultDisplayIndex = 10 },
        new GridColumnDefinition { Key = "Data", Header = "Data", IsVisibleByDefault = true, DefaultWidth = 110, DefaultDisplayIndex = 11 },
        new GridColumnDefinition { Key = "Note", Header = "Note", IsVisibleByDefault = false, DefaultWidth = 180, DefaultDisplayIndex = 12 }
    ];
}
