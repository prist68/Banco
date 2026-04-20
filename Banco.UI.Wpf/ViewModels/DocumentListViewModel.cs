using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Banco.UI.Wpf.Infrastructure.GridColumns;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Documents;

namespace Banco.UI.Wpf.ViewModels;

public sealed class DocumentListViewModel : ViewModelBase
{
    public enum DocumentListSection
    {
        Documents = 0,
        ReorderList = 1
    }

    private sealed record FilteredDocumentTotals(
        int FilteredCount,
        int FilteredOfficialCount,
        decimal Totale,
        decimal Contanti,
        decimal Carta,
        decimal Web,
        decimal Buoni,
        decimal Sospeso,
        decimal ResiduoPagamento,
        decimal DaFiscalizzare,
        decimal TotaleContantiCartaScontrinati,
        decimal TotaleContantiCortesiaONonScontrinati,
        decimal TotaleSospesoSeparato);

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IGestionaleDocumentDeleteService _documentDeleteService;
    private readonly ILocalDocumentRepository _localDocumentRepository;
    private readonly IPosProcessLogService _logService;
    private readonly ReorderListViewModel _reorderListViewModel;
    private readonly Dictionary<string, GridColumnLayoutState> _columns = new(StringComparer.OrdinalIgnoreCase);
    private readonly CollectionViewSource _uiDocumentsViewSource = new();
    private string _statusMessage = "Caricamento documenti in corso...";
    private string _uiStatusMessage = "Caricamento documenti in corso...";
    private bool _isLoading;
    private bool _includeLocalDocuments;
    private bool _isDetailExpanded = true;
    private bool _isFilterOggiActive;
    private bool _isFilterSettimanaActive;
    private bool _isFilterMeseCorrenteActive;
    private string _nominativoSearchText = string.Empty;
    private DateTime? _dataDa;
    private DateTime? _dataA;
    private DateTime? _savedDataDa;
    private DateTime? _savedDataA;
    private double _listPanelWidth = 1.8;
    private double _detailPanelWidth = 1.0;
    private DocumentGridRowViewModel? _selectedDocument;
    private DocumentGridDetailViewModel? _selectedDocumentDetail;
    private decimal _filteredTotale;
    private decimal _filteredContanti;
    private decimal _filteredCarta;
    private decimal _filteredWeb;
    private decimal _filteredBuoni;
    private decimal _filteredSospeso;
    private decimal _filteredResiduoPagamento;
    private decimal _filteredDaFiscalizzare;
    private decimal _filteredTotaleContantiCartaScontrini;
    private decimal _filteredTotaleContantiCortesia;
    private decimal _filteredTotaleSospesoSeparato;
    private int _filteredCount;
    private int _filteredOfficialCount;
    private UIDocumentPresentationMode _uiDocumentPresentationMode = UIDocumentPresentationMode.Default;
    private decimal _uiFilteredTotale;
    private decimal _uiFilteredContanti;
    private decimal _uiFilteredCarta;
    private decimal _uiFilteredBuoni;
    private decimal _uiFilteredSospeso;
    private decimal _uiFilteredCortesia;
    private int _uiFilteredCount;
    private int _uiFilteredOfficialCount;
    private int _selectedDocumentsCount;
    private int _selectedDeletableDocumentsCount;
    private bool _isDeletingSelectedLocalDocuments;
    private DocumentListSection _selectedSection;
    private List<DocumentGridRowViewModel> _selectedDocuments = [];
    private DocumentListFilterMode _currentFilterMode = DocumentListFilterMode.Completa;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);
    private int _detailLoadVersion;

    public DocumentListViewModel(
        IGestionaleDocumentReadService documentReadService,
        IGestionaleDocumentDeleteService documentDeleteService,
        ILocalDocumentRepository localDocumentRepository,
        IApplicationConfigurationService configurationService,
        IPosProcessLogService logService,
        ReorderListViewModel reorderListViewModel)
    {
        _documentReadService = documentReadService;
        _documentDeleteService = documentDeleteService;
        _localDocumentRepository = localDocumentRepository;
        _configurationService = configurationService;
        _logService = logService;
        _reorderListViewModel = reorderListViewModel;
        _configurationService.SettingsChanged += OnSettingsChanged;

        Documents = [];
        DocumentsView = CollectionViewSource.GetDefaultView(Documents);
        DocumentsView.Filter = FilterDocument;
        _uiDocumentsViewSource.Source = Documents;
        UIDocumentsView = _uiDocumentsViewSource.View;
        UIDocumentsView.Filter = FilterUIDocument;

        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        FilterMeseCorrenteCommand = new RelayCommand(ApplyCurrentMonthFilter);
        FilterSettimanaCommand = new RelayCommand(ToggleFilterSettimana);
        ToggleUnscontrinatiModeCommand = new RelayCommand(ToggleUnscontrinatiMode);
        ToggleIncludeLocalDocumentsCommand = new RelayCommand(() => IncludeLocalDocuments = !IncludeLocalDocuments);
        ToggleDetailCommand = new RelayCommand(() => IsDetailExpanded = !IsDetailExpanded);
        FilterOggiCommand = new RelayCommand(ToggleFilterOggi);
        NewBancoDocumentCommand = new RelayCommand(() => NewBancoDocumentRequested?.Invoke());
        OpenDocumentInBancoCommand = new RelayCommand(OpenSelectedDocumentInBanco, () => SelectedDocument is not null);
        SetDocumentFilterModeCommand = new RelayCommand<string>(SetDocumentFilterMode);
        ShowDocumentsSectionCommand = new RelayCommand(ShowDocumentsSection);
        ShowReorderListSectionCommand = new RelayCommand(ShowReorderListSection);

        _ = InitializeAsync();
    }

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        if (!e.GestionaleDatabaseChanged)
        {
            return;
        }

        _ = HandleGestionaleConfigurationChangedAsync(e.Settings);
    }

    private async Task HandleGestionaleConfigurationChangedAsync(AppSettings settings)
    {
        StatusMessage = $"Configurazione DB aggiornata in tempo reale su {settings.GestionaleDatabase.Host}. Ricarico la lista documenti...";
        _logService.Info(nameof(DocumentListViewModel), $"Cambio configurazione DB rilevato. Host={settings.GestionaleDatabase.Host}, Database={settings.GestionaleDatabase.Database}.");
        await RefreshAsync();
    }

    public string Titolo => "Documenti";

    public ReorderListViewModel ReorderList => _reorderListViewModel;

    public DocumentListSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                NotifyPropertyChanged(nameof(SelectedSectionIndex));
                NotifyPropertyChanged(nameof(IsDocumentsSectionSelected));
                NotifyPropertyChanged(nameof(IsReorderListSectionSelected));
            }
        }
    }

    public event Action<int>? OpenDocumentInBancoRequested;

    public event Action<Guid>? OpenLocalDocumentInBancoRequested;

    public event Action? NewBancoDocumentRequested;

    public ObservableCollection<DocumentGridRowViewModel> Documents { get; }

    public ICollectionView DocumentsView { get; }

    public ICollectionView UIDocumentsView { get; }

    public RelayCommand ShowDocumentsSectionCommand { get; }

    public RelayCommand ShowReorderListSectionCommand { get; }

    public int SelectedSectionIndex
    {
        get => (int)SelectedSection;
        set
        {
            var normalized = Enum.IsDefined(typeof(DocumentListSection), value)
                ? (DocumentListSection)value
                : DocumentListSection.Documents;
            SelectedSection = normalized;
        }
    }

    public bool IsDocumentsSectionSelected => SelectedSection == DocumentListSection.Documents;

    public bool IsReorderListSectionSelected => SelectedSection == DocumentListSection.ReorderList;

    public DocumentGridRowViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetProperty(ref _selectedDocument, value))
            {
                OpenDocumentInBancoCommand.RaiseCanExecuteChanged();
                NotifyPropertyChanged(nameof(OpenDocumentInBancoLabel));
                NotifyPropertyChanged(nameof(OpenDocumentInBancoTooltip));
                _ = LoadDetailAsync(value);
            }
        }
    }

    public DocumentGridDetailViewModel? SelectedDocumentDetail
    {
        get => _selectedDocumentDetail;
        private set => SetProperty(ref _selectedDocumentDetail, value);
    }

    public ObservableCollection<DocumentGridDetailRowViewModel> SelectedDocumentRows { get; } = [];

    public string NominativoSearchText
    {
        get => _nominativoSearchText;
        set
        {
            if (SetProperty(ref _nominativoSearchText, value))
            {
                RefreshFilter();
            }
        }
    }

    public DateTime? DataDa
    {
        get => _dataDa;
        set
        {
            if (SetProperty(ref _dataDa, value))
            {
                // Disattiva filtro Oggi se l'utente cambia manualmente la data
                if (IsFilterOggiActive && value?.Date != DateTime.Today)
                {
                    IsFilterOggiActive = false;
                }

                if (IsFilterMeseCorrenteActive && value?.Date != GetCurrentMonthStart().Date)
                {
                    IsFilterMeseCorrenteActive = false;
                }

                if (IsFilterSettimanaActive && value?.Date != GetCurrentWeekStart().Date)
                {
                    IsFilterSettimanaActive = false;
                }

                RefreshFilter();
            }
        }
    }

    public DateTime? DataA
    {
        get => _dataA;
        set
        {
            if (SetProperty(ref _dataA, value))
            {
                // Disattiva filtro Oggi se l'utente cambia manualmente la data
                if (IsFilterOggiActive && value?.Date != DateTime.Today)
                {
                    IsFilterOggiActive = false;
                }

                if (IsFilterMeseCorrenteActive && value?.Date != GetCurrentMonthEnd().Date)
                {
                    IsFilterMeseCorrenteActive = false;
                }

                if (IsFilterSettimanaActive && value?.Date != GetCurrentWeekEnd().Date)
                {
                    IsFilterSettimanaActive = false;
                }

                RefreshFilter();
            }
        }
    }

    public bool IncludeLocalDocuments
    {
        get => _includeLocalDocuments;
        set
        {
            if (SetProperty(ref _includeLocalDocuments, value))
            {
                _ = PersistFlagsAsync();
                RefreshFilter();
            }
        }
    }

    public bool IsUnscontrinatiExpandedMode => false;

    public bool IsDetailExpanded
    {
        get => _isDetailExpanded;
        private set => SetProperty(ref _isDetailExpanded, value);
    }

    public bool IsFilterOggiActive
    {
        get => _isFilterOggiActive;
        private set
        {
            if (SetProperty(ref _isFilterOggiActive, value))
            {
                NotifyPropertyChanged(nameof(OggiButtonLabel));
            }
        }
    }

    public bool IsFilterMeseCorrenteActive
    {
        get => _isFilterMeseCorrenteActive;
        private set
        {
            if (SetProperty(ref _isFilterMeseCorrenteActive, value))
            {
                NotifyPropertyChanged(nameof(MeseCorrenteButtonLabel));
            }
        }
    }

    public bool IsFilterSettimanaActive
    {
        get => _isFilterSettimanaActive;
        private set => SetProperty(ref _isFilterSettimanaActive, value);
    }

    public string OggiButtonLabel => "Oggi";

    public string SettimanaButtonLabel => "Settimana";

    public string MeseCorrenteButtonLabel => "Mese";

    public string PresetButtonLabel => CurrentFilterModeLabel;

    public UIDocumentPresentationMode UIDocumentPresentationMode
    {
        get => _uiDocumentPresentationMode;
        private set
        {
            if (SetProperty(ref _uiDocumentPresentationMode, value))
            {
                NotifyPropertyChanged(nameof(UIDocumentPresentationModeLabel));
                NotifyPropertyChanged(nameof(IsUIDefaultPresentationMode));
                NotifyPropertyChanged(nameof(IsUIOnlyCortesiaPresentationMode));
                NotifyPropertyChanged(nameof(IsUICompleteWithCortesiaPresentationMode));
                NotifyPropertyChanged(nameof(IsUICortesiaColumnVisible));
                NotifyPropertyChanged(nameof(IsUICortesiaSummaryVisible));
                NotifyPropertyChanged(nameof(IsUICartaSummaryVisible));
                RefreshUIDocumentPresentation();
            }
        }
    }

    public string UIDocumentPresentationModeLabel => UIDocumentPresentationMode switch
    {
        UIDocumentPresentationMode.OnlyCortesia => "Solo cortesia",
        UIDocumentPresentationMode.CompleteWithCortesia => "Completa con cortesia",
        _ => "Solo scontrinati"
    };

    public bool IsUIDefaultPresentationMode => UIDocumentPresentationMode == UIDocumentPresentationMode.Default;

    public bool IsUIOnlyCortesiaPresentationMode => UIDocumentPresentationMode == UIDocumentPresentationMode.OnlyCortesia;

    public bool IsUICompleteWithCortesiaPresentationMode => UIDocumentPresentationMode == UIDocumentPresentationMode.CompleteWithCortesia;

    public bool IsUICortesiaColumnVisible => UIDocumentPresentationMode == UIDocumentPresentationMode.CompleteWithCortesia;

    public bool IsUICortesiaSummaryVisible => UIDocumentPresentationMode != UIDocumentPresentationMode.Default;

    public bool IsUICartaSummaryVisible => UIDocumentPresentationMode != UIDocumentPresentationMode.OnlyCortesia;

    public DocumentListFilterMode CurrentFilterMode
    {
        get => _currentFilterMode;
        private set
        {
            if (SetProperty(ref _currentFilterMode, value))
            {
                NotifyPropertyChanged(nameof(IsCompletaFilterActive));
                NotifyPropertyChanged(nameof(IsSoloScontrinatiFilterActive));
                NotifyPropertyChanged(nameof(IsSoloCortesiaFilterActive));
                NotifyPropertyChanged(nameof(CurrentFilterModeLabel));
                NotifyPropertyChanged(nameof(TotalsPanelSummary));
                RefreshFilter();
            }
        }
    }

    public bool IsCompletaFilterActive => CurrentFilterMode == DocumentListFilterMode.Completa;

    public bool IsSoloScontrinatiFilterActive => CurrentFilterMode == DocumentListFilterMode.SoloScontrinati;

    public bool IsSoloCortesiaFilterActive => CurrentFilterMode == DocumentListFilterMode.SoloCortesia;

    public string CurrentFilterModeLabel => CurrentFilterMode switch
    {
        DocumentListFilterMode.SoloScontrinati => "Solo scontrinati",
        DocumentListFilterMode.SoloCortesia => "Solo cortesia",
        _ => "Completa"
    };

    public double ListPanelWidth
    {
        get => _listPanelWidth;
        set => SetProperty(ref _listPanelWidth, value);
    }

    public double DetailPanelWidth
    {
        get => _detailPanelWidth;
        set => SetProperty(ref _detailPanelWidth, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public int FilteredCount
    {
        get => _filteredCount;
        private set
        {
            if (SetProperty(ref _filteredCount, value))
            {
                NotifyPropertyChanged(nameof(TotalsPanelSummary));
            }
        }
    }

    public int FilteredOfficialCount
    {
        get => _filteredOfficialCount;
        private set
        {
            if (SetProperty(ref _filteredOfficialCount, value))
            {
                NotifyPropertyChanged(nameof(TotalsPanelSummary));
            }
        }
    }

    public int UIFilteredCount
    {
        get => _uiFilteredCount;
        private set => SetProperty(ref _uiFilteredCount, value);
    }

    public int UIFilteredOfficialCount
    {
        get => _uiFilteredOfficialCount;
        private set => SetProperty(ref _uiFilteredOfficialCount, value);
    }

    public decimal FilteredTotale
    {
        get => _filteredTotale;
        private set => SetProperty(ref _filteredTotale, value);
    }

    public decimal FilteredContanti
    {
        get => _filteredContanti;
        private set => SetProperty(ref _filteredContanti, value);
    }

    public decimal FilteredCarta
    {
        get => _filteredCarta;
        private set => SetProperty(ref _filteredCarta, value);
    }

    public decimal FilteredWeb
    {
        get => _filteredWeb;
        private set => SetProperty(ref _filteredWeb, value);
    }

    public decimal FilteredBuoni
    {
        get => _filteredBuoni;
        private set => SetProperty(ref _filteredBuoni, value);
    }

    public decimal FilteredSospeso
    {
        get => _filteredSospeso;
        private set => SetProperty(ref _filteredSospeso, value);
    }

    public decimal FilteredResiduoPagamento
    {
        get => _filteredResiduoPagamento;
        private set => SetProperty(ref _filteredResiduoPagamento, value);
    }

    public decimal FilteredDaFiscalizzare
    {
        get => _filteredDaFiscalizzare;
        private set => SetProperty(ref _filteredDaFiscalizzare, value);
    }

    public decimal FilteredTotaleContantiCartaScontrini
    {
        get => _filteredTotaleContantiCartaScontrini;
        private set => SetProperty(ref _filteredTotaleContantiCartaScontrini, value);
    }

    public decimal FilteredTotaleContantiCortesia
    {
        get => _filteredTotaleContantiCortesia;
        private set => SetProperty(ref _filteredTotaleContantiCortesia, value);
    }

    public decimal FilteredTotaleSospesoSeparato
    {
        get => _filteredTotaleSospesoSeparato;
        private set => SetProperty(ref _filteredTotaleSospesoSeparato, value);
    }

    public decimal UIFilteredTotale
    {
        get => _uiFilteredTotale;
        private set => SetProperty(ref _uiFilteredTotale, value);
    }

    public decimal UIFilteredContanti
    {
        get => _uiFilteredContanti;
        private set => SetProperty(ref _uiFilteredContanti, value);
    }

    public decimal UIFilteredCarta
    {
        get => _uiFilteredCarta;
        private set => SetProperty(ref _uiFilteredCarta, value);
    }

    public decimal UIFilteredBuoni
    {
        get => _uiFilteredBuoni;
        private set => SetProperty(ref _uiFilteredBuoni, value);
    }

    public decimal UIFilteredSospeso
    {
        get => _uiFilteredSospeso;
        private set => SetProperty(ref _uiFilteredSospeso, value);
    }

    public decimal UIFilteredCortesia
    {
        get => _uiFilteredCortesia;
        private set => SetProperty(ref _uiFilteredCortesia, value);
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand FilterMeseCorrenteCommand { get; }

    public RelayCommand FilterSettimanaCommand { get; }

    public RelayCommand ToggleUnscontrinatiModeCommand { get; }

    public RelayCommand ToggleIncludeLocalDocumentsCommand { get; }

    public RelayCommand ToggleDetailCommand { get; }

    public RelayCommand FilterOggiCommand { get; }

    public RelayCommand NewBancoDocumentCommand { get; }

    public RelayCommand OpenDocumentInBancoCommand { get; }

    public RelayCommand<string> SetDocumentFilterModeCommand { get; }

    public int SelectedDocumentsCount
    {
        get => _selectedDocumentsCount;
        private set
        {
            if (SetProperty(ref _selectedDocumentsCount, value))
            {
                NotifyPropertyChanged(nameof(SelectionSummary));
                NotifyPropertyChanged(nameof(CanDeleteSelectedLocalDocuments));
            }
        }
    }

    public int SelectedDeletableDocumentsCount
    {
        get => _selectedDeletableDocumentsCount;
        private set
        {
            if (SetProperty(ref _selectedDeletableDocumentsCount, value))
            {
                NotifyPropertyChanged(nameof(SelectionSummary));
                NotifyPropertyChanged(nameof(CanDeleteSelectedLocalDocuments));
            }
        }
    }

    public bool CanDeleteSelectedLocalDocuments => SelectedDeletableDocumentsCount > 0 && !_isDeletingSelectedLocalDocuments;

    public string SelectionSummary => SelectedDocumentsCount == 0
        ? string.Empty
        : SelectedDocumentsCount == SelectedDeletableDocumentsCount
            ? $"{SelectedDeletableDocumentsCount} documenti eliminabili selezionati."
            : $"{SelectedDocumentsCount} righe selezionate, di cui {SelectedDeletableDocumentsCount} eliminabili.";

    public string OpenDocumentInBancoLabel => SelectedDocument?.OpenDocumentActionLabel ?? "Apri nel Banco";

    public string OpenDocumentInBancoTooltip => SelectedDocument?.OpenDocumentActionTooltip ?? "Apre il documento selezionato nel Banco.";

    public string TotalsPanelSummary => FilteredOfficialCount == 0
        ? "Riepilogo economico vuoto: il filtro corrente non contiene documenti ufficiali legacy."
        : FilteredCount == FilteredOfficialCount
            ? $"Riepilogo economico su {FilteredOfficialCount} documenti ufficiali del filtro {CurrentFilterModeLabel.ToLowerInvariant()}."
            : $"Riepilogo economico su {FilteredOfficialCount} documenti ufficiali. Le restanti {FilteredCount - FilteredOfficialCount} righe visibili sono locali e non entrano nei totali di vendita chiusa.";

    public string UITotalsPanelSummary => UIFilteredOfficialCount == 0
        ? "Riepilogo economico vuoto: nessun documento ufficiale coerente con la modalita` attiva."
        : UIFilteredCount == UIFilteredOfficialCount
            ? $"Riepilogo economico su {UIFilteredOfficialCount} documenti ufficiali in modalita` {UIDocumentPresentationModeLabel.ToLowerInvariant()}."
            : $"Riepilogo economico su {UIFilteredOfficialCount} documenti ufficiali. Le restanti {UIFilteredCount - UIFilteredOfficialCount} righe visibili sono supporti tecnici.";

    public string UIStatusMessage => _uiStatusMessage;

    public DocumentGridFooterViewModel FooterRow => new()
    {
        Status = FilteredOfficialCount.ToString("N0"),
        Totale = FilteredTotale.ToString("N2"),
        PagContanti = FilteredContanti.ToString("N2"),
        PagCarta = FilteredCarta.ToString("N2"),
        PagWeb = FilteredWeb.ToString("N2"),
        PagBuoni = FilteredBuoni.ToString("N2"),
        PagSospeso = FilteredSospeso.ToString("N2"),
        ResiduoPagamento = FilteredResiduoPagamento.ToString("N2"),
        DaFiscalizzare = FilteredDaFiscalizzare.ToString("N2")
    };

    public IReadOnlyList<DocumentGridFooterViewModel> FooterRows => [FooterRow];

    public DocumentGridFooterViewModel UIFooterRow => new()
    {
        Status = UIFilteredOfficialCount.ToString("N0"),
        Totale = UIFilteredTotale.ToString("N2"),
        PagContanti = UIFilteredContanti.ToString("N2"),
        PagCarta = IsUICartaSummaryVisible ? UIFilteredCarta.ToString("N2") : string.Empty,
        Cortesia = IsUICortesiaSummaryVisible ? UIFilteredCortesia.ToString("N2") : string.Empty,
        PagBuoni = UIFilteredBuoni.ToString("N2"),
        PagSospeso = UIFilteredSospeso.ToString("N2")
    };

    public IReadOnlyList<DocumentGridFooterViewModel> UIFooterRows => [UIFooterRow];

    public IReadOnlyList<GridColumnDefinition> ColumnDefinitions => DocumentColumnDefinitions.All;

    public bool IsStatusColumnVisible => GetEffectiveColumnVisibility("Status");
    public bool IsOidColumnVisible => GetEffectiveColumnVisibility("Oid");
    public bool IsDocumentoColumnVisible => GetEffectiveColumnVisibility("Documento");
    public bool IsDataColumnVisible => GetEffectiveColumnVisibility("Data");
    public bool IsOperatoreColumnVisible => GetEffectiveColumnVisibility("Operatore");
    public bool IsClienteColumnVisible => GetEffectiveColumnVisibility("Cliente");
    public bool IsTotaleColumnVisible => GetEffectiveColumnVisibility("Totale");
    public bool IsPagContantiColumnVisible => GetEffectiveColumnVisibility("PagContanti");
    public bool IsPagCartaColumnVisible => GetEffectiveColumnVisibility("PagCarta");
    public bool IsPagWebColumnVisible => GetEffectiveColumnVisibility("PagWeb");
    public bool IsPagBuoniColumnVisible => GetEffectiveColumnVisibility("PagBuoni");
    public bool IsPagSospesoColumnVisible => GetEffectiveColumnVisibility("PagSospeso");
    public bool IsResiduoColumnVisible => GetEffectiveColumnVisibility("ResiduoPagamento");
    public bool IsDaFiscalizzareColumnVisible => GetEffectiveColumnVisibility("DaFiscalizzare");
    public bool IsOrigineColumnVisible => GetEffectiveColumnVisibility("Origine");
    public bool IsStatoColumnVisible => GetEffectiveColumnVisibility("StatoDocumento");
    public bool IsScontrinoColumnVisible => GetEffectiveColumnVisibility("Scontrino");

    public async Task<GridLayoutSettings> GetGridLayoutAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = GridLayoutMigration.GetOrCreateDocumentListLayout(settings, ColumnDefinitions);
        SyncColumnsFromLayout(layout);
        return layout;
    }

    public async Task SaveGridLayoutAsync(GridLayoutSettings layout)
    {
        var settings = await _configurationService.LoadAsync();
        settings.GridLayouts[GridLayoutMigration.DocumentListGridId] = layout;
        settings.DocumentListIncludeLocalDocuments = IncludeLocalDocuments;
        settings.DocumentListUnscontrinatiExpandedMode = false;
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

    public string GetFooterLabel(string key)
    {
        return key switch
        {
            "Status" => FilteredOfficialCount.ToString("N0"),
            "Totale" => FilteredTotale.ToString("N2"),
            "PagContanti" => FilteredContanti.ToString("N2"),
            "PagCarta" => FilteredCarta.ToString("N2"),
            "PagWeb" => FilteredWeb.ToString("N2"),
            "PagBuoni" => FilteredBuoni.ToString("N2"),
            "PagSospeso" => FilteredSospeso.ToString("N2"),
            "ResiduoPagamento" => FilteredResiduoPagamento.ToString("N2"),
            "DaFiscalizzare" => FilteredDaFiscalizzare.ToString("N2"),
            _ => string.Empty
        };
    }

    public async Task SaveSplitterProportionsAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = GridLayoutMigration.GetOrCreateDocumentListLayout(settings, ColumnDefinitions);
        layout.SplitterWidths["listPanelProportion"] = ListPanelWidth;
        layout.SplitterWidths["detailPanelProportion"] = DetailPanelWidth;
        settings.GridLayouts[GridLayoutMigration.DocumentListGridId] = layout;
        await _configurationService.SaveAsync(settings);
    }

    public void ShowDocumentsSection()
    {
        SelectedSection = DocumentListSection.Documents;
    }

    public void ShowReorderListSection()
    {
        SelectedSection = DocumentListSection.ReorderList;
    }

    public void ResetUIDocumentPresentationMode()
    {
        UIDocumentPresentationMode = UIDocumentPresentationMode.Default;
    }

    public void CycleUIDocumentPresentationMode()
    {
        UIDocumentPresentationMode = UIDocumentPresentationMode switch
        {
            UIDocumentPresentationMode.Default => UIDocumentPresentationMode.OnlyCortesia,
            UIDocumentPresentationMode.OnlyCortesia => UIDocumentPresentationMode.CompleteWithCortesia,
            _ => UIDocumentPresentationMode.OnlyCortesia
        };
    }

    private async Task InitializeAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = GridLayoutMigration.GetOrCreateDocumentListLayout(settings, ColumnDefinitions);
        SyncColumnsFromLayout(layout);
        IncludeLocalDocuments = layout.Flags.TryGetValue("includeLocalDocuments", out var includeLocal)
            ? includeLocal
            : settings.DocumentListIncludeLocalDocuments;
        CurrentFilterMode = DocumentListFilterMode.Completa;

        // Ripristina proporzioni splitter salvate
        if (layout.SplitterWidths.TryGetValue("listPanelProportion", out var listWidth))
        {
            ListPanelWidth = listWidth;
        }

        if (layout.SplitterWidths.TryGetValue("detailPanelProportion", out var detailWidth))
        {
            DetailPanelWidth = detailWidth;
        }

        await _configurationService.SaveAsync(settings);
        await RefreshAsync();
    }

    // Accetta una selezione preferita esplicita che viene usata come snapshot per la riconciliazione.
    // Serve per evitare la double-reconciliation nel flusso delete per-riga.
    public async Task RefreshAsync(DocumentGridRowViewModel? preferredSelection = null)
    {
        await _refreshSemaphore.WaitAsync();
        try
        {
            _logService.Info(nameof(DocumentListViewModel), "Refresh lista documenti avviato.");
            await LoadAsyncCore(preferredSelection);
            _logService.Info(nameof(DocumentListViewModel), $"Refresh lista documenti completato. Visibili={FilteredCount}, Ufficiali={FilteredOfficialCount}.");
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    private async Task LoadAsyncCore(DocumentGridRowViewModel? preferredSelection = null)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Caricamento lista documenti unificata...";
            // Se viene fornita una selezione preferita (es. riga vicina dopo delete),
            // la usa come snapshot invece del documento correntemente selezionato.
            var selectedDocumentSnapshot = preferredSelection ?? SelectedDocument;

            var gestionali = await _documentReadService.GetRecentBancoDocumentsAsync(250);
            var locali = await _localDocumentRepository.GetAllAsync();
            var localiPerDocumentoGestionale = locali
                .Where(document => document.DocumentoGestionaleOid.HasValue)
                .GroupBy(document => document.DocumentoGestionaleOid!.Value)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.DataUltimaModifica).First());

            Documents.Clear();

            foreach (var document in gestionali)
            {
                localiPerDocumentoGestionale.TryGetValue(document.Oid, out var localMetadata);
                Documents.Add(DocumentGridRowViewModel.FromGestionale(document, localMetadata));
            }

            var gestionaliOids = gestionali
                .Select(document => document.Oid)
                .ToHashSet();

            foreach (var document in locali.Where(document =>
                         document.DocumentoGestionaleOid.HasValue &&
                         !gestionaliOids.Contains(document.DocumentoGestionaleOid.Value)))
            {
                Documents.Add(DocumentGridRowViewModel.FromPublishedLocalFallback(document));
            }

            foreach (var document in locali.Where(document => !document.DocumentoGestionaleOid.HasValue))
            {
                Documents.Add(DocumentGridRowViewModel.FromLocal(document));
            }

            RefreshFilter(selectedDocumentSnapshot);
        }
        catch (Exception ex)
        {
            Documents.Clear();
            SelectedDocument = null;
            SelectedDocumentDetail = null;
            SelectedDocumentRows.Clear();
            UpdateSelectedDocuments([]);
            StatusMessage = $"Errore lettura documenti: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadDetailAsync(DocumentGridRowViewModel? row)
    {
        var detailLoadVersion = Interlocked.Increment(ref _detailLoadVersion);
        SelectedDocumentRows.Clear();
        if (row is null)
        {
            SelectedDocumentDetail = null;
            return;
        }

        if (!row.IsLocal && row.GestionaleOid.HasValue)
        {
            var detail = await _documentReadService.GetDocumentDetailAsync(row.GestionaleOid.Value);
            if (detail is null)
            {
                if (detailLoadVersion == _detailLoadVersion)
                {
                    SelectedDocumentDetail = null;
                }
                return;
            }

            var detailViewModel = new DocumentGridDetailViewModel
            {
                DocumentoLabel = detail.DocumentoLabel,
                SourceLabel = "Legacy",
                Cliente = detail.SoggettoNominativo,
                Operatore = detail.Operatore,
                StatoDocumento = row.StatoDocumento,
                ScontrinoLabel = row.ScontrinoLabel,
                Totale = detail.TotaleDocumento,
                TotalePagato = row.TotalePagato,
                ResiduoPagamento = row.ResiduoPagamento,
                DaFiscalizzare = row.DaFiscalizzare,
                IsLocal = false
            };

            if (detailLoadVersion != _detailLoadVersion || !ReferenceEquals(SelectedDocument, row))
            {
                return;
            }

            SelectedDocumentDetail = detailViewModel;
            SelectedDocumentRows.Clear();

            foreach (var item in detail.Righe)
            {
                SelectedDocumentRows.Add(new DocumentGridDetailRowViewModel
                {
                    OrdineRiga = item.OrdineRiga,
                    CodiceArticolo = item.CodiceArticolo,
                    Descrizione = item.Descrizione,
                    Quantita = item.Quantita,
                    PrezzoUnitario = item.PrezzoUnitario,
                    ScontoPercentuale = item.ScontoPercentuale,
                    ImportoRiga = item.ImportoRiga
                });
            }
        }
        else if (row.IsLocal && row.LocalDocumentId.HasValue)
        {
            var detail = await _localDocumentRepository.GetByIdAsync(row.LocalDocumentId.Value);
            if (detail is null)
            {
                if (detailLoadVersion == _detailLoadVersion)
                {
                    SelectedDocumentDetail = null;
                }
                return;
            }

            var detailViewModel = new DocumentGridDetailViewModel
            {
                DocumentoLabel = FormatLocalDocumentLabel(detail),
                SourceLabel = "Banco",
                Cliente = detail.Cliente,
                Operatore = detail.Operatore,
                StatoDocumento = row.StatoDocumento,
                ScontrinoLabel = row.ScontrinoLabel,
                Totale = detail.TotaleDocumento,
                TotalePagato = row.TotalePagato,
                ResiduoPagamento = row.ResiduoPagamento,
                DaFiscalizzare = row.DaFiscalizzare,
                IsLocal = true
            };

            if (detailLoadVersion != _detailLoadVersion || !ReferenceEquals(SelectedDocument, row))
            {
                return;
            }

            SelectedDocumentDetail = detailViewModel;
            SelectedDocumentRows.Clear();

            foreach (var item in detail.Righe.OrderBy(r => r.OrdineRiga))
            {
                SelectedDocumentRows.Add(new DocumentGridDetailRowViewModel
                {
                    OrdineRiga = item.OrdineRiga,
                    CodiceArticolo = item.CodiceArticolo,
                    Descrizione = item.Descrizione,
                    Quantita = item.Quantita,
                    PrezzoUnitario = item.PrezzoUnitario,
                    ScontoPercentuale = item.ScontoPercentuale,
                    AliquotaIva = item.AliquotaIva,
                    ImportoRiga = item.ImportoRiga
                });
            }
        }
    }

    private static string FormatLocalDocumentLabel(DocumentoLocale detail)
    {
        if (detail.NumeroDocumentoGestionale.HasValue && detail.AnnoDocumentoGestionale.HasValue)
        {
            return $"{detail.NumeroDocumentoGestionale}/{detail.AnnoDocumentoGestionale}";
        }

        return "Documento Banco";
    }

    private void OpenSelectedDocumentInBanco()
    {
        if (SelectedDocument is null)
        {
            return;
        }

        _logService.Info(nameof(DocumentListViewModel), $"Richiesta apertura nel Banco del documento {SelectedDocument.NumeroDocumento}.");

        if (SelectedDocument.LocalDocumentId.HasValue)
        {
            OpenLocalDocumentInBancoRequested?.Invoke(SelectedDocument.LocalDocumentId.Value);
            return;
        }

        if (SelectedDocument.GestionaleOid.HasValue)
        {
            OpenDocumentInBancoRequested?.Invoke(SelectedDocument.GestionaleOid.Value);
        }
    }

    private void ToggleUnscontrinatiMode()
    {
        CurrentFilterMode = CurrentFilterMode == DocumentListFilterMode.SoloCortesia
            ? DocumentListFilterMode.Completa
            : DocumentListFilterMode.SoloCortesia;
        IsDetailExpanded = true;
    }

    private void ToggleFilterOggi()
    {
        if (IsFilterOggiActive)
        {
            // Disattiva: ripristina le date precedenti
            DataDa = _savedDataDa;
            DataA = _savedDataA;
            IsFilterOggiActive = false;
            RefreshFilter();
        }
        else
        {
            // Attiva: salva date correnti e imposta oggi
            _savedDataDa = DataDa;
            _savedDataA = DataA;
            IsFilterMeseCorrenteActive = false;
            IsFilterSettimanaActive = false;
            IsFilterOggiActive = true;
            DataDa = DateTime.Today;
            DataA = DateTime.Today;
            RefreshFilter();
        }
    }

    private static DateTime GetCurrentWeekStart()
    {
        var today = DateTime.Today;
        var diff = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(-diff);
    }

    private static DateTime GetCurrentWeekEnd()
    {
        return GetCurrentWeekStart().AddDays(6);
    }

    private void ToggleFilterSettimana()
    {
        if (IsFilterSettimanaActive)
        {
            DataDa = _savedDataDa;
            DataA = _savedDataA;
            IsFilterSettimanaActive = false;
            RefreshFilter();
            return;
        }

        _savedDataDa = DataDa;
        _savedDataA = DataA;
        IsFilterOggiActive = false;
        IsFilterMeseCorrenteActive = false;
        IsFilterSettimanaActive = true;
        DataDa = GetCurrentWeekStart();
        DataA = GetCurrentWeekEnd();
        RefreshFilter();
    }

    private static DateTime GetCurrentMonthStart()
    {
        var today = DateTime.Today;
        return new DateTime(today.Year, today.Month, 1);
    }

    private static DateTime GetCurrentMonthEnd()
    {
        return GetCurrentMonthStart().AddMonths(1).AddDays(-1);
    }

    private void ApplyCurrentMonthFilter()
    {
        _savedDataDa = DataDa;
        _savedDataA = DataA;
        IsFilterOggiActive = false;
        IsFilterSettimanaActive = false;
        IsFilterMeseCorrenteActive = true;
        DataDa = GetCurrentMonthStart();
        DataA = GetCurrentMonthEnd();
        RefreshFilter();
    }

    private async Task PersistFlagsAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = GridLayoutMigration.GetOrCreateDocumentListLayout(settings, ColumnDefinitions);
        layout.Flags["includeLocalDocuments"] = IncludeLocalDocuments;
        layout.Flags["unscontrinatiExpandedMode"] = false;
        settings.DocumentListIncludeLocalDocuments = IncludeLocalDocuments;
        settings.DocumentListUnscontrinatiExpandedMode = false;
        await _configurationService.SaveAsync(settings);
    }

    private void SetDocumentFilterMode(string? rawMode)
    {
        CurrentFilterMode = rawMode?.Trim().ToLowerInvariant() switch
        {
            "soloscontrinati" => DocumentListFilterMode.SoloScontrinati,
            "solocortesia" => DocumentListFilterMode.SoloCortesia,
            _ => DocumentListFilterMode.Completa
        };
    }

    private void RefreshFilter(DocumentGridRowViewModel? preferredSelection = null)
    {
        DocumentsView.Refresh();
        var filtered = GetFilteredDocuments();
        UpdateFilteredTotals(filtered);
        RefreshUIDocumentPresentation();
        ReconcileSelectedDocument(filtered, preferredSelection);

        var totalSourceCount = Documents.Count(row => IncludeLocalDocuments || !row.IsLocal);
        if (totalSourceCount == 0)
        {
            StatusMessage = IncludeLocalDocuments
                ? "Nessun documento disponibile tra DB gestionale e supporti tecnici."
                : "Nessun documento gestionale trovato nel DB.";
            return;
        }

        StatusMessage = FilteredCount == 0
            ? "Nessun documento visibile con i filtri correnti."
            : $"Documenti visibili: {FilteredCount} su {totalSourceCount}.";
    }

    private void RefreshUIDocumentPresentation()
    {
        UIDocumentsView.Refresh();
        var filtered = GetUIFilteredDocuments();
        UpdateUIFilteredTotals(filtered);
        ReconcileSelectedDocument(filtered, SelectedDocument);
        NotifyPropertyChanged(nameof(UIFooterRow));
        NotifyPropertyChanged(nameof(UIFooterRows));
        NotifyPropertyChanged(nameof(UITotalsPanelSummary));
        NotifyPropertyChanged(nameof(UIStatusMessage));
    }

    public void UpdateSelectedDocuments(IEnumerable<DocumentGridRowViewModel> selectedDocuments)
    {
        _selectedDocuments = selectedDocuments
            .Distinct()
            .ToList();

        SelectedDocumentsCount = _selectedDocuments.Count;
        SelectedDeletableDocumentsCount = _selectedDocuments.Count(item => item.CanDeleteFromList);
    }

    public async Task DeleteSelectedLocalDocumentsAsync()
    {
        var deleteTargets = BuildDeleteTargets(_selectedDocuments);
        if (deleteTargets.LocalDocumentIds.Count == 0 && deleteTargets.GestionaleOids.Count == 0)
        {
            return;
        }

        var fallbackSelection = ResolvePostDeleteSelection(deleteTargets.LocalDocumentIds, deleteTargets.GestionaleOids);
        var documentsToDeleteCount = deleteTargets.LocalDocumentIds.Count + deleteTargets.GestionaleOids.Count;

        try
        {
            _isDeletingSelectedLocalDocuments = true;
            NotifyPropertyChanged(nameof(CanDeleteSelectedLocalDocuments));
            StatusMessage = documentsToDeleteCount == 1
                ? "Cancellazione documento in corso..."
                : $"Cancellazione di {documentsToDeleteCount} documenti in corso...";
            _logService.Info(nameof(DocumentListViewModel), $"Cancellazione multipla documenti avviata. Target={documentsToDeleteCount}.");

            foreach (var localDocumentId in deleteTargets.LocalDocumentIds)
            {
                await _localDocumentRepository.DeleteAsync(localDocumentId);
            }

            foreach (var gestionaleOid in deleteTargets.GestionaleOids)
            {
                await _documentDeleteService.DeleteNonFiscalizedDocumentAsync(gestionaleOid);
                var localMetadata = await _localDocumentRepository.GetByDocumentoGestionaleOidAsync(gestionaleOid);
                if (localMetadata is not null && !deleteTargets.LocalDocumentIds.Contains(localMetadata.Id))
                {
                    await _localDocumentRepository.DeleteAsync(localMetadata.Id);
                }
            }

            await RefreshAsync(fallbackSelection);
            NotifyDeleteCommandStateChanged();
            StatusMessage = documentsToDeleteCount == 1
                ? "Documento eliminato."
                : $"{documentsToDeleteCount} documenti eliminati.";
            _logService.Info(nameof(DocumentListViewModel), $"Cancellazione multipla completata. Target={documentsToDeleteCount}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante la cancellazione dei documenti: {ex.Message}";
            _logService.Error(nameof(DocumentListViewModel), "Errore durante la cancellazione multipla documenti.", ex);
        }
        finally
        {
            _isDeletingSelectedLocalDocuments = false;
            NotifyDeleteCommandStateChanged();
        }
    }

    private bool MatchesCommonFilters(DocumentGridRowViewModel row)
    {
        if (!IncludeLocalDocuments && row.IsLocal)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(NominativoSearchText) &&
            !row.Cliente.Contains(NominativoSearchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (DataDa.HasValue && row.Data.Date < DataDa.Value.Date)
        {
            return false;
        }

        if (DataA.HasValue && row.Data.Date > DataA.Value.Date)
        {
            return false;
        }

        return true;
    }

    private bool FilterDocument(object obj)
    {
        if (obj is not DocumentGridRowViewModel row || !MatchesCommonFilters(row))
        {
            return false;
        }

        if (CurrentFilterMode == DocumentListFilterMode.SoloScontrinati && !row.IsScontrinato)
        {
            return false;
        }

        if (CurrentFilterMode == DocumentListFilterMode.SoloCortesia && !row.IsNonScontrinato)
        {
            return false;
        }

        return true;
    }

    private bool FilterUIDocument(object obj)
    {
        if (obj is not DocumentGridRowViewModel row || !MatchesCommonFilters(row))
        {
            return false;
        }

        return UIDocumentPresentationMode switch
        {
            UIDocumentPresentationMode.OnlyCortesia => row.IsCortesia,
            UIDocumentPresentationMode.CompleteWithCortesia => row.IsScontrinato || row.IsCortesia,
            _ => row.IsScontrinato
        };
    }

    private List<DocumentGridRowViewModel> GetFilteredDocuments()
    {
        return DocumentsView.Cast<DocumentGridRowViewModel>().ToList();
    }

    private List<DocumentGridRowViewModel> GetUIFilteredDocuments()
    {
        return UIDocumentsView.Cast<DocumentGridRowViewModel>().ToList();
    }

    private void UpdateFilteredTotals(IReadOnlyCollection<DocumentGridRowViewModel> filtered)
    {
        var totals = CalculateFilteredTotals(filtered);
        FilteredCount = totals.FilteredCount;
        FilteredOfficialCount = totals.FilteredOfficialCount;
        FilteredTotale = totals.Totale;
        FilteredContanti = totals.Contanti;
        FilteredCarta = totals.Carta;
        FilteredWeb = totals.Web;
        FilteredBuoni = totals.Buoni;
        FilteredSospeso = totals.Sospeso;
        FilteredResiduoPagamento = totals.ResiduoPagamento;
        FilteredDaFiscalizzare = totals.DaFiscalizzare;
        FilteredTotaleContantiCartaScontrini = totals.TotaleContantiCartaScontrinati;
        FilteredTotaleContantiCortesia = totals.TotaleContantiCortesiaONonScontrinati;
        FilteredTotaleSospesoSeparato = totals.TotaleSospesoSeparato;
        NotifyPropertyChanged(nameof(FooterRow));
        NotifyPropertyChanged(nameof(FooterRows));
    }

    private static FilteredDocumentTotals CalculateFilteredTotals(IReadOnlyCollection<DocumentGridRowViewModel> filtered)
    {
        var filteredOfficial = filtered
            .Where(item => !item.IsLocal && item.GestionaleOid.HasValue)
            .ToList();

        return new FilteredDocumentTotals(
            filtered.Count,
            filteredOfficial.Count,
            filteredOfficial.Sum(item => item.Totale),
            filteredOfficial.Sum(item => item.PagContanti),
            filteredOfficial.Sum(item => item.PagCarta),
            filteredOfficial.Sum(item => item.PagWeb),
            filteredOfficial.Sum(item => item.PagBuoni),
            filteredOfficial.Sum(item => item.PagSospeso),
            filteredOfficial.Sum(item => item.ResiduoPagamento),
            filteredOfficial.Sum(item => item.DaFiscalizzare),
            filteredOfficial.Sum(item => item.TotaleContantiCartaScontrinato),
            filteredOfficial.Sum(item => item.TotaleContantiCortesiaONonScontrinato),
            filteredOfficial.Sum(item => item.PagSospeso));
    }

    private void UpdateUIFilteredTotals(IReadOnlyCollection<DocumentGridRowViewModel> filtered)
    {
        var totals = CalculateFilteredTotals(filtered);
        UIFilteredCount = totals.FilteredCount;
        UIFilteredOfficialCount = totals.FilteredOfficialCount;
        UIFilteredTotale = totals.Totale;
        UIFilteredContanti = totals.Contanti;
        UIFilteredCarta = totals.Carta;
        UIFilteredBuoni = totals.Buoni;
        UIFilteredSospeso = totals.Sospeso;
        UIFilteredCortesia = filtered
            .Where(item => !item.IsLocal && item.GestionaleOid.HasValue && item.IsCortesia)
            .Sum(item => item.TotaleContantiCortesiaONonScontrinato);

        var totalSourceCount = Documents.Count(row => IncludeLocalDocuments || !row.IsLocal);
        _uiStatusMessage = totalSourceCount == 0
            ? "Nessun documento disponibile."
            : UIFilteredCount == 0
                ? "Nessun documento visibile nella modalita` attiva."
                : $"Documenti visibili: {UIFilteredCount} su {totalSourceCount}.";
    }

    public bool CanDeleteDocumentRow(DocumentGridRowViewModel? row) => row?.CanDeleteFromList == true && !_isDeletingSelectedLocalDocuments;

    // Il fallbackSelection è la riga vicina calcolata dal code-behind prima del delete.
    // Viene passata a RefreshAsync come snapshot preferito per la riconciliazione,
    // evitando la double-reconciliation e la race tra due LoadDetailAsync.
    public async Task DeleteDocumentRowAsync(DocumentGridRowViewModel row)
    {
        if (!CanDeleteDocumentRow(row))
        {
            return;
        }

        var deleteTargets = BuildDeleteTargets([row]);
        var fallbackSelection = ResolvePostDeleteSelection(deleteTargets.LocalDocumentIds, deleteTargets.GestionaleOids);

        try
        {
            _isDeletingSelectedLocalDocuments = true;
            NotifyDeleteCommandStateChanged();
            StatusMessage = "Cancellazione documento in corso...";
            _logService.Info(nameof(DocumentListViewModel), $"Cancellazione riga documento avviata: {row.NumeroDocumento}.");
            foreach (var localDocumentId in deleteTargets.LocalDocumentIds)
            {
                await _localDocumentRepository.DeleteAsync(localDocumentId);
            }

            foreach (var gestionaleOid in deleteTargets.GestionaleOids)
            {
                await _documentDeleteService.DeleteNonFiscalizedDocumentAsync(gestionaleOid);
                var localMetadata = await _localDocumentRepository.GetByDocumentoGestionaleOidAsync(gestionaleOid);
                if (localMetadata is not null && !deleteTargets.LocalDocumentIds.Contains(localMetadata.Id))
                {
                    await _localDocumentRepository.DeleteAsync(localMetadata.Id);
                }
            }
            // Usa fallbackSelection come snapshot: RefreshAsync → LoadAsyncCore → RefreshFilter
            // riconcilia SelectedDocument UNA SOLA VOLTA con la riga vicina corretta.
            await RefreshAsync(fallbackSelection);
            NotifyDeleteCommandStateChanged();
            StatusMessage = "Documento eliminato.";
            _logService.Info(nameof(DocumentListViewModel), $"Cancellazione riga documento completata: {row.NumeroDocumento}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante la cancellazione del documento: {ex.Message}";
            _logService.Error(nameof(DocumentListViewModel), $"Errore durante la cancellazione della riga documento {row.NumeroDocumento}.", ex);
        }
        finally
        {
            _isDeletingSelectedLocalDocuments = false;
            NotifyDeleteCommandStateChanged();
        }
    }

    public void ReconcileViewSelection(DocumentGridRowViewModel? preferredSelection)
    {
        ReconcileSelectedDocument(GetFilteredDocuments(), preferredSelection);
    }

    private DocumentGridRowViewModel? ResolvePostDeleteSelection(
        IReadOnlySet<Guid> deletingLocalDocumentIds,
        IReadOnlySet<int> deletingGestionaleOids)
    {
        var filtered = GetFilteredDocuments();
        if (filtered.Count == 0)
        {
            return null;
        }

        var selectedIndex = filtered.FindIndex(item => AreSameDocument(item, SelectedDocument));
        var survivingRows = filtered
            .Where(item => !WillBeDeleted(item, deletingLocalDocumentIds, deletingGestionaleOids))
            .ToList();

        if (survivingRows.Count == 0)
        {
            return null;
        }

        if (selectedIndex >= 0)
        {
            for (var index = selectedIndex + 1; index < filtered.Count; index++)
            {
                var candidate = filtered[index];
                if (!WillBeDeleted(candidate, deletingLocalDocumentIds, deletingGestionaleOids))
                {
                    return candidate;
                }
            }

            for (var index = selectedIndex - 1; index >= 0; index--)
            {
                var candidate = filtered[index];
                if (!WillBeDeleted(candidate, deletingLocalDocumentIds, deletingGestionaleOids))
                {
                    return candidate;
                }
            }
        }

        return survivingRows.FirstOrDefault();
    }

    private void ReconcileSelectedDocument(
        IReadOnlyList<DocumentGridRowViewModel> filtered,
        DocumentGridRowViewModel? preferredSelection)
    {
        var resolvedSelection = FindMatchingRow(filtered, preferredSelection)
                                ?? FindMatchingRow(filtered, SelectedDocument)
                                ?? filtered.FirstOrDefault();

        if (!ReferenceEquals(SelectedDocument, resolvedSelection))
        {
            SelectedDocument = resolvedSelection;
        }
        else if (resolvedSelection is null)
        {
            SelectedDocumentRows.Clear();
            SelectedDocumentDetail = null;
        }

        UpdateSelectedDocuments(resolvedSelection is null
            ? []
            : [resolvedSelection]);
    }

    private static DocumentGridRowViewModel? FindMatchingRow(
        IReadOnlyList<DocumentGridRowViewModel> filtered,
        DocumentGridRowViewModel? candidate)
    {
        if (candidate is null)
        {
            return null;
        }

        if (candidate.LocalDocumentId.HasValue)
        {
            var localMatch = filtered.FirstOrDefault(item => item.LocalDocumentId == candidate.LocalDocumentId);
            if (localMatch is not null)
            {
                return localMatch;
            }
        }

        if (candidate.GestionaleOid.HasValue)
        {
            return filtered.FirstOrDefault(item =>
                item.GestionaleOid == candidate.GestionaleOid &&
                item.IsLocal == candidate.IsLocal);
        }

        return filtered.FirstOrDefault(item =>
            string.Equals(item.NumeroDocumento, candidate.NumeroDocumento, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Cliente, candidate.Cliente, StringComparison.OrdinalIgnoreCase) &&
            item.IsLocal == candidate.IsLocal);
    }

    private static bool AreSameDocument(DocumentGridRowViewModel left, DocumentGridRowViewModel? right)
    {
        if (right is null)
        {
            return false;
        }

        if (left.LocalDocumentId.HasValue || right.LocalDocumentId.HasValue)
        {
            return left.LocalDocumentId == right.LocalDocumentId;
        }

        if (left.GestionaleOid.HasValue || right.GestionaleOid.HasValue)
        {
            return left.GestionaleOid == right.GestionaleOid && left.IsLocal == right.IsLocal;
        }

        return string.Equals(left.NumeroDocumento, right.NumeroDocumento, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Cliente, right.Cliente, StringComparison.OrdinalIgnoreCase) &&
               left.IsLocal == right.IsLocal;
    }

    private static bool WillBeDeleted(
        DocumentGridRowViewModel candidate,
        IReadOnlySet<Guid> deletingLocalDocumentIds,
        IReadOnlySet<int> deletingGestionaleOids)
    {
        if (candidate.LocalDocumentId.HasValue && deletingLocalDocumentIds.Contains(candidate.LocalDocumentId.Value))
        {
            return true;
        }

        return candidate.GestionaleOid.HasValue && deletingGestionaleOids.Contains(candidate.GestionaleOid.Value);
    }

    private static (HashSet<Guid> LocalDocumentIds, HashSet<int> GestionaleOids) BuildDeleteTargets(IEnumerable<DocumentGridRowViewModel> rows)
    {
        var localDocumentIds = new HashSet<Guid>();
        var gestionaleOids = new HashSet<int>();

        foreach (var row in rows.Where(item => item.CanDeleteFromList))
        {
            if (row.LocalDocumentId.HasValue)
            {
                localDocumentIds.Add(row.LocalDocumentId.Value);
            }

            if (row.GestionaleOid.HasValue)
            {
                gestionaleOids.Add(row.GestionaleOid.Value);
            }
        }

        return (localDocumentIds, gestionaleOids);
    }

    private void NotifyDeleteCommandStateChanged()
    {
        NotifyPropertyChanged(nameof(CanDeleteSelectedLocalDocuments));
        NotifyPropertyChanged(nameof(SelectionSummary));
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

    private bool GetEffectiveColumnVisibility(string key)
    {
        return GetColumnVisibility(key);
    }

    private void RaiseColumnVisibilityNotifications()
    {
        NotifyPropertyChanged(nameof(IsStatusColumnVisible));
        NotifyPropertyChanged(nameof(IsOidColumnVisible));
        NotifyPropertyChanged(nameof(IsDocumentoColumnVisible));
        NotifyPropertyChanged(nameof(IsDataColumnVisible));
        NotifyPropertyChanged(nameof(IsOperatoreColumnVisible));
        NotifyPropertyChanged(nameof(IsClienteColumnVisible));
        NotifyPropertyChanged(nameof(IsTotaleColumnVisible));
        NotifyPropertyChanged(nameof(IsPagContantiColumnVisible));
        NotifyPropertyChanged(nameof(IsPagCartaColumnVisible));
        NotifyPropertyChanged(nameof(IsPagWebColumnVisible));
        NotifyPropertyChanged(nameof(IsPagBuoniColumnVisible));
        NotifyPropertyChanged(nameof(IsPagSospesoColumnVisible));
        NotifyPropertyChanged(nameof(IsResiduoColumnVisible));
        NotifyPropertyChanged(nameof(IsDaFiscalizzareColumnVisible));
        NotifyPropertyChanged(nameof(IsOrigineColumnVisible));
        NotifyPropertyChanged(nameof(IsStatoColumnVisible));
        NotifyPropertyChanged(nameof(IsScontrinoColumnVisible));
    }
}

internal static class DocumentColumnDefinitions
{
    public static IReadOnlyList<GridColumnDefinition> All { get; } =
    [
        new GridColumnDefinition { Key = "Status", Header = "#", IsVisibleByDefault = true, DefaultWidth = 56, DefaultDisplayIndex = 0 },
        new GridColumnDefinition { Key = "Origine", Header = "Origine", IsVisibleByDefault = false, DefaultWidth = 90, DefaultDisplayIndex = 1, IsPresetUnscontrinati = true },
        new GridColumnDefinition { Key = "Oid", Header = "OID", IsVisibleByDefault = true, DefaultWidth = 80, DefaultDisplayIndex = 2 },
        new GridColumnDefinition { Key = "Documento", Header = "Numero", IsVisibleByDefault = true, DefaultWidth = 110, DefaultDisplayIndex = 3 },
        new GridColumnDefinition { Key = "Data", Header = "Data", IsVisibleByDefault = true, DefaultWidth = 110, DefaultDisplayIndex = 4 },
        new GridColumnDefinition { Key = "Operatore", Header = "Operatore", IsVisibleByDefault = true, DefaultWidth = 120, DefaultDisplayIndex = 5 },
        new GridColumnDefinition { Key = "Cliente", Header = "Ragione sociale", IsVisibleByDefault = true, DefaultWidth = 260, DefaultDisplayIndex = 6 },
        new GridColumnDefinition { Key = "Totale", Header = "Totale", IsVisibleByDefault = true, DefaultWidth = 100, DefaultDisplayIndex = 7, IsNumeric = true },
        new GridColumnDefinition { Key = "PagContanti", Header = "Pag. Contanti", IsVisibleByDefault = true, DefaultWidth = 100, DefaultDisplayIndex = 8, IsNumeric = true },
        new GridColumnDefinition { Key = "PagCarta", Header = "Pag. Carta", IsVisibleByDefault = true, DefaultWidth = 100, DefaultDisplayIndex = 9, IsNumeric = true },
        new GridColumnDefinition { Key = "PagWeb", Header = "Pag. Web", IsVisibleByDefault = true, DefaultWidth = 100, DefaultDisplayIndex = 10, IsNumeric = true },
        new GridColumnDefinition { Key = "PagBuoni", Header = "Pag. Buoni", IsVisibleByDefault = true, DefaultWidth = 100, DefaultDisplayIndex = 11, IsNumeric = true },
        new GridColumnDefinition { Key = "PagSospeso", Header = "Pag. Sospeso", IsVisibleByDefault = false, DefaultWidth = 100, DefaultDisplayIndex = 12, IsNumeric = true, IsPresetUnscontrinati = true },
        new GridColumnDefinition { Key = "ResiduoPagamento", Header = "Residuo", IsVisibleByDefault = false, DefaultWidth = 100, DefaultDisplayIndex = 13, IsNumeric = true, IsPresetUnscontrinati = true },
        new GridColumnDefinition { Key = "DaFiscalizzare", Header = "Da fiscalizzare", IsVisibleByDefault = false, DefaultWidth = 120, DefaultDisplayIndex = 14, IsNumeric = true, IsPresetUnscontrinati = true },
        new GridColumnDefinition { Key = "StatoDocumento", Header = "Stato documento", IsVisibleByDefault = false, DefaultWidth = 140, DefaultDisplayIndex = 15, IsPresetUnscontrinati = true },
        new GridColumnDefinition { Key = "Scontrino", Header = "Scontrino", IsVisibleByDefault = false, DefaultWidth = 100, DefaultDisplayIndex = 16, IsPresetUnscontrinati = true }
    ];
}

public enum DocumentListFilterMode
{
    Completa = 0,
    SoloScontrinati = 1,
    SoloCortesia = 2
}

public enum UIDocumentPresentationMode
{
    Default = 0,
    OnlyCortesia = 1,
    CompleteWithCortesia = 2
}
