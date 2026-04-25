using System.Collections.ObjectModel;
using System.Globalization;
using Banco.UI.Shared.Grid;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using Banco.Vendita.Customers;
using Banco.Vendita.Documents;

namespace Banco.UI.Wpf.ViewModels;

public sealed class PurchaseHistoryViewModel : DateFilterViewModelBase
{
    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly Dictionary<string, GridColumnLayoutState> _columns = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _articleLookupCts;
    private CancellationTokenSource? _supplierLookupCts;
    private bool _isLoading;
    private string _searchArticleText = string.Empty;
    private string _searchSupplierText = string.Empty;
    private GestionaleArticleSearchResult? _selectedArticle;
    private GestionaleCustomerSummary? _selectedSupplier;
    private bool _isArticlePopupOpen;
    private bool _isSupplierPopupOpen;
    private bool _isUpdatingArticleLookupText;
    private bool _isUpdatingSupplierLookupText;
    private string _statusMessage = "Imposta almeno un filtro per avviare la ricerca acquisti.";
    private string _emptyStateTitle = "Ricerca pronta";
    private string _emptyStateMessage = "Seleziona un articolo, un fornitore o un periodo per consultare lo storico acquisti.";
    private bool _isArticleFilterLocked;
    private PurchaseHistoryOpenMode _openMode;

    public PurchaseHistoryViewModel(
        IGestionaleDocumentReadService documentReadService,
        IGestionaleArticleReadService articleReadService,
        IGestionaleCustomerReadService customerReadService,
        IApplicationConfigurationService configurationService)
    {
        _documentReadService = documentReadService;
        _articleReadService = articleReadService;
        _customerReadService = customerReadService;
        _configurationService = configurationService;

        Results = [];
        ArticleLookupResults = [];
        SupplierLookupResults = [];

        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    public ObservableCollection<GestionaleArticlePurchaseHistoryItem> Results { get; }

    public ObservableCollection<GestionaleArticleSearchResult> ArticleLookupResults { get; }

    public ObservableCollection<GestionaleCustomerSummary> SupplierLookupResults { get; }

    public RelayCommand ClearFiltersCommand { get; }

    public IReadOnlyList<GridColumnDefinition> ColumnDefinitions => PurchaseHistoryColumnDefinitions;

    public PurchaseHistoryOpenMode OpenMode
    {
        get => _openMode;
        private set => SetProperty(ref _openMode, value);
    }

    public bool IsArticleContext => OpenMode == PurchaseHistoryOpenMode.ArticleContext;

    public string WindowTitle => IsArticleContext
        ? $"Storico acquisti articolo - {SelectedArticleDisplay}"
        : "Ricerca acquisti";

    public string WindowSubtitle => IsArticleContext
        ? "Storico acquisti contestualizzato sull'articolo selezionato nel Banco."
        : "Ricerca libera per articolo, fornitore e periodo.";

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsArticleFilterLocked
    {
        get => _isArticleFilterLocked;
        private set
        {
            if (SetProperty(ref _isArticleFilterLocked, value))
            {
                NotifyPropertyChanged(nameof(CanEditArticleFilter));
            }
        }
    }

    public bool CanEditArticleFilter => !IsArticleFilterLocked;

    public string SearchArticleText
    {
        get => _searchArticleText;
        set
        {
            if (!SetProperty(ref _searchArticleText, value))
            {
                return;
            }

            if (IsArticleFilterLocked)
            {
                return;
            }

            if (_isUpdatingArticleLookupText)
            {
                return;
            }

            if (_selectedArticle is not null &&
                !_selectedArticle.DisplayLabel.Equals(value ?? string.Empty, StringComparison.Ordinal))
            {
                SelectedArticle = null;
            }

            ScheduleArticleLookup();
        }
    }

    public string SearchSupplierText
    {
        get => _searchSupplierText;
        set
        {
            if (!SetProperty(ref _searchSupplierText, value))
            {
                return;
            }

            if (_isUpdatingSupplierLookupText)
            {
                return;
            }

            if (_selectedSupplier is not null &&
                !_selectedSupplier.DisplayLabel.Equals(value ?? string.Empty, StringComparison.Ordinal))
            {
                SelectedSupplier = null;
            }

            ScheduleSupplierLookup();
        }
    }

    public GestionaleArticleSearchResult? SelectedArticle
    {
        get => _selectedArticle;
        private set
        {
            if (!SetProperty(ref _selectedArticle, value))
            {
                return;
            }

            NotifyPropertyChanged(nameof(SelectedArticleDisplay));
            NotifyPropertyChanged(nameof(WindowTitle));
            ScheduleRefresh();
        }
    }

    public GestionaleCustomerSummary? SelectedSupplier
    {
        get => _selectedSupplier;
        private set
        {
            if (!SetProperty(ref _selectedSupplier, value))
            {
                return;
            }

            NotifyPropertyChanged(nameof(SelectedSupplierDisplay));
            ScheduleRefresh();
        }
    }

    public string SelectedArticleDisplay => SelectedArticle?.DisplayLabel ?? string.Empty;

    public string SelectedSupplierDisplay => SelectedSupplier?.DisplayLabel ?? string.Empty;

    public bool IsArticlePopupOpen
    {
        get => _isArticlePopupOpen;
        set => SetProperty(ref _isArticlePopupOpen, value);
    }

    public bool IsSupplierPopupOpen
    {
        get => _isSupplierPopupOpen;
        set => SetProperty(ref _isSupplierPopupOpen, value);
    }

    public decimal TotaleAcquistato { get; private set; }

    public decimal PezziAcquistati { get; private set; }

    public decimal UltimoPrezzo { get; private set; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string EmptyStateTitle
    {
        get => _emptyStateTitle;
        private set => SetProperty(ref _emptyStateTitle, value);
    }

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set => SetProperty(ref _emptyStateMessage, value);
    }

    public bool HasResults => Results.Count > 0;

    public async Task<GridLayoutSettings> GetGridLayoutAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = GridLayoutMigration.GetOrCreatePurchaseHistoryLayout(settings, ColumnDefinitions);
        SyncColumnsFromLayout(layout);
        return layout;
    }

    public async Task SaveGridLayoutAsync(GridLayoutSettings layout)
    {
        var settings = await _configurationService.LoadAsync();
        settings.GridLayouts[GridLayoutMigration.PurchaseHistoryGridId] = layout;
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

    public void InitializeForArticleContext(GestionaleArticleSearchResult articolo)
    {
        ArgumentNullException.ThrowIfNull(articolo);

        _articleLookupCts?.Cancel();
        _supplierLookupCts?.Cancel();
        OpenMode = PurchaseHistoryOpenMode.ArticleContext;
        SetInitialDateFilterState(null, null, null, null);
        IsArticleFilterLocked = true;
        _selectedArticle = articolo;
        _searchArticleText = articolo.DisplayLabel;
        _selectedSupplier = null;
        _searchSupplierText = string.Empty;
        ArticleLookupResults.Clear();
        SupplierLookupResults.Clear();
        IsArticlePopupOpen = false;
        IsSupplierPopupOpen = false;
        NotifyPropertyChanged(nameof(SearchArticleText));
        NotifyPropertyChanged(nameof(SearchSupplierText));
        NotifyPropertyChanged(nameof(SelectedArticleDisplay));
        NotifyPropertyChanged(nameof(SelectedSupplierDisplay));
        NotifyPropertyChanged(nameof(WindowTitle));
        NotifyPropertyChanged(nameof(WindowSubtitle));
        ScheduleRefresh(0);
    }

    public void InitializeForFreeSearch()
    {
        _articleLookupCts?.Cancel();
        _supplierLookupCts?.Cancel();
        OpenMode = PurchaseHistoryOpenMode.FreeSearch;
        IsArticleFilterLocked = false;
        _selectedArticle = null;
        _selectedSupplier = null;
        _searchArticleText = string.Empty;
        _searchSupplierText = string.Empty;
        Results.Clear();
        ArticleLookupResults.Clear();
        SupplierLookupResults.Clear();
        IsArticlePopupOpen = false;
        IsSupplierPopupOpen = false;
        ResetSummary();
        SetInitialDateFilterState(null, null, null, null);
        NotifyPropertyChanged(nameof(SearchArticleText));
        NotifyPropertyChanged(nameof(SearchSupplierText));
        NotifyPropertyChanged(nameof(SelectedArticleDisplay));
        NotifyPropertyChanged(nameof(SelectedSupplierDisplay));
        NotifyPropertyChanged(nameof(WindowTitle));
        NotifyPropertyChanged(nameof(WindowSubtitle));
        UpdateIdleState();
    }

    public void SelectArticleFromLookup(GestionaleArticleSearchResult? articolo)
    {
        if (articolo is null)
        {
            return;
        }

        SelectedArticle = articolo;
        _isUpdatingArticleLookupText = true;
        try
        {
            SearchArticleText = articolo.DisplayLabel;
        }
        finally
        {
            _isUpdatingArticleLookupText = false;
        }
        IsArticlePopupOpen = false;
        ArticleLookupResults.Clear();
    }

    public void SelectSupplierFromLookup(GestionaleCustomerSummary? fornitore)
    {
        if (fornitore is null)
        {
            return;
        }

        SelectedSupplier = fornitore;
        _isUpdatingSupplierLookupText = true;
        try
        {
            SearchSupplierText = fornitore.DisplayLabel;
        }
        finally
        {
            _isUpdatingSupplierLookupText = false;
        }
        IsSupplierPopupOpen = false;
        SupplierLookupResults.Clear();
    }

    public async Task ForceRefreshAsync()
    {
        await ExecuteSearchAsync(CancellationToken.None);
    }

    protected override async Task RefreshOnFiltersChangedAsync(CancellationToken cancellationToken)
    {
        await ExecuteSearchAsync(cancellationToken);
    }

    private async Task ExecuteSearchAsync(CancellationToken cancellationToken)
    {
        if (!CanRunSearch())
        {
            Results.Clear();
            ResetSummary();
            UpdateIdleState();
            return;
        }

        if (!HasIntervalloValido)
        {
            Results.Clear();
            ResetSummary();
            EmptyStateTitle = "Intervallo non valido";
            EmptyStateMessage = "Correggi le date: la data iniziale non puo` essere successiva alla data finale.";
            StatusMessage = EmptyStateMessage;
            return;
        }

        IsLoading = true;

        try
        {
            var detail = await _documentReadService.SearchArticlePurchaseHistoryAsync(
                new GestionaleArticlePurchaseSearchRequest
                {
                    ArticoloOid = SelectedArticle?.Oid,
                    FornitoreOid = SelectedSupplier?.Oid,
                    DataInizio = DataInizio,
                    DataFine = DataFine
                },
                cancellationToken);

            Results.Clear();
            foreach (var item in detail.Items)
            {
                Results.Add(item);
            }

            TotaleAcquistato = detail.Summary.TotaleAcquistato;
            PezziAcquistati = detail.Summary.PezziAcquistati;
            UltimoPrezzo = detail.Summary.UltimoPrezzo;
            NotifyPropertyChanged(nameof(TotaleAcquistato));
            NotifyPropertyChanged(nameof(PezziAcquistati));
            NotifyPropertyChanged(nameof(UltimoPrezzo));
            NotifyPropertyChanged(nameof(HasResults));

            if (detail.Summary.HasResults)
            {
                StatusMessage = $"Righe trovate: {Results.Count}.";
                EmptyStateTitle = "Nessun acquisto trovato";
                EmptyStateMessage = "Nessuna riga di acquisto corrisponde ai filtri impostati.";
            }
            else
            {
                StatusMessage = "Nessun acquisto trovato per i filtri correnti.";
                EmptyStateTitle = "Nessun acquisto trovato";
                EmptyStateMessage = "Modifica articolo, fornitore o periodo per ampliare la ricerca.";
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch
        {
            Results.Clear();
            ResetSummary();
            EmptyStateTitle = "Ricerca non disponibile";
            EmptyStateMessage = "Si e` verificato un problema durante la lettura dello storico acquisti.";
            StatusMessage = EmptyStateMessage;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRunSearch()
    {
        if (IsArticleContext)
        {
            return SelectedArticle?.Oid > 0;
        }

        return SelectedArticle?.Oid > 0
               || SelectedSupplier?.Oid > 0
               || DataInizio.HasValue
               || DataFine.HasValue;
    }

    private void UpdateIdleState()
    {
        EmptyStateTitle = "Ricerca pronta";
        EmptyStateMessage = IsArticleContext
            ? "Seleziona un articolo valido nel Banco per caricare lo storico acquisti."
            : "Imposta almeno articolo, fornitore o periodo per avviare la ricerca.";
        StatusMessage = EmptyStateMessage;
        NotifyPropertyChanged(nameof(HasResults));
    }

    private void ClearFilters()
    {
        if (IsArticleContext)
        {
            SetInitialDateFilterState(null, null, null, null);
            ScheduleRefresh(0);
            return;
        }

        InitializeForFreeSearch();
    }

    private void ResetSummary()
    {
        TotaleAcquistato = 0;
        PezziAcquistati = 0;
        UltimoPrezzo = 0;
        NotifyPropertyChanged(nameof(TotaleAcquistato));
        NotifyPropertyChanged(nameof(PezziAcquistati));
        NotifyPropertyChanged(nameof(UltimoPrezzo));
        NotifyPropertyChanged(nameof(HasResults));
    }

    private void ScheduleArticleLookup()
    {
        _articleLookupCts?.Cancel();
        _articleLookupCts?.Dispose();

        if (IsArticleFilterLocked || string.IsNullOrWhiteSpace(SearchArticleText))
        {
            ArticleLookupResults.Clear();
            IsArticlePopupOpen = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _articleLookupCts = cts;
        _ = ScheduleArticleLookupCoreAsync(cts.Token);
    }

    private async Task ScheduleArticleLookupCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var results = await _articleReadService.SearchArticlesAsync(SearchArticleText, cancellationToken: cancellationToken);
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

        var filtered = results
            .Where(item => item.IsVariante || !ShouldHideParentArticle(item, parentOidsWithVariants, keysWithVariants))
            .OrderBy(item => item.CodiceArticolo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Descrizione, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.IsVariante ? 0 : 1)
            .ThenBy(item => item.VarianteDescrizione, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.VarianteNome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return filtered;
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

        if (parentOidsWithVariants.Contains(item.Oid))
        {
            return true;
        }

        return keysWithVariants.Contains(BuildArticleVariantGroupKey(item));
    }

    private static string BuildArticleVariantGroupKey(GestionaleArticleSearchResult item)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{item.CodiceArticolo.Trim().ToUpperInvariant()}|{item.Descrizione.Trim().ToUpperInvariant()}");
    }

    private void ScheduleSupplierLookup()
    {
        _supplierLookupCts?.Cancel();
        _supplierLookupCts?.Dispose();

        if (string.IsNullOrWhiteSpace(SearchSupplierText))
        {
            SupplierLookupResults.Clear();
            IsSupplierPopupOpen = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _supplierLookupCts = cts;
        _ = ScheduleSupplierLookupCoreAsync(cts.Token);
    }

    private async Task ScheduleSupplierLookupCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var results = await _customerReadService.SearchSuppliersAsync(SearchSupplierText, cancellationToken: cancellationToken);
            if (results.Count == 0)
            {
                results = await _customerReadService.SearchCustomersAsync(SearchSupplierText, cancellationToken: cancellationToken);
            }

            SupplierLookupResults.Clear();
            foreach (var result in results
                         .Where(item => item.Oid > 0 && !item.IsClienteGenerico)
                         .DistinctBy(item => item.Oid))
            {
                SupplierLookupResults.Add(result);
            }

            IsSupplierPopupOpen = SupplierLookupResults.Count > 0;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void SyncColumnsFromLayout(GridLayoutSettings layout)
    {
        _columns.Clear();
        foreach (var definition in ColumnDefinitions)
        {
            if (!layout.Columns.TryGetValue(definition.Key, out var state))
            {
                state = new GridColumnLayoutState
                {
                    Width = definition.DefaultWidth,
                    DisplayIndex = definition.DefaultDisplayIndex,
                    IsVisible = definition.IsVisibleByDefault,
                    ContentAlignment = definition.TextAlignment
                };
                layout.Columns[definition.Key] = state;
            }

            _columns[definition.Key] = state;
        }
    }

    private static readonly IReadOnlyList<GridColumnDefinition> PurchaseHistoryColumnDefinitions =
    [
        new() { Key = "DataDocumento", Header = "Data", IsVisibleByDefault = true, DefaultWidth = 96, DefaultDisplayIndex = 0, Group = "Documento", Description = "Data del documento di acquisto.", MinWidth = 88, TextAlignment = GridColumnContentAlignment.Center, IsFrozen = true },
        new() { Key = "TipoDocumento", Header = "Tipo", IsVisibleByDefault = true, DefaultWidth = 132, DefaultDisplayIndex = 1, Group = "Documento", Description = "Tipo documento legacy.", MinWidth = 110, TextAlignment = GridColumnContentAlignment.Left },
        new() { Key = "CodiceArticolo", Header = "Articolo", IsVisibleByDefault = true, DefaultWidth = 100, DefaultDisplayIndex = 2, Group = "Articolo", Description = "Codice articolo della riga.", MinWidth = 90, TextAlignment = GridColumnContentAlignment.Left },
        new() { Key = "DescrizioneArticolo", Header = "Descrizione", IsVisibleByDefault = true, DefaultWidth = 280, DefaultDisplayIndex = 3, Group = "Articolo", Description = "Descrizione articolo acquistato.", MinWidth = 180, TextAlignment = GridColumnContentAlignment.Left },
        new() { Key = "FornitoreNominativo", Header = "Fornitore", IsVisibleByDefault = true, DefaultWidth = 220, DefaultDisplayIndex = 4, Group = "Fornitore", Description = "Fornitore collegato al documento di acquisto.", MinWidth = 150, TextAlignment = GridColumnContentAlignment.Left },
        new() { Key = "RiferimentoFattura", Header = "FT", IsVisibleByDefault = true, DefaultWidth = 120, DefaultDisplayIndex = 5, Group = "Documento", Description = "Riferimento fattura o documento esterno.", MinWidth = 90, TextAlignment = GridColumnContentAlignment.Center },
        new() { Key = "Quantita", Header = "Pezzi", IsVisibleByDefault = true, DefaultWidth = 90, DefaultDisplayIndex = 6, Group = "Valori", Description = "Quantita` acquistata.", IsNumeric = true, MinWidth = 76, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "PrezzoUnitario", Header = "Prezzo", IsVisibleByDefault = true, DefaultWidth = 110, DefaultDisplayIndex = 7, Group = "Valori", Description = "Prezzo unitario di acquisto.", IsNumeric = true, MinWidth = 92, Format = "N2", TextAlignment = GridColumnContentAlignment.Right },
        new() { Key = "TotaleRiga", Header = "Totale", IsVisibleByDefault = true, DefaultWidth = 110, DefaultDisplayIndex = 8, Group = "Valori", Description = "Totale della riga di acquisto.", IsNumeric = true, MinWidth = 92, Format = "N2", TextAlignment = GridColumnContentAlignment.Right }
    ];
}
