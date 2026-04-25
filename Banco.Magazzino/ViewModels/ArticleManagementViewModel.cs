using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Banco.Core.Infrastructure;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;

namespace Banco.Magazzino.ViewModels;

public sealed class ArticleManagementViewModel : ViewModelBase
{
    private const string AllCategoriesLabel = "Tutte le categorie";
    private static readonly Regex HtmlRegex = new("<.*?>", RegexOptions.Compiled);

    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IGestionaleArticleWriteService _articleWriteService;
    private readonly ILocalArticleTagRepository _articleTagRepository;
    private readonly IPosProcessLogService _logService;
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _articleCodeDebounceCts;
    private CancellationTokenSource? _catalogReloadCts;
    private bool _suppressAutoSearch;
    private string _searchText = string.Empty;
    private string _searchCodeFilterText = string.Empty;
    private string _articleCodeInput = string.Empty;
    private string _catalogSearchText = string.Empty;
    private string _statusMessage = "Inserisci codice o barcode, oppure usa il lookup articolo.";
    private string _catalogStatusMessage = "Apri l'elenco per consultare il catalogo legacy con filtri operativi.";
    private string _detailStatusLabel = "Seleziona un articolo o usa la ricerca live.";
    private bool _isLoading;
    private bool _isCatalogLoading;
    private bool _isCatalogVisible;
    private bool _catalogCategoriesLoaded;
    private bool _catalogOnlyAvailable;
    private bool _catalogOnlyWithImage;
    private bool _legacyLookupsLoaded;
    private bool _isCategoryPickerOpen;
    private bool _isVatPickerOpen;
    private bool _isTaxPickerOpen;
    private bool _isPrimaryUnitPickerOpen;
    private bool _isSecondaryUnitPickerOpen;
    private bool _isCostAccountPickerOpen;
    private bool _isRevenueAccountPickerOpen;
    private bool _isMarkupCategoryPickerOpen;
    private bool _isArticleTypePickerOpen;
    private bool _isTraceabilityPickerOpen;
    private bool _isCostTypePickerOpen;
    private bool _isConditionPickerOpen;
    private bool _isLoyaltyOperationPickerOpen;
    private bool _isVariant1PickerOpen;
    private bool _isVariant2PickerOpen;
    private string _selectedCatalogCategoryPath = AllCategoriesLabel;
    private string _categoryPickerSearchText = string.Empty;
    private string _vatPickerSearchText = string.Empty;
    private string _taxPickerSearchText = string.Empty;
    private string _primaryUnitPickerSearchText = string.Empty;
    private string _secondaryUnitPickerSearchText = string.Empty;
    private string _costAccountPickerSearchText = string.Empty;
    private string _revenueAccountPickerSearchText = string.Empty;
    private string _markupCategoryPickerSearchText = string.Empty;
    private string _articleTypePickerSearchText = string.Empty;
    private string _traceabilityPickerSearchText = string.Empty;
    private string _costTypePickerSearchText = string.Empty;
    private string _conditionPickerSearchText = string.Empty;
    private string _loyaltyOperationPickerSearchText = string.Empty;
    private string _variant1PickerSearchText = string.Empty;
    private string _variant2PickerSearchText = string.Empty;
    private GestionaleArticleCatalogRow? _selectedCatalogRow;
    private GestionaleArticleSearchResult? _selectedQuickSearchResult;
    private GestionaleArticleSearchResult? _selectedSearchResult;
    private GestionaleLookupOption? _selectedCategoryOption;
    private GestionaleLookupOption? _selectedVatOption;
    private GestionaleLookupOption? _selectedTaxOption;
    private GestionaleLookupOption? _selectedPrimaryUnitOption;
    private GestionaleLookupOption? _selectedSecondaryUnitOption;
    private GestionaleLookupOption? _selectedCostAccountOption;
    private GestionaleLookupOption? _selectedRevenueAccountOption;
    private GestionaleLookupOption? _selectedMarkupCategoryOption;
    private GestionaleLookupOption? _selectedArticleTypeOption;
    private GestionaleLookupOption? _selectedTraceabilityOption;
    private GestionaleLookupOption? _selectedCostTypeOption;
    private GestionaleLookupOption? _selectedConditionOption;
    private GestionaleLookupOption? _selectedLoyaltyOperationOption;
    private GestionaleLookupOption? _selectedVariant1Option;
    private GestionaleLookupOption? _selectedVariant2Option;
    private int? _selectedCategoryOid;
    private int? _selectedVatOid;
    private int? _selectedTaxOid;
    private int? _selectedPrimaryUnitOid;
    private int? _selectedSecondaryUnitOid;
    private int? _selectedCostAccountOid;
    private int? _selectedRevenueAccountOid;
    private int? _selectedMarkupCategoryOid;
    private int? _selectedArticleTypeCode;
    private int? _selectedTraceabilityCode;
    private int? _selectedCostTypeCode;
    private int? _selectedConditionCode;
    private int? _selectedLoyaltyOperationCode;
    private int? _selectedVariant1Oid;
    private int? _selectedVariant2Oid;
    private int? _currentArticleOid;
    private string _articleCode = "-";
    private string _articleDescription = "Nessun articolo selezionato.";
    private string _articleSubtitle = "Apri l'elenco articoli o usa la ricerca live per caricare una scheda.";
    private string _barcodeLabel = "-";
    private string _categoryPathLabel = "-";
    private string _availabilityLabel = "-";
    private string _priceLabel = "-";
    private string _priceListLabel = "-";
    private string _unitLabel = "PZ";
    private string _variantLabel = "Articolo base";
    private string _costAccountLabel = "-";
    private string _revenueAccountLabel = "-";
    private string _markupCategoryLabel = "-";
    private string _articleTypeLabel = "Normale";
    private string _traceabilityLabel = "Nessuna";
    private string _costTypeLabel = "Normale";
    private string _variant1Label = "-";
    private string _variant2Label = "-";
    private string _warrantyMonthsLabel = "0";
    private string _soldLastMonthLabel = "0";
    private string _soldLastThreeMonthsLabel = "0";
    private string _minimumSaleQuantityLabel = "1";
    private string _saleMultipleLabel = "1";
    private string _sourceLabel = "-";
    private string _invoiceCodeTypeLabel = "-";
    private string _invoiceCodeValueLabel = "-";
    private string _legacyWarningsLabel = "-";
    private bool _isOnlineArticle;
    private string _ecommerceAvailabilityLabel = "-";
    private string _conditionLabel = "-";
    private string _loyaltyOperationLabel = "-";
    private string _codeValidationSummary = "Nessun controllo codice eseguito.";
    private bool _isArticleCodeUnique;
    private bool _isArticleCodeDuplicate;
    private bool _hasArticleCodeValidation;
    private string _salesConstraintLabel = "Nessun vincolo disponibile.";
    private string _tagsLabel = string.Empty;
    private string _newArticleTagText = string.Empty;
    private string _exciseLabel = "-";
    private bool _usesTax;
    private bool _usesBancoTouch;
    private bool _exportsArticle;
    private bool _excludesInventory;
    private bool _excludesDocumentTotal;
    private bool _excludesReceipt;
    private bool _excludesSubjectDiscount;
    private bool _isObsoleteArticle;
    private bool _addsShortDescriptionToDescription;
    private string _taxMultiplierLabel = "-";
    private string _lastSaleDateLabel = "-";
    private string _shortDescriptionText = string.Empty;
    private string _longDescriptionText = "La descrizione lunga legacy comparirà qui.";
    private string _longDescriptionHtml = string.Empty;
    private string? _articleImagePath;

    public ArticleManagementViewModel(
        IGestionaleArticleReadService articleReadService,
        IGestionaleArticleWriteService articleWriteService,
        ILocalArticleTagRepository articleTagRepository,
        IPosProcessLogService logService)
    {
        _articleReadService = articleReadService;
        _articleWriteService = articleWriteService;
        _articleTagRepository = articleTagRepository;
        _logService = logService;

        Specifications = [];
        PriceTiers = [];
        PriceTierRows = [];
        ArticleTags = [];
        ArticleTagSuggestions = [];
        QuickSearchResults = [];
        CatalogRows = [];
        CategoryPaths = [AllCategoriesLabel];
        CategoryOptions = [];
        FilteredCategoryOptions = [];
        VatOptions = [];
        FilteredVatOptions = [];
        TaxOptions = [];
        FilteredTaxOptions = [];
        UnitOptions = [];
        FilteredPrimaryUnitOptions = [];
        FilteredSecondaryUnitOptions = [];
        AccountOptions = [];
        FilteredCostAccountOptions = [];
        FilteredRevenueAccountOptions = [];
        MarkupCategoryOptions = [];
        FilteredMarkupCategoryOptions = [];
        ArticleTypeOptions = [];
        FilteredArticleTypeOptions = [];
        TraceabilityOptions = [];
        FilteredTraceabilityOptions = [];
        CostTypeOptions = [];
        FilteredCostTypeOptions = [];
        ConditionOptions = [];
        FilteredConditionOptions = [];
        LoyaltyOperationOptions = [];
        FilteredLoyaltyOperationOptions = [];
        VariantOptions = [];
        FilteredVariant1Options = [];
        FilteredVariant2Options = [];

        ToggleCatalogCommand = new RelayCommand(async () => await ToggleCatalogAsync());
        RefreshCurrentArticleCommand = new RelayCommand(async () => await ReloadCurrentArticleAsync(), () => SelectedSearchResult is not null && !IsLoading);
        CloseCurrentArticleCommand = new RelayCommand(CloseCurrentArticle, () => HasSelectedArticle && !IsLoading);
        RefreshCatalogCommand = new RelayCommand(async () => await ReloadCatalogAsync(forceReloadCategories: false), () => IsCatalogVisible && !IsCatalogLoading);
        AddArticleTagCommand = new RelayCommand(async () => await AddArticleTagAsync(), CanAddArticleTag);
        RemoveArticleTagCommand = new RelayCommand(async parameter => await RemoveArticleTagAsync(parameter as string), () => HasSelectedArticle && !IsLoading);
    }

    public string Titolo => "Gestione Articolo";

    public ObservableCollection<GestionaleArticleLookupSpecification> Specifications { get; }

    public ObservableCollection<GestionaleArticleQuantityPriceTier> PriceTiers { get; }

    public ObservableCollection<ArticleManagementPriceTierRowViewModel> PriceTierRows { get; }

    public ObservableCollection<string> ArticleTags { get; }

    public ObservableCollection<string> ArticleTagSuggestions { get; }

    public ObservableCollection<GestionaleArticleSearchResult> QuickSearchResults { get; }

    public ObservableCollection<GestionaleArticleCatalogRow> CatalogRows { get; }

    public ObservableCollection<string> CategoryPaths { get; }

    public ObservableCollection<GestionaleLookupOption> CategoryOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredCategoryOptions { get; }

    public ObservableCollection<GestionaleLookupOption> VatOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredVatOptions { get; }

    public ObservableCollection<GestionaleLookupOption> TaxOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredTaxOptions { get; }

    public ObservableCollection<GestionaleLookupOption> UnitOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredPrimaryUnitOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredSecondaryUnitOptions { get; }

    public ObservableCollection<GestionaleLookupOption> AccountOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredCostAccountOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredRevenueAccountOptions { get; }

    public ObservableCollection<GestionaleLookupOption> MarkupCategoryOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredMarkupCategoryOptions { get; }

    public ObservableCollection<GestionaleLookupOption> ArticleTypeOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredArticleTypeOptions { get; }

    public ObservableCollection<GestionaleLookupOption> TraceabilityOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredTraceabilityOptions { get; }

    public ObservableCollection<GestionaleLookupOption> CostTypeOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredCostTypeOptions { get; }

    public ObservableCollection<GestionaleLookupOption> ConditionOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredConditionOptions { get; }

    public ObservableCollection<GestionaleLookupOption> LoyaltyOperationOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredLoyaltyOperationOptions { get; }

    public ObservableCollection<GestionaleLookupOption> VariantOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredVariant1Options { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredVariant2Options { get; }

    public RelayCommand ToggleCatalogCommand { get; }

    public RelayCommand RefreshCurrentArticleCommand { get; }

    public RelayCommand CloseCurrentArticleCommand { get; }

    public RelayCommand RefreshCatalogCommand { get; }

    public RelayCommand AddArticleTagCommand { get; }

    public RelayCommand RemoveArticleTagCommand { get; }


    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                if (!_suppressAutoSearch)
                {
                    ScheduleAutoSearch();
                }
            }
        }
    }

    public string SearchCodeFilterText
    {
        get => _searchCodeFilterText;
        set
        {
            if (SetProperty(ref _searchCodeFilterText, value))
            {
                if (!_suppressAutoSearch)
                {
                    ScheduleAutoSearch();
                }
            }
        }
    }

    public string ArticleCodeInput
    {
        get => _articleCodeInput;
        set
        {
            if (SetProperty(ref _articleCodeInput, value))
            {
                ScheduleArticleCodeLookup();
            }
        }
    }

    public string CatalogSearchText
    {
        get => _catalogSearchText;
        set
        {
            if (SetProperty(ref _catalogSearchText, value))
            {
                ScheduleCatalogReload();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CatalogStatusMessage
    {
        get => _catalogStatusMessage;
        private set => SetProperty(ref _catalogStatusMessage, value);
    }

    public string DetailStatusLabel
    {
        get => _detailStatusLabel;
        private set => SetProperty(ref _detailStatusLabel, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCurrentArticleCommand.RaiseCanExecuteChanged();
                CloseCurrentArticleCommand.RaiseCanExecuteChanged();
                AddArticleTagCommand.RaiseCanExecuteChanged();
                RemoveArticleTagCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCatalogLoading
    {
        get => _isCatalogLoading;
        private set
        {
            if (SetProperty(ref _isCatalogLoading, value))
            {
                RefreshCatalogCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCatalogVisible
    {
        get => _isCatalogVisible;
        private set
        {
            if (SetProperty(ref _isCatalogVisible, value))
            {
                NotifyPropertyChanged(nameof(CatalogToggleButtonText));
                RefreshCatalogCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CatalogToggleButtonText => IsCatalogVisible ? "Chiudi elenco" : "Elenco articoli";

    public bool CatalogOnlyAvailable
    {
        get => _catalogOnlyAvailable;
        set
        {
            if (SetProperty(ref _catalogOnlyAvailable, value))
            {
                ScheduleCatalogReload();
            }
        }
    }

    public bool CatalogOnlyWithImage
    {
        get => _catalogOnlyWithImage;
        set
        {
            if (SetProperty(ref _catalogOnlyWithImage, value))
            {
                ScheduleCatalogReload();
            }
        }
    }

    public string SelectedCatalogCategoryPath
    {
        get => _selectedCatalogCategoryPath;
        set
        {
            if (SetProperty(ref _selectedCatalogCategoryPath, string.IsNullOrWhiteSpace(value) ? AllCategoriesLabel : value))
            {
                ScheduleCatalogReload();
            }
        }
    }

    public bool IsCategoryPickerOpen
    {
        get => _isCategoryPickerOpen;
        set => SetProperty(ref _isCategoryPickerOpen, value);
    }

    public bool IsVatPickerOpen
    {
        get => _isVatPickerOpen;
        set => SetProperty(ref _isVatPickerOpen, value);
    }

    public bool IsTaxPickerOpen
    {
        get => _isTaxPickerOpen;
        set => SetProperty(ref _isTaxPickerOpen, value);
    }

    public bool IsPrimaryUnitPickerOpen
    {
        get => _isPrimaryUnitPickerOpen;
        set => SetProperty(ref _isPrimaryUnitPickerOpen, value);
    }

    public bool IsSecondaryUnitPickerOpen
    {
        get => _isSecondaryUnitPickerOpen;
        set => SetProperty(ref _isSecondaryUnitPickerOpen, value);
    }

    public bool IsCostAccountPickerOpen
    {
        get => _isCostAccountPickerOpen;
        set => SetProperty(ref _isCostAccountPickerOpen, value);
    }

    public bool IsRevenueAccountPickerOpen
    {
        get => _isRevenueAccountPickerOpen;
        set => SetProperty(ref _isRevenueAccountPickerOpen, value);
    }

    public bool IsMarkupCategoryPickerOpen
    {
        get => _isMarkupCategoryPickerOpen;
        set => SetProperty(ref _isMarkupCategoryPickerOpen, value);
    }

    public bool IsArticleTypePickerOpen
    {
        get => _isArticleTypePickerOpen;
        set => SetProperty(ref _isArticleTypePickerOpen, value);
    }

    public bool IsTraceabilityPickerOpen
    {
        get => _isTraceabilityPickerOpen;
        set => SetProperty(ref _isTraceabilityPickerOpen, value);
    }

    public bool IsCostTypePickerOpen
    {
        get => _isCostTypePickerOpen;
        set => SetProperty(ref _isCostTypePickerOpen, value);
    }

    public bool IsConditionPickerOpen
    {
        get => _isConditionPickerOpen;
        set => SetProperty(ref _isConditionPickerOpen, value);
    }

    public bool IsLoyaltyOperationPickerOpen
    {
        get => _isLoyaltyOperationPickerOpen;
        set => SetProperty(ref _isLoyaltyOperationPickerOpen, value);
    }

    public bool IsVariant1PickerOpen
    {
        get => _isVariant1PickerOpen;
        set => SetProperty(ref _isVariant1PickerOpen, value);
    }

    public bool IsVariant2PickerOpen
    {
        get => _isVariant2PickerOpen;
        set => SetProperty(ref _isVariant2PickerOpen, value);
    }

    public string CategoryPickerSearchText
    {
        get => _categoryPickerSearchText;
        set
        {
            if (SetProperty(ref _categoryPickerSearchText, value))
            {
                RefreshFilteredCategoryOptions();
            }
        }
    }

    public string VatPickerSearchText
    {
        get => _vatPickerSearchText;
        set
        {
            if (SetProperty(ref _vatPickerSearchText, value))
            {
                RefreshFilteredVatOptions();
            }
        }
    }

    public string TaxPickerSearchText
    {
        get => _taxPickerSearchText;
        set
        {
            if (SetProperty(ref _taxPickerSearchText, value))
            {
                RefreshFilteredTaxOptions();
            }
        }
    }

    public string PrimaryUnitPickerSearchText
    {
        get => _primaryUnitPickerSearchText;
        set
        {
            if (SetProperty(ref _primaryUnitPickerSearchText, value))
            {
                RefreshFilteredPrimaryUnitOptions();
            }
        }
    }

    public string SecondaryUnitPickerSearchText
    {
        get => _secondaryUnitPickerSearchText;
        set
        {
            if (SetProperty(ref _secondaryUnitPickerSearchText, value))
            {
                RefreshFilteredSecondaryUnitOptions();
            }
        }
    }

    public string CostAccountPickerSearchText
    {
        get => _costAccountPickerSearchText;
        set
        {
            if (SetProperty(ref _costAccountPickerSearchText, value))
            {
                RefreshFilteredCostAccountOptions();
            }
        }
    }

    public string RevenueAccountPickerSearchText
    {
        get => _revenueAccountPickerSearchText;
        set
        {
            if (SetProperty(ref _revenueAccountPickerSearchText, value))
            {
                RefreshFilteredRevenueAccountOptions();
            }
        }
    }

    public string MarkupCategoryPickerSearchText
    {
        get => _markupCategoryPickerSearchText;
        set
        {
            if (SetProperty(ref _markupCategoryPickerSearchText, value))
            {
                RefreshFilteredMarkupCategoryOptions();
            }
        }
    }

    public string ArticleTypePickerSearchText
    {
        get => _articleTypePickerSearchText;
        set
        {
            if (SetProperty(ref _articleTypePickerSearchText, value))
            {
                RefreshFilteredArticleTypeOptions();
            }
        }
    }

    public string TraceabilityPickerSearchText
    {
        get => _traceabilityPickerSearchText;
        set
        {
            if (SetProperty(ref _traceabilityPickerSearchText, value))
            {
                RefreshFilteredTraceabilityOptions();
            }
        }
    }

    public string CostTypePickerSearchText
    {
        get => _costTypePickerSearchText;
        set
        {
            if (SetProperty(ref _costTypePickerSearchText, value))
            {
                RefreshFilteredCostTypeOptions();
            }
        }
    }

    public string ConditionPickerSearchText
    {
        get => _conditionPickerSearchText;
        set
        {
            if (SetProperty(ref _conditionPickerSearchText, value))
            {
                RefreshFilteredConditionOptions();
            }
        }
    }

    public string LoyaltyOperationPickerSearchText
    {
        get => _loyaltyOperationPickerSearchText;
        set
        {
            if (SetProperty(ref _loyaltyOperationPickerSearchText, value))
            {
                RefreshFilteredLoyaltyOperationOptions();
            }
        }
    }

    public string Variant1PickerSearchText
    {
        get => _variant1PickerSearchText;
        set
        {
            if (SetProperty(ref _variant1PickerSearchText, value))
            {
                RefreshFilteredVariant1Options();
            }
        }
    }

    public string Variant2PickerSearchText
    {
        get => _variant2PickerSearchText;
        set
        {
            if (SetProperty(ref _variant2PickerSearchText, value))
            {
                RefreshFilteredVariant2Options();
            }
        }
    }

    public GestionaleArticleCatalogRow? SelectedCatalogRow
    {
        get => _selectedCatalogRow;
        set
        {
            if (SetProperty(ref _selectedCatalogRow, value) && value is not null)
            {
                var searchResult = BuildSearchResultFromCatalogRow(value);
                _ = SelectArticleAsync(searchResult, updateSearchText: false);
            }
        }
    }

    public GestionaleArticleSearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        private set
        {
            if (SetProperty(ref _selectedSearchResult, value))
            {
                RefreshCurrentArticleCommand.RaiseCanExecuteChanged();
                AddArticleTagCommand.RaiseCanExecuteChanged();
                RemoveArticleTagCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GestionaleArticleSearchResult? SelectedQuickSearchResult
    {
        get => _selectedQuickSearchResult;
        set
        {
            if (SetProperty(ref _selectedQuickSearchResult, value) && value is not null)
            {
                _ = AcceptQuickSearchResultAsync(value);
            }
        }
    }

    public GestionaleLookupOption? SelectedCategoryOption
    {
        get => _selectedCategoryOption;
        set
        {
            if (!SetProperty(ref _selectedCategoryOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedCategoryOid != oid)
            {
                _selectedCategoryOid = oid;
                NotifyPropertyChanged(nameof(SelectedCategoryOid));
            }

            NotifyPropertyChanged(nameof(SelectedCategoryLabel));

            if (value is not null)
            {
                IsCategoryPickerOpen = false;
                if (!string.IsNullOrWhiteSpace(_categoryPickerSearchText))
                {
                    _categoryPickerSearchText = string.Empty;
                    NotifyPropertyChanged(nameof(CategoryPickerSearchText));
                    RefreshFilteredCategoryOptions();
                }
            }
        }
    }

    public GestionaleLookupOption? SelectedVatOption
    {
        get => _selectedVatOption;
        set
        {
            if (!SetProperty(ref _selectedVatOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedVatOid != oid)
            {
                _selectedVatOid = oid;
                NotifyPropertyChanged(nameof(SelectedVatOid));
            }

            NotifyPropertyChanged(nameof(SelectedVatLabel));
            ClosePickerAfterSelection(LookupPickerKind.Vat, value is not null, ref _vatPickerSearchText, nameof(VatPickerSearchText), RefreshFilteredVatOptions);
        }
    }

    public GestionaleLookupOption? SelectedTaxOption
    {
        get => _selectedTaxOption;
        set
        {
            if (!SetProperty(ref _selectedTaxOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedTaxOid != oid)
            {
                _selectedTaxOid = oid;
                NotifyPropertyChanged(nameof(SelectedTaxOid));
            }

            NotifyPropertyChanged(nameof(SelectedTaxLabel));
            ClosePickerAfterSelection(LookupPickerKind.Tax, value is not null, ref _taxPickerSearchText, nameof(TaxPickerSearchText), RefreshFilteredTaxOptions);
        }
    }

    public GestionaleLookupOption? SelectedPrimaryUnitOption
    {
        get => _selectedPrimaryUnitOption;
        set
        {
            if (!SetProperty(ref _selectedPrimaryUnitOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedPrimaryUnitOid != oid)
            {
                _selectedPrimaryUnitOid = oid;
                NotifyPropertyChanged(nameof(SelectedPrimaryUnitOid));
            }

            NotifyPropertyChanged(nameof(SelectedPrimaryUnitLabel));
            ClosePickerAfterSelection(LookupPickerKind.PrimaryUnit, value is not null, ref _primaryUnitPickerSearchText, nameof(PrimaryUnitPickerSearchText), RefreshFilteredPrimaryUnitOptions);
        }
    }

    public GestionaleLookupOption? SelectedSecondaryUnitOption
    {
        get => _selectedSecondaryUnitOption;
        set
        {
            if (!SetProperty(ref _selectedSecondaryUnitOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedSecondaryUnitOid != oid)
            {
                _selectedSecondaryUnitOid = oid;
                NotifyPropertyChanged(nameof(SelectedSecondaryUnitOid));
            }

            NotifyPropertyChanged(nameof(SelectedSecondaryUnitLabel));
            ClosePickerAfterSelection(LookupPickerKind.SecondaryUnit, value is not null, ref _secondaryUnitPickerSearchText, nameof(SecondaryUnitPickerSearchText), RefreshFilteredSecondaryUnitOptions);
        }
    }

    public GestionaleLookupOption? SelectedCostAccountOption
    {
        get => _selectedCostAccountOption;
        set
        {
            if (!SetProperty(ref _selectedCostAccountOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedCostAccountOid != oid)
            {
                _selectedCostAccountOid = oid;
                NotifyPropertyChanged(nameof(SelectedCostAccountOid));
            }

            NotifyPropertyChanged(nameof(SelectedCostAccountLabel));
            ClosePickerAfterSelection(LookupPickerKind.CostAccount, value is not null, ref _costAccountPickerSearchText, nameof(CostAccountPickerSearchText), RefreshFilteredCostAccountOptions);
        }
    }

    public GestionaleLookupOption? SelectedRevenueAccountOption
    {
        get => _selectedRevenueAccountOption;
        set
        {
            if (!SetProperty(ref _selectedRevenueAccountOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedRevenueAccountOid != oid)
            {
                _selectedRevenueAccountOid = oid;
                NotifyPropertyChanged(nameof(SelectedRevenueAccountOid));
            }

            NotifyPropertyChanged(nameof(SelectedRevenueAccountLabel));
            ClosePickerAfterSelection(LookupPickerKind.RevenueAccount, value is not null, ref _revenueAccountPickerSearchText, nameof(RevenueAccountPickerSearchText), RefreshFilteredRevenueAccountOptions);
        }
    }

    public GestionaleLookupOption? SelectedMarkupCategoryOption
    {
        get => _selectedMarkupCategoryOption;
        set
        {
            if (!SetProperty(ref _selectedMarkupCategoryOption, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedMarkupCategoryOid != oid)
            {
                _selectedMarkupCategoryOid = oid;
                NotifyPropertyChanged(nameof(SelectedMarkupCategoryOid));
            }

            NotifyPropertyChanged(nameof(SelectedMarkupCategoryLabel));
            ClosePickerAfterSelection(LookupPickerKind.MarkupCategory, value is not null, ref _markupCategoryPickerSearchText, nameof(MarkupCategoryPickerSearchText), RefreshFilteredMarkupCategoryOptions);
        }
    }

    public GestionaleLookupOption? SelectedArticleTypeOption
    {
        get => _selectedArticleTypeOption;
        set
        {
            if (!SetProperty(ref _selectedArticleTypeOption, value))
            {
                return;
            }

            var code = value?.Oid;
            if (_selectedArticleTypeCode != code)
            {
                _selectedArticleTypeCode = code;
                NotifyPropertyChanged(nameof(SelectedArticleTypeCode));
            }

            NotifyPropertyChanged(nameof(SelectedArticleTypeLabel));
            ClosePickerAfterSelection(LookupPickerKind.ArticleType, value is not null, ref _articleTypePickerSearchText, nameof(ArticleTypePickerSearchText), RefreshFilteredArticleTypeOptions);
        }
    }

    public GestionaleLookupOption? SelectedTraceabilityOption
    {
        get => _selectedTraceabilityOption;
        set
        {
            if (!SetProperty(ref _selectedTraceabilityOption, value))
            {
                return;
            }

            var code = value?.Oid;
            if (_selectedTraceabilityCode != code)
            {
                _selectedTraceabilityCode = code;
                NotifyPropertyChanged(nameof(SelectedTraceabilityCode));
            }

            NotifyPropertyChanged(nameof(SelectedTraceabilityLabel));
            ClosePickerAfterSelection(LookupPickerKind.Traceability, value is not null, ref _traceabilityPickerSearchText, nameof(TraceabilityPickerSearchText), RefreshFilteredTraceabilityOptions);
        }
    }

    public GestionaleLookupOption? SelectedCostTypeOption
    {
        get => _selectedCostTypeOption;
        set
        {
            if (!SetProperty(ref _selectedCostTypeOption, value))
            {
                return;
            }

            var code = value?.Oid;
            if (_selectedCostTypeCode != code)
            {
                _selectedCostTypeCode = code;
                NotifyPropertyChanged(nameof(SelectedCostTypeCode));
            }

            NotifyPropertyChanged(nameof(SelectedCostTypeLabel));
            ClosePickerAfterSelection(LookupPickerKind.CostType, value is not null, ref _costTypePickerSearchText, nameof(CostTypePickerSearchText), RefreshFilteredCostTypeOptions);
        }
    }

    public GestionaleLookupOption? SelectedConditionOption
    {
        get => _selectedConditionOption;
        set
        {
            if (!SetProperty(ref _selectedConditionOption, value))
            {
                return;
            }

            var code = value?.Oid;
            if (_selectedConditionCode != code)
            {
                _selectedConditionCode = code;
                NotifyPropertyChanged(nameof(SelectedConditionCode));
            }

            NotifyPropertyChanged(nameof(SelectedConditionLabel));
            ClosePickerAfterSelection(LookupPickerKind.Condition, value is not null, ref _conditionPickerSearchText, nameof(ConditionPickerSearchText), RefreshFilteredConditionOptions);
        }
    }

    public GestionaleLookupOption? SelectedLoyaltyOperationOption
    {
        get => _selectedLoyaltyOperationOption;
        set
        {
            if (!SetProperty(ref _selectedLoyaltyOperationOption, value))
            {
                return;
            }

            var code = value?.Oid;
            if (_selectedLoyaltyOperationCode != code)
            {
                _selectedLoyaltyOperationCode = code;
                NotifyPropertyChanged(nameof(SelectedLoyaltyOperationCode));
            }

            NotifyPropertyChanged(nameof(SelectedLoyaltyOperationLabel));
            ClosePickerAfterSelection(LookupPickerKind.LoyaltyOperation, value is not null, ref _loyaltyOperationPickerSearchText, nameof(LoyaltyOperationPickerSearchText), RefreshFilteredLoyaltyOperationOptions);
        }
    }

    public GestionaleLookupOption? SelectedVariant1Option
    {
        get => _selectedVariant1Option;
        set
        {
            if (!SetProperty(ref _selectedVariant1Option, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedVariant1Oid != oid)
            {
                _selectedVariant1Oid = oid;
                NotifyPropertyChanged(nameof(SelectedVariant1Oid));
            }

            NotifyPropertyChanged(nameof(SelectedVariant1Label));
            ClosePickerAfterSelection(LookupPickerKind.Variant1, value is not null, ref _variant1PickerSearchText, nameof(Variant1PickerSearchText), RefreshFilteredVariant1Options);
        }
    }

    public GestionaleLookupOption? SelectedVariant2Option
    {
        get => _selectedVariant2Option;
        set
        {
            if (!SetProperty(ref _selectedVariant2Option, value))
            {
                return;
            }

            var oid = value?.Oid;
            if (_selectedVariant2Oid != oid)
            {
                _selectedVariant2Oid = oid;
                NotifyPropertyChanged(nameof(SelectedVariant2Oid));
            }

            NotifyPropertyChanged(nameof(SelectedVariant2Label));
            ClosePickerAfterSelection(LookupPickerKind.Variant2, value is not null, ref _variant2PickerSearchText, nameof(Variant2PickerSearchText), RefreshFilteredVariant2Options);
        }
    }

    public int? SelectedCategoryOid
    {
        get => _selectedCategoryOid;
        set
        {
            if (SetProperty(ref _selectedCategoryOid, value))
            {
                SyncSelectedCategoryOption();
            }
        }
    }

    public int? SelectedVatOid
    {
        get => _selectedVatOid;
        set
        {
            if (SetProperty(ref _selectedVatOid, value))
            {
                SyncSelectedVatOption();
            }
        }
    }

    public int? SelectedTaxOid
    {
        get => _selectedTaxOid;
        set
        {
            if (SetProperty(ref _selectedTaxOid, value))
            {
                SyncSelectedTaxOption();
            }
        }
    }

    public int? SelectedPrimaryUnitOid
    {
        get => _selectedPrimaryUnitOid;
        set
        {
            if (SetProperty(ref _selectedPrimaryUnitOid, value))
            {
                SyncSelectedPrimaryUnitOption();
            }
        }
    }

    public int? SelectedSecondaryUnitOid
    {
        get => _selectedSecondaryUnitOid;
        set
        {
            if (SetProperty(ref _selectedSecondaryUnitOid, value))
            {
                SyncSelectedSecondaryUnitOption();
            }
        }
    }

    public int? SelectedCostAccountOid
    {
        get => _selectedCostAccountOid;
        set
        {
            if (SetProperty(ref _selectedCostAccountOid, value))
            {
                SyncSelectedCostAccountOption();
            }
        }
    }

    public int? SelectedRevenueAccountOid
    {
        get => _selectedRevenueAccountOid;
        set
        {
            if (SetProperty(ref _selectedRevenueAccountOid, value))
            {
                SyncSelectedRevenueAccountOption();
            }
        }
    }

    public int? SelectedMarkupCategoryOid
    {
        get => _selectedMarkupCategoryOid;
        set
        {
            if (SetProperty(ref _selectedMarkupCategoryOid, value))
            {
                SyncSelectedMarkupCategoryOption();
            }
        }
    }

    public int? SelectedArticleTypeCode
    {
        get => _selectedArticleTypeCode;
        private set
        {
            if (SetProperty(ref _selectedArticleTypeCode, value))
            {
                SyncSelectedArticleTypeOption();
            }
        }
    }

    public int? SelectedTraceabilityCode
    {
        get => _selectedTraceabilityCode;
        private set
        {
            if (SetProperty(ref _selectedTraceabilityCode, value))
            {
                SyncSelectedTraceabilityOption();
            }
        }
    }

    public int? SelectedCostTypeCode
    {
        get => _selectedCostTypeCode;
        private set
        {
            if (SetProperty(ref _selectedCostTypeCode, value))
            {
                SyncSelectedCostTypeOption();
            }
        }
    }

    public int? SelectedConditionCode
    {
        get => _selectedConditionCode;
        private set
        {
            if (SetProperty(ref _selectedConditionCode, value))
            {
                SyncSelectedConditionOption();
            }
        }
    }

    public int? SelectedLoyaltyOperationCode
    {
        get => _selectedLoyaltyOperationCode;
        private set
        {
            if (SetProperty(ref _selectedLoyaltyOperationCode, value))
            {
                SyncSelectedLoyaltyOperationOption();
            }
        }
    }

    public int? SelectedVariant1Oid
    {
        get => _selectedVariant1Oid;
        set
        {
            if (SetProperty(ref _selectedVariant1Oid, value))
            {
                SyncSelectedVariant1Option();
            }
        }
    }

    public int? SelectedVariant2Oid
    {
        get => _selectedVariant2Oid;
        set
        {
            if (SetProperty(ref _selectedVariant2Oid, value))
            {
                SyncSelectedVariant2Option();
            }
        }
    }

    public int? CurrentArticleOid
    {
        get => _currentArticleOid;
        private set => SetProperty(ref _currentArticleOid, value);
    }

    public string ArticleCode
    {
        get => _articleCode;
        private set => SetProperty(ref _articleCode, value);
    }

    public string ArticleDescription
    {
        get => _articleDescription;
        private set => SetProperty(ref _articleDescription, value);
    }

    public string ArticleSubtitle
    {
        get => _articleSubtitle;
        private set => SetProperty(ref _articleSubtitle, value);
    }

    public string BarcodeLabel
    {
        get => _barcodeLabel;
        private set => SetProperty(ref _barcodeLabel, value);
    }

    public string CategoryPathLabel
    {
        get => _categoryPathLabel;
        private set
        {
            if (SetProperty(ref _categoryPathLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedCategoryLabel));
            }
        }
    }

    public string SelectedCategoryLabel => SelectedCategoryOption?.Label ?? CategoryPathLabel;

    public string SelectedVatLabel => SelectedVatOption?.Label ?? "-";

    public string SelectedTaxLabel => SelectedTaxOption?.Label ?? ExciseLabel;

    public string SelectedPrimaryUnitLabel => SelectedPrimaryUnitOption?.Label ?? UnitLabel;

    public string SelectedSecondaryUnitLabel => SelectedSecondaryUnitOption?.Label ?? "-";

    public string SelectedCostAccountLabel => SelectedCostAccountOption?.Label ?? CostAccountLabel;

    public string SelectedRevenueAccountLabel => SelectedRevenueAccountOption?.Label ?? RevenueAccountLabel;

    public string SelectedMarkupCategoryLabel => SelectedMarkupCategoryOption?.Label ?? MarkupCategoryLabel;

    public string SelectedArticleTypeLabel => SelectedArticleTypeOption?.Label ?? ArticleTypeLabel;

    public string SelectedTraceabilityLabel => SelectedTraceabilityOption?.Label ?? TraceabilityLabel;

    public string SelectedCostTypeLabel => SelectedCostTypeOption?.Label ?? CostTypeLabel;

    public string SelectedConditionLabel => SelectedConditionOption?.Label ?? ConditionLabel;

    public string SelectedLoyaltyOperationLabel => SelectedLoyaltyOperationOption?.Label ?? LoyaltyOperationLabel;

    public string SelectedVariant1Label => SelectedVariant1Option?.Label ?? Variant1LookupLabel;

    public string SelectedVariant2Label => SelectedVariant2Option?.Label ?? Variant2LookupLabel;

    public string AvailabilityLabel
    {
        get => _availabilityLabel;
        private set => SetProperty(ref _availabilityLabel, value);
    }

    public string PriceLabel
    {
        get => _priceLabel;
        private set => SetProperty(ref _priceLabel, value);
    }

    public string PriceListLabel
    {
        get => _priceListLabel;
        private set => SetProperty(ref _priceListLabel, value);
    }

    public string UnitLabel
    {
        get => _unitLabel;
        private set => SetProperty(ref _unitLabel, value);
    }

    public string VariantLabel
    {
        get => _variantLabel;
        private set => SetProperty(ref _variantLabel, value);
    }

    public string CostAccountLabel
    {
        get => _costAccountLabel;
        private set
        {
            if (SetProperty(ref _costAccountLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedCostAccountLabel));
            }
        }
    }

    public string RevenueAccountLabel
    {
        get => _revenueAccountLabel;
        private set
        {
            if (SetProperty(ref _revenueAccountLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedRevenueAccountLabel));
            }
        }
    }

    public string MarkupCategoryLabel
    {
        get => _markupCategoryLabel;
        private set
        {
            if (SetProperty(ref _markupCategoryLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedMarkupCategoryLabel));
            }
        }
    }

    public string ArticleTypeLabel
    {
        get => _articleTypeLabel;
        private set
        {
            if (SetProperty(ref _articleTypeLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedArticleTypeLabel));
            }
        }
    }

    public string TraceabilityLabel
    {
        get => _traceabilityLabel;
        private set
        {
            if (SetProperty(ref _traceabilityLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedTraceabilityLabel));
            }
        }
    }

    public string CostTypeLabel
    {
        get => _costTypeLabel;
        private set
        {
            if (SetProperty(ref _costTypeLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedCostTypeLabel));
            }
        }
    }

    public string Variant1LookupLabel
    {
        get => _variant1Label;
        private set
        {
            if (SetProperty(ref _variant1Label, value))
            {
                NotifyPropertyChanged(nameof(SelectedVariant1Label));
            }
        }
    }

    public string Variant2LookupLabel
    {
        get => _variant2Label;
        private set
        {
            if (SetProperty(ref _variant2Label, value))
            {
                NotifyPropertyChanged(nameof(SelectedVariant2Label));
            }
        }
    }

    public string WarrantyMonthsLabel
    {
        get => _warrantyMonthsLabel;
        private set => SetProperty(ref _warrantyMonthsLabel, value);
    }

    public string SoldLastMonthLabel
    {
        get => _soldLastMonthLabel;
        private set => SetProperty(ref _soldLastMonthLabel, value);
    }

    public string SoldLastThreeMonthsLabel
    {
        get => _soldLastThreeMonthsLabel;
        private set => SetProperty(ref _soldLastThreeMonthsLabel, value);
    }

    public string MinimumSaleQuantityLabel
    {
        get => _minimumSaleQuantityLabel;
        private set => SetProperty(ref _minimumSaleQuantityLabel, value);
    }

    public string SaleMultipleLabel
    {
        get => _saleMultipleLabel;
        private set => SetProperty(ref _saleMultipleLabel, value);
    }

    public string SourceLabel
    {
        get => _sourceLabel;
        private set => SetProperty(ref _sourceLabel, value);
    }

    public string InvoiceCodeTypeLabel
    {
        get => _invoiceCodeTypeLabel;
        private set => SetProperty(ref _invoiceCodeTypeLabel, value);
    }

    public string InvoiceCodeValueLabel
    {
        get => _invoiceCodeValueLabel;
        private set => SetProperty(ref _invoiceCodeValueLabel, value);
    }

    public string LegacyWarningsLabel
    {
        get => _legacyWarningsLabel;
        private set => SetProperty(ref _legacyWarningsLabel, value);
    }

    public bool IsOnlineArticle
    {
        get => _isOnlineArticle;
        private set => SetProperty(ref _isOnlineArticle, value);
    }

    public string EcommerceAvailabilityLabel
    {
        get => _ecommerceAvailabilityLabel;
        private set => SetProperty(ref _ecommerceAvailabilityLabel, value);
    }

    public string ConditionLabel
    {
        get => _conditionLabel;
        private set
        {
            if (SetProperty(ref _conditionLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedConditionLabel));
            }
        }
    }

    public string LoyaltyOperationLabel
    {
        get => _loyaltyOperationLabel;
        private set
        {
            if (SetProperty(ref _loyaltyOperationLabel, value))
            {
                NotifyPropertyChanged(nameof(SelectedLoyaltyOperationLabel));
            }
        }
    }

    public string CodeValidationSummary
    {
        get => _codeValidationSummary;
        private set
        {
            if (SetProperty(ref _codeValidationSummary, value))
            {
                NotifyPropertyChanged(nameof(CodeValidationAlertText));
            }
        }
    }

    public bool IsArticleCodeUnique
    {
        get => _isArticleCodeUnique;
        private set => SetProperty(ref _isArticleCodeUnique, value);
    }

    public bool IsArticleCodeDuplicate
    {
        get => _isArticleCodeDuplicate;
        private set
        {
            if (SetProperty(ref _isArticleCodeDuplicate, value))
            {
                NotifyPropertyChanged(nameof(HasCodeValidationAlert));
                NotifyPropertyChanged(nameof(CodeValidationAlertText));
            }
        }
    }

    public bool HasArticleCodeValidation
    {
        get => _hasArticleCodeValidation;
        private set => SetProperty(ref _hasArticleCodeValidation, value);
    }

    public bool HasCodeValidationAlert => IsArticleCodeDuplicate;

    public string CodeValidationAlertText => IsArticleCodeDuplicate ? CodeValidationSummary : string.Empty;

    public string SalesConstraintLabel
    {
        get => _salesConstraintLabel;
        private set => SetProperty(ref _salesConstraintLabel, value);
    }

    public string TagsLabel
    {
        get => _tagsLabel;
        private set => SetProperty(ref _tagsLabel, value);
    }

    public string NewArticleTagText
    {
        get => _newArticleTagText;
        set
        {
            if (SetProperty(ref _newArticleTagText, value))
            {
                AddArticleTagCommand.RaiseCanExecuteChanged();
                _ = RefreshArticleTagSuggestionsAsync(value);
            }
        }
    }

    public string ExciseLabel
    {
        get => _exciseLabel;
        private set => SetProperty(ref _exciseLabel, value);
    }

    public bool UsesTax
    {
        get => _usesTax;
        private set
        {
            if (SetProperty(ref _usesTax, value))
            {
                NotifyPropertyChanged(nameof(TaxUsageLabel));
            }
        }
    }

    public string TaxUsageLabel => UsesTax ? "Attiva" : "Non attiva";

    public bool UsesBancoTouch
    {
        get => _usesBancoTouch;
        private set => SetProperty(ref _usesBancoTouch, value);
    }

    public bool ExportsArticle
    {
        get => _exportsArticle;
        private set => SetProperty(ref _exportsArticle, value);
    }

    public bool ExcludesInventory
    {
        get => _excludesInventory;
        private set => SetProperty(ref _excludesInventory, value);
    }

    public bool ExcludesDocumentTotal
    {
        get => _excludesDocumentTotal;
        private set => SetProperty(ref _excludesDocumentTotal, value);
    }

    public bool ExcludesReceipt
    {
        get => _excludesReceipt;
        private set => SetProperty(ref _excludesReceipt, value);
    }

    public bool ExcludesSubjectDiscount
    {
        get => _excludesSubjectDiscount;
        private set => SetProperty(ref _excludesSubjectDiscount, value);
    }

    public bool IsObsoleteArticle
    {
        get => _isObsoleteArticle;
        private set => SetProperty(ref _isObsoleteArticle, value);
    }

    public bool AddsShortDescriptionToDescription
    {
        get => _addsShortDescriptionToDescription;
        private set => SetProperty(ref _addsShortDescriptionToDescription, value);
    }

    public string TaxMultiplierLabel
    {
        get => _taxMultiplierLabel;
        private set => SetProperty(ref _taxMultiplierLabel, value);
    }

    public string LastSaleDateLabel
    {
        get => _lastSaleDateLabel;
        private set => SetProperty(ref _lastSaleDateLabel, value);
    }

    public string ShortDescriptionText
    {
        get => _shortDescriptionText;
        private set => SetProperty(ref _shortDescriptionText, value);
    }

    public string LongDescriptionText
    {
        get => _longDescriptionText;
        private set => SetProperty(ref _longDescriptionText, value);
    }

    public string LongDescriptionHtml
    {
        get => _longDescriptionHtml;
        private set => SetProperty(ref _longDescriptionHtml, value);
    }

    public string? ArticleImagePath
    {
        get => _articleImagePath;
        private set
        {
            if (SetProperty(ref _articleImagePath, value))
            {
                NotifyPropertyChanged(nameof(HasArticleImage));
            }
        }
    }

    public bool HasArticleImage => !string.IsNullOrWhiteSpace(ArticleImagePath);

    public bool HasSelectedArticle => SelectedSearchResult is not null;

    public bool HasQuickSearchResults => QuickSearchResults.Count > 0;

    public bool IsQuickSearchVisible =>
        (!string.IsNullOrWhiteSpace(SearchText) || !string.IsNullOrWhiteSpace(SearchCodeFilterText)) &&
        HasQuickSearchResults;

    public bool HasFilteredCategoryOptions => FilteredCategoryOptions.Count > 0;

    public bool HasFilteredVatOptions => FilteredVatOptions.Count > 0;

    public bool HasFilteredTaxOptions => FilteredTaxOptions.Count > 0;

    public bool HasFilteredPrimaryUnitOptions => FilteredPrimaryUnitOptions.Count > 0;

    public bool HasFilteredSecondaryUnitOptions => FilteredSecondaryUnitOptions.Count > 0;

    public bool HasFilteredCostAccountOptions => FilteredCostAccountOptions.Count > 0;

    public bool HasFilteredRevenueAccountOptions => FilteredRevenueAccountOptions.Count > 0;

    public bool HasFilteredMarkupCategoryOptions => FilteredMarkupCategoryOptions.Count > 0;

    public bool HasFilteredArticleTypeOptions => FilteredArticleTypeOptions.Count > 0;

    public bool HasFilteredTraceabilityOptions => FilteredTraceabilityOptions.Count > 0;

    public bool HasFilteredCostTypeOptions => FilteredCostTypeOptions.Count > 0;

    public bool HasFilteredConditionOptions => FilteredConditionOptions.Count > 0;

    public bool HasFilteredLoyaltyOperationOptions => FilteredLoyaltyOperationOptions.Count > 0;

    public bool HasFilteredVariant1Options => FilteredVariant1Options.Count > 0;

    public bool HasFilteredVariant2Options => FilteredVariant2Options.Count > 0;

    public bool HasSpecifications => Specifications.Count > 0;

    public bool HasPriceTiers => PriceTiers.Count > 0;

    public string SpecificationsStatusText =>
        HasSpecifications
            ? $"{Specifications.Count} specifiche legacy presenti."
            : "Nessuna specifica legacy disponibile per l'articolo selezionato.";

    public string PriceTiersStatusText =>
        HasPriceTiers
            ? $"{PriceTiers.Count} fasce prezzo legacy disponibili."
            : "Nessuna fascia prezzo aggiuntiva disponibile.";

    public async Task InitializeAsync()
    {
        if (!_legacyLookupsLoaded)
        {
            await EnsureLegacyLookupsAsync();
        }

        if (!_catalogCategoriesLoaded)
        {
            await EnsureCatalogCategoriesAsync();
        }
    }

    private async Task ToggleCatalogAsync()
    {
        if (IsCatalogVisible)
        {
            IsCatalogVisible = false;
            CatalogStatusMessage = "Elenco articoli chiuso. La scheda resta disponibile.";
            return;
        }

        IsCatalogVisible = true;
        if (string.IsNullOrWhiteSpace(CatalogSearchText) && !string.IsNullOrWhiteSpace(SearchText))
        {
            CatalogSearchText = SearchText.Trim();
        }

        await ReloadCatalogAsync(forceReloadCategories: !_catalogCategoriesLoaded);
    }

    private async Task ReloadCurrentArticleAsync()
    {
        if (SelectedSearchResult is null)
        {
            return;
        }

        await LoadSelectedArticleAsync(SelectedSearchResult);
    }

    private void ScheduleAutoSearch()
    {
        var previousCts = _searchDebounceCts;
        _searchDebounceCts = null;
        previousCts?.Cancel();

        var normalizedPrimary = SearchText.Trim();
        var normalizedSecondary = SearchCodeFilterText.Trim();
        var normalized = string.Join(
            ' ',
            new[] { normalizedPrimary, normalizedSecondary }
                .Where(static part => !string.IsNullOrWhiteSpace(part)));

        if (string.IsNullOrWhiteSpace(normalized))
        {
            ClearQuickSearchResults();
            StatusMessage = HasSelectedArticle
                ? "Scheda corrente mantenuta. Inserisci una nuova ricerca o apri l'elenco."
                : "Cerca per codice, barcode o descrizione, oppure apri l'elenco articoli.";
            return;
        }

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = RunQuickSearchAsync(normalized, cts);
    }

    private void ScheduleArticleCodeLookup()
    {
        var previousCts = _articleCodeDebounceCts;
        _articleCodeDebounceCts = null;
        previousCts?.Cancel();

        var normalizedCode = ArticleCodeInput.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            CodeValidationSummary = "Nessun controllo codice eseguito.";
            HasArticleCodeValidation = false;
            IsArticleCodeUnique = false;
            IsArticleCodeDuplicate = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _articleCodeDebounceCts = cts;
        _ = RunArticleCodeLookupAsync(normalizedCode, cts);
    }

    public async Task<bool> TryLoadArticleMasterByCodeOrBarcodeAsync(
        string codeOrBarcode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = codeOrBarcode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            CodeValidationSummary = "Nessun controllo codice eseguito.";
            HasArticleCodeValidation = false;
            IsArticleCodeUnique = false;
            IsArticleCodeDuplicate = false;
            return false;
        }

        var validation = await _articleReadService.ValidateArticleCodeAsync(normalizedCode, cancellationToken);
        if (validation is not null)
        {
            ApplyCodeValidation(validation, new GestionaleArticleSearchResult
            {
                CodiceArticolo = normalizedCode,
                Descrizione = validation.DescrizioneArticolo
            });
        }

        var masterMatch = await _articleReadService.FindArticleMasterByCodeOrBarcodeAsync(
            normalizedCode,
            cancellationToken: cancellationToken);
        if (masterMatch is null)
        {
            CodeValidationSummary = "Ricerca codice/barcode trovata, ma non e` stato possibile aprire la scheda master legacy.";
            HasArticleCodeValidation = true;
            IsArticleCodeUnique = false;
            IsArticleCodeDuplicate = false;
            return false;
        }

        await SelectArticleAsync(masterMatch, updateSearchText: false);
        StatusMessage = $"Scheda master {masterMatch.CodiceArticolo} caricata dal campo codice/barcode.";
        return true;
    }

    public async Task<GestionaleArticleSearchResult?> ResolveLookupSelectionToManagementMasterAsync(
        GestionaleArticleSearchResult? article,
        CancellationToken cancellationToken = default)
    {
        if (article is null)
        {
            return null;
        }

        return await _articleReadService.GetArticleMasterAsync(article, cancellationToken: cancellationToken) ?? article;
    }

    public async Task LoadArticleForManagementAsync(GestionaleArticleSearchResult article)
    {
        await SelectArticleAsync(article, updateSearchText: false);
        StatusMessage = article.HasVariantChildren
            ? $"Scheda padre {article.CodiceArticolo} caricata dal lookup articolo."
            : $"Scheda {article.CodiceArticolo} caricata dal lookup articolo.";
    }

    private void CloseCurrentArticle()
    {
        SelectedSearchResult = null;
        CurrentArticleOid = null;
        SelectedCatalogRow = null;
        ArticleCodeInput = string.Empty;
        ArticleCode = "-";
        ArticleDescription = "Nessun articolo selezionato.";
        ArticleSubtitle = "Apri l'elenco articoli o usa il lookup articolo per caricare una scheda.";
        BarcodeLabel = "-";
        CategoryPathLabel = "-";
        AvailabilityLabel = "-";
        PriceLabel = "-";
        PriceListLabel = "-";
        UnitLabel = "PZ";
        VariantLabel = "Articolo base";
        CostAccountLabel = "-";
        RevenueAccountLabel = "-";
        MarkupCategoryLabel = "-";
        ArticleTypeLabel = "Normale";
        TraceabilityLabel = "Nessuna";
        CostTypeLabel = "Normale";
        Variant1LookupLabel = "-";
        Variant2LookupLabel = "-";
        WarrantyMonthsLabel = "0";
        SoldLastMonthLabel = "0";
        SoldLastThreeMonthsLabel = "0";
        MinimumSaleQuantityLabel = "1";
        SaleMultipleLabel = "1";
        SourceLabel = "-";
        InvoiceCodeTypeLabel = "-";
        InvoiceCodeValueLabel = "-";
        LegacyWarningsLabel = "-";
        IsOnlineArticle = false;
        EcommerceAvailabilityLabel = "-";
        ConditionLabel = "-";
        LoyaltyOperationLabel = "-";
        CodeValidationSummary = "Nessun controllo codice eseguito.";
        HasArticleCodeValidation = false;
        IsArticleCodeUnique = false;
        IsArticleCodeDuplicate = false;
        SalesConstraintLabel = "Nessun vincolo disponibile.";
        TagsLabel = string.Empty;
        ArticleTags.Clear();
        NewArticleTagText = string.Empty;
        ExciseLabel = "-";
        UsesTax = false;
        UsesBancoTouch = false;
        ExportsArticle = false;
        ExcludesInventory = false;
        ExcludesDocumentTotal = false;
        ExcludesReceipt = false;
        ExcludesSubjectDiscount = false;
        IsObsoleteArticle = false;
        AddsShortDescriptionToDescription = false;
        TaxMultiplierLabel = "-";
        LastSaleDateLabel = "-";
        ShortDescriptionText = string.Empty;
        LongDescriptionText = string.Empty;
        LongDescriptionHtml = string.Empty;
        ArticleImagePath = null;
        SelectedCategoryOid = null;
        SelectedVatOid = null;
        SelectedTaxOid = null;
        SelectedPrimaryUnitOid = null;
        SelectedSecondaryUnitOid = null;
        SelectedCostAccountOid = null;
        SelectedRevenueAccountOid = null;
        SelectedMarkupCategoryOid = null;
        SelectedArticleTypeCode = null;
        SelectedTraceabilityCode = null;
        SelectedCostTypeCode = null;
        SelectedConditionCode = null;
        SelectedLoyaltyOperationCode = null;
        SelectedVariant1Oid = null;
        SelectedVariant2Oid = null;
        Specifications.Clear();
        PriceTiers.Clear();
        PriceTierRows.Clear();
        ClearQuickSearchResults();
        DetailStatusLabel = "Scheda chiusa. Campo codice pronto per una nuova ricerca.";
        StatusMessage = "Scheda articolo chiusa senza salvataggio.";
        NotifyPropertyChanged(nameof(HasSelectedArticle));
        CloseCurrentArticleCommand.RaiseCanExecuteChanged();
    }

    private async Task RunArticleCodeLookupAsync(string normalizedCode, CancellationTokenSource lookupCts)
    {
        try
        {
            var cancellationToken = lookupCts.Token;
            await Task.Delay(220, cancellationToken);
            if (cancellationToken.IsCancellationRequested ||
                !string.Equals(ArticleCodeInput.Trim(), normalizedCode, StringComparison.Ordinal))
            {
                return;
            }

            var validation = await _articleReadService.ValidateArticleCodeAsync(normalizedCode, cancellationToken);
            if (cancellationToken.IsCancellationRequested ||
                !string.Equals(ArticleCodeInput.Trim(), normalizedCode, StringComparison.Ordinal))
            {
                return;
            }

            if (validation is null)
            {
                return;
            }

            ApplyCodeValidation(validation, new GestionaleArticleSearchResult
            {
                CodiceArticolo = normalizedCode,
                Descrizione = validation.DescrizioneArticolo
            });

            if (!validation.IsUnique)
            {
                return;
            }

            var masterMatch = await _articleReadService.FindArticleMasterByCodeOrBarcodeAsync(
                normalizedCode,
                cancellationToken: cancellationToken);
            if (masterMatch is null)
            {
                CodeValidationSummary = "Ricerca codice/barcode trovata, ma non è stato possibile aprire la scheda master legacy.";
                HasArticleCodeValidation = true;
                IsArticleCodeUnique = false;
                IsArticleCodeDuplicate = false;
                return;
            }

            await SelectArticleAsync(masterMatch, updateSearchText: false);
            StatusMessage = $"Scheda master {masterMatch.CodiceArticolo} caricata dal campo codice/barcode.";
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            CodeValidationSummary = $"Errore controllo codice: {ex.Message}";
            HasArticleCodeValidation = true;
            IsArticleCodeUnique = false;
            IsArticleCodeDuplicate = false;
            _logService.Error(nameof(ArticleManagementViewModel), "Errore durante il controllo codice articolo legacy.", ex);
        }
        finally
        {
            if (ReferenceEquals(_articleCodeDebounceCts, lookupCts))
            {
                _articleCodeDebounceCts = null;
            }

            lookupCts.Dispose();
        }
    }

    private async Task RunQuickSearchAsync(string normalizedSearch, CancellationTokenSource searchCts)
    {
        try
        {
            var cancellationToken = searchCts.Token;
            await Task.Delay(220, cancellationToken);
            var currentSearch = string.Join(
                ' ',
                new[] { SearchText.Trim(), SearchCodeFilterText.Trim() }
                    .Where(static part => !string.IsNullOrWhiteSpace(part)));

            if (cancellationToken.IsCancellationRequested || !string.Equals(currentSearch, normalizedSearch, StringComparison.Ordinal))
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Ricerca scheda articolo in corso...";

            var results = await _articleReadService.SearchArticleMastersAsync(
                normalizedSearch,
                maxResults: 25,
                cancellationToken: cancellationToken);
            currentSearch = string.Join(
                ' ',
                new[] { SearchText.Trim(), SearchCodeFilterText.Trim() }
                    .Where(static part => !string.IsNullOrWhiteSpace(part)));

            if (cancellationToken.IsCancellationRequested || !string.Equals(currentSearch, normalizedSearch, StringComparison.Ordinal))
            {
                return;
            }

            ReplaceCollection(QuickSearchResults, results);
            NotifyQuickSearchStateChanged();

            if (results.Count == 0)
            {
                StatusMessage = "Nessun articolo trovato. Prova ad aprire l'elenco con filtri più ampi.";
                return;
            }

            StatusMessage = results.Count == 1
                ? "1 scheda master compatibile trovata. Selezionala dalla lista rapida sotto la ricerca."
                : $"{results.Count} schede master compatibili trovate. Scegli dalla lista rapida oppure apri l'elenco articoli.";
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore ricerca articoli: {ex.Message}";
            _logService.Error(nameof(ArticleManagementViewModel), "Errore durante la ricerca articoli legacy per Gestione Articolo.", ex);
        }
        finally
        {
            if (ReferenceEquals(_searchDebounceCts, searchCts))
            {
                _searchDebounceCts = null;
            }

            searchCts.Dispose();
            IsLoading = false;
        }
    }

    private void ScheduleCatalogReload()
    {
        if (!IsCatalogVisible)
        {
            return;
        }

        var previousCts = _catalogReloadCts;
        _catalogReloadCts = null;
        previousCts?.Cancel();

        var cts = new CancellationTokenSource();
        _catalogReloadCts = cts;
        _ = RunCatalogReloadAsync(cts);
    }

    private async Task RunCatalogReloadAsync(CancellationTokenSource reloadCts)
    {
        try
        {
            var cancellationToken = reloadCts.Token;
            await Task.Delay(180, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await ReloadCatalogAsync(forceReloadCategories: false, cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_catalogReloadCts, reloadCts))
            {
                _catalogReloadCts = null;
            }

            reloadCts.Dispose();
        }
    }

    private async Task ReloadCatalogAsync(bool forceReloadCategories, CancellationToken cancellationToken = default)
    {
        try
        {
            IsCatalogLoading = true;

            if (forceReloadCategories || !_catalogCategoriesLoaded)
            {
                await EnsureCatalogCategoriesAsync(cancellationToken);
            }

            CatalogStatusMessage = "Caricamento elenco articoli legacy in corso...";

            var filter = new GestionaleArticleCatalogFilter
            {
                SearchText = CatalogSearchText.Trim(),
                CategoryPath = string.Equals(SelectedCatalogCategoryPath, AllCategoriesLabel, StringComparison.Ordinal)
                    ? null
                    : SelectedCatalogCategoryPath,
                OnlyAvailable = CatalogOnlyAvailable,
                OnlyWithImage = CatalogOnlyWithImage
            };

            var rows = await _articleReadService.BrowseArticlesAsync(filter, maxResults: 250, cancellationToken: cancellationToken);

            CatalogRows.Clear();
            foreach (var row in rows)
            {
                CatalogRows.Add(row);
            }

            if (SelectedSearchResult is not null)
            {
                SelectedCatalogRow = CatalogRows.FirstOrDefault(row => row.Oid == SelectedSearchResult.Oid);
            }

            CatalogStatusMessage = CatalogRows.Count == 0
                ? "Nessun articolo compatibile con i filtri impostati."
                : $"{CatalogRows.Count} articoli disponibili nell'elenco.";
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            CatalogStatusMessage = $"Errore elenco articoli: {ex.Message}";
            _logService.Error(nameof(ArticleManagementViewModel), "Errore durante il caricamento dell'elenco articoli legacy.", ex);
        }
        finally
        {
            IsCatalogLoading = false;
        }
    }

    private async Task EnsureCatalogCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _articleReadService.GetArticleCategoryPathsAsync(cancellationToken);

        CategoryPaths.Clear();
        CategoryPaths.Add(AllCategoriesLabel);
        foreach (var category in categories)
        {
            CategoryPaths.Add(category);
        }

        _catalogCategoriesLoaded = true;
        if (!CategoryPaths.Contains(SelectedCatalogCategoryPath))
        {
            SelectedCatalogCategoryPath = AllCategoriesLabel;
        }
    }

    private async Task EnsureLegacyLookupsAsync(CancellationToken cancellationToken = default)
    {
        var categoryOptions = await _articleReadService.GetArticleCategoryOptionsAsync(cancellationToken);
        var vatOptions = await _articleReadService.GetVatOptionsAsync(cancellationToken);
        var taxOptions = await _articleReadService.GetTaxOptionsAsync(cancellationToken);
        var unitOptions = await _articleReadService.GetUnitOptionsAsync(cancellationToken);
        var accountOptions = await _articleReadService.GetAccountOptionsAsync(cancellationToken);
        var markupCategoryOptions = await _articleReadService.GetMarkupCategoryOptionsAsync(cancellationToken);
        var articleTypeOptions = await _articleReadService.GetArticleTypeOptionsAsync(cancellationToken);
        var traceabilityOptions = await _articleReadService.GetTraceabilityOptionsAsync(cancellationToken);
        var costTypeOptions = await _articleReadService.GetCostTypeOptionsAsync(cancellationToken);
        var conditionOptions = await _articleReadService.GetConditionOptionsAsync(cancellationToken);
        var loyaltyOperationOptions = await _articleReadService.GetLoyaltyOperationOptionsAsync(cancellationToken);
        var variantOptions = await _articleReadService.GetVariantOptionsAsync(cancellationToken);

        ReplaceCollection(CategoryOptions, categoryOptions);
        ReplaceCollection(VatOptions, vatOptions);
        ReplaceCollection(TaxOptions, taxOptions);
        ReplaceCollection(UnitOptions, unitOptions);
        ReplaceCollection(AccountOptions, accountOptions);
        ReplaceCollection(MarkupCategoryOptions, markupCategoryOptions);
        ReplaceCollection(ArticleTypeOptions, articleTypeOptions);
        ReplaceCollection(TraceabilityOptions, traceabilityOptions);
        ReplaceCollection(CostTypeOptions, costTypeOptions);
        ReplaceCollection(ConditionOptions, conditionOptions);
        ReplaceCollection(LoyaltyOperationOptions, loyaltyOperationOptions);
        ReplaceCollection(VariantOptions, variantOptions);
        SyncSelectedCategoryOption();
        SyncSelectedVatOption();
        SyncSelectedTaxOption();
        SyncSelectedPrimaryUnitOption();
        SyncSelectedSecondaryUnitOption();
        SyncSelectedCostAccountOption();
        SyncSelectedRevenueAccountOption();
        SyncSelectedMarkupCategoryOption();
        SyncSelectedArticleTypeOption();
        SyncSelectedTraceabilityOption();
        SyncSelectedCostTypeOption();
        SyncSelectedConditionOption();
        SyncSelectedLoyaltyOperationOption();
        SyncSelectedVariant1Option();
        SyncSelectedVariant2Option();
        RefreshFilteredCategoryOptions();
        RefreshFilteredVatOptions();
        RefreshFilteredTaxOptions();
        RefreshFilteredPrimaryUnitOptions();
        RefreshFilteredSecondaryUnitOptions();
        RefreshFilteredCostAccountOptions();
        RefreshFilteredRevenueAccountOptions();
        RefreshFilteredMarkupCategoryOptions();
        RefreshFilteredArticleTypeOptions();
        RefreshFilteredTraceabilityOptions();
        RefreshFilteredCostTypeOptions();
        RefreshFilteredConditionOptions();
        RefreshFilteredLoyaltyOperationOptions();
        RefreshFilteredVariant1Options();
        RefreshFilteredVariant2Options();

        _legacyLookupsLoaded = true;
    }

    private async Task SelectArticleAsync(GestionaleArticleSearchResult article, bool updateSearchText)
    {
        if (updateSearchText)
        {
            _suppressAutoSearch = true;
            try
            {
                SearchText = article.CodiceArticolo;
            }
            finally
            {
                _suppressAutoSearch = false;
            }
        }

        ArticleCodeInput = article.CodiceArticolo;

        ClearQuickSearchResults();
        SelectedSearchResult = article;
        CurrentArticleOid = article.Oid;
        await LoadSelectedArticleAsync(article);
    }

    private async Task AcceptQuickSearchResultAsync(GestionaleArticleSearchResult article)
    {
        await SelectArticleAsync(article, updateSearchText: true);
        StatusMessage = article.HasVariantChildren
            ? $"Scheda padre {article.CodiceArticolo} caricata dalla ricerca live."
            : $"Scheda {article.CodiceArticolo} caricata dalla ricerca live.";
        SelectedQuickSearchResult = null;
    }

    private async Task LoadSelectedArticleAsync(GestionaleArticleSearchResult article)
    {
        try
        {
            IsLoading = true;
            DetailStatusLabel = "Lettura scheda articolo legacy in corso...";

            if (!_legacyLookupsLoaded)
            {
                await EnsureLegacyLookupsAsync();
            }

            var pricingDetail = await _articleReadService.GetArticlePricingDetailAsync(article);
            var lookupDetail = await _articleReadService.GetArticleLookupDetailAsync(article);
            var codeValidation = await _articleReadService.ValidateArticleCodeAsync(article.CodiceArticolo);

            CurrentArticleOid = lookupDetail?.ArticoloOid > 0
                ? lookupDetail.ArticoloOid
                : article.Oid;
            ArticleCode = article.CodiceArticolo;
            ArticleDescription = article.Descrizione;
            ArticleCodeInput = article.CodiceArticolo;
            ArticleSubtitle = lookupDetail?.LastSaleDate is null
                ? "Scheda legacy in consultazione."
                : $"Ultima vendita legacy il {lookupDetail.LastSaleDate.Value:dd/MM/yyyy}.";
            BarcodeLabel = NormalizeLabel(lookupDetail?.BarcodeAlternativo ?? article.BarcodeAlternativo);
            CategoryPathLabel = BuildCategoryPathLabel(lookupDetail);
            AvailabilityLabel = $"{article.Giacenza.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"))} {ResolvePrimaryUnit(pricingDetail)}";
            PriceLabel = $"{article.PrezzoVendita.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"))} €";
            PriceListLabel = NormalizeLabel(lookupDetail?.ListinoNome ?? pricingDetail?.ListinoNome);
            UnitLabel = ResolveUnitLabel(pricingDetail);
            VariantLabel = string.IsNullOrWhiteSpace(article.VarianteLabel) ? "Articolo base" : article.VarianteLabel;
            CostAccountLabel = NormalizeLabel(lookupDetail?.ContoCostoLabel);
            RevenueAccountLabel = NormalizeLabel(lookupDetail?.ContoRicavoLabel);
            MarkupCategoryLabel = NormalizeLabel(lookupDetail?.CategoriaRicaricoLabel);
            ArticleTypeLabel = ResolveLegacyCodeLabel(lookupDetail?.TipoArticoloCode, "Normale");
            TraceabilityLabel = ResolveLegacyCodeLabel(lookupDetail?.TracciabilitaCode, "Nessuna");
            CostTypeLabel = ResolveLegacyCodeLabel(lookupDetail?.TipoCostoArticoloCode, "Normale");
            Variant1LookupLabel = NormalizeLabel(lookupDetail?.Variante1LookupLabel);
            Variant2LookupLabel = NormalizeLabel(lookupDetail?.Variante2LookupLabel);
            WarrantyMonthsLabel = FormatWarrantyMonthsLabel(lookupDetail?.GaranziaMesiVendita);
            SoldLastMonthLabel = FormatSoldQuantityLabel(lookupDetail?.VendutoUltimoMese ?? 0m);
            SoldLastThreeMonthsLabel = FormatSoldQuantityLabel(lookupDetail?.VendutoUltimiTreMesi ?? 0m);
            MinimumSaleQuantityLabel = FormatDecimalLabel(lookupDetail?.QuantitaMinimaVendita ?? pricingDetail?.QuantitaMinimaVendita ?? 1m);
            SaleMultipleLabel = FormatDecimalLabel(lookupDetail?.QuantitaMultiplaVendita ?? pricingDetail?.QuantitaMultiplaVendita ?? 1m);
            SourceLabel = NormalizeLabel(lookupDetail?.Fonte);
            InvoiceCodeTypeLabel = NormalizeLabel(lookupDetail?.CodiceTipo);
            InvoiceCodeValueLabel = NormalizeLabel(lookupDetail?.CodiceValore);
            LegacyWarningsLabel = NormalizeMultilineText(lookupDetail?.Avvertenze);
            IsOnlineArticle = lookupDetail?.Online ?? false;
            EcommerceAvailabilityLabel = ResolveEcommerceAvailabilityLabel(lookupDetail);
            ConditionLabel = ResolveLegacyCodeLabel(lookupDetail?.CondizioneCode, "Nuovo");
            LoyaltyOperationLabel = ResolveLegacyCodeLabel(lookupDetail?.OperazioneSuCartaFedeltaCode, "Aumento punti carta fedelta'");
            ApplyCodeValidation(codeValidation, article);
            SalesConstraintLabel = ResolveSalesConstraintLabel(pricingDetail);
            var articleTags = lookupDetail?.Tags ?? [];
            SetArticleTags(articleTags);
            TagsLabel = ResolveTagsLabelFromTags(articleTags);
            NewArticleTagText = string.Empty;
            await CacheArticleTagsAsync(CurrentArticleOid ?? article.Oid, articleTags);
            await RefreshArticleTagSuggestionsAsync();
            ExciseLabel = NormalizeLabel(lookupDetail?.ExciseLabel);
            UsesTax = lookupDetail?.UsaTassa ?? false;
            UsesBancoTouch = lookupDetail?.UsaVenditaAlBancoTouch ?? false;
            ExportsArticle = lookupDetail?.Esporta ?? false;
            ExcludesInventory = lookupDetail?.EscludiInventario ?? false;
            ExcludesDocumentTotal = lookupDetail?.EscludiTotaleDocumento ?? false;
            ExcludesReceipt = lookupDetail?.EscludiScontrino ?? false;
            ExcludesSubjectDiscount = lookupDetail?.EscludiScontoSoggetto ?? false;
            IsObsoleteArticle = lookupDetail?.IsObsoleto ?? false;
            AddsShortDescriptionToDescription = lookupDetail?.AggDescrBreveAllaDescrizione ?? false;
            TaxMultiplierLabel = FormatTaxMultiplierLabel(lookupDetail?.MoltiplicatoreTassa, UsesTax);
            LastSaleDateLabel = lookupDetail?.LastSaleDate?.ToString("dd/MM/yyyy") ?? "-";
            ShortDescriptionText = NormalizeMultilineText(lookupDetail?.DescrizioneBreveHtml);
            LongDescriptionText = NormalizeMultilineText(lookupDetail?.DescrizioneLungaHtml);
            LongDescriptionHtml = lookupDetail?.DescrizioneLungaHtml ?? string.Empty;
            ArticleImagePath = await ResolveArticleCoverImagePathAsync(CurrentArticleOid ?? article.Oid, lookupDetail?.ImageUrl);
            SelectedCategoryOid = lookupDetail?.CategoriaOid;
            SelectedVatOid = article.IvaOid > 0
                ? article.IvaOid
                : lookupDetail?.IvaOid;
            SelectedTaxOid = lookupDetail?.TassaOid;
            SelectedPrimaryUnitOid = lookupDetail?.UnitaMisuraOid;
            SelectedSecondaryUnitOid = lookupDetail?.UnitaMisuraSecondariaOid;
            SelectedCostAccountOid = lookupDetail?.ContoCostoOid;
            SelectedRevenueAccountOid = lookupDetail?.ContoRicavoOid;
            SelectedMarkupCategoryOid = lookupDetail?.CategoriaRicaricoOid;
            SelectedArticleTypeCode = lookupDetail?.TipoArticoloCode;
            SelectedTraceabilityCode = lookupDetail?.TracciabilitaCode;
            SelectedCostTypeCode = lookupDetail?.TipoCostoArticoloCode;
            SelectedConditionCode = lookupDetail?.CondizioneCode;
            SelectedLoyaltyOperationCode = lookupDetail?.OperazioneSuCartaFedeltaCode;
            SelectedVariant1Oid = lookupDetail?.Variante1Oid;
            SelectedVariant2Oid = lookupDetail?.Variante2Oid;
            SyncSelectedCategoryOption();
            SyncSelectedVatOption();
            SyncSelectedTaxOption();
            SyncSelectedPrimaryUnitOption();
            SyncSelectedSecondaryUnitOption();
            SyncSelectedCostAccountOption();
            SyncSelectedRevenueAccountOption();
            SyncSelectedMarkupCategoryOption();
            SyncSelectedArticleTypeOption();
            SyncSelectedTraceabilityOption();
            SyncSelectedCostTypeOption();
            SyncSelectedConditionOption();
            SyncSelectedLoyaltyOperationOption();
            SyncSelectedVariant1Option();
            SyncSelectedVariant2Option();

            var priceTiers = lookupDetail?.FascePrezzoQuantita ?? pricingDetail?.FascePrezzoQuantita ?? [];

            ReplaceCollection(Specifications, lookupDetail?.Specifications ?? []);
            ReplaceCollection(PriceTiers, priceTiers);
            ReplaceCollection(
                PriceTierRows,
                priceTiers.Select(static tier => new ArticleManagementPriceTierRowViewModel(tier)).ToList());

            NotifyPropertyChanged(nameof(HasSelectedArticle));
            NotifyPropertyChanged(nameof(HasSpecifications));
            NotifyPropertyChanged(nameof(HasPriceTiers));
            NotifyPropertyChanged(nameof(SpecificationsStatusText));
            NotifyPropertyChanged(nameof(PriceTiersStatusText));

            DetailStatusLabel = "Scheda legacy caricata in sola lettura.";
        }
        catch (Exception ex)
        {
            DetailStatusLabel = $"Errore lettura dettaglio: {ex.Message}";
            _logService.Error(nameof(ArticleManagementViewModel), "Errore durante il caricamento del dettaglio legacy articolo.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static GestionaleArticleSearchResult BuildSearchResultFromCatalogRow(GestionaleArticleCatalogRow row) =>
        new()
        {
            Oid = row.Oid,
            CodiceArticolo = row.CodiceArticolo,
            Descrizione = row.Descrizione,
            PrezzoVendita = row.PrezzoVendita,
            Giacenza = row.Giacenza,
            BarcodeAlternativo = row.BarcodePrincipale
        };

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void ClearQuickSearchResults()
    {
        if (QuickSearchResults.Count == 0 && SelectedQuickSearchResult is null)
        {
            return;
        }

        QuickSearchResults.Clear();
        _selectedQuickSearchResult = null;
        NotifyPropertyChanged(nameof(SelectedQuickSearchResult));
        NotifyQuickSearchStateChanged();
    }

    public void OpenLookupPicker(string? pickerKey)
    {
        var kind = ResolveLookupPickerKind(pickerKey);
        CloseAllLookupPickers();

        switch (kind)
        {
            case LookupPickerKind.Category:
                IsCategoryPickerOpen = true;
                RefreshFilteredCategoryOptions();
                break;
            case LookupPickerKind.Vat:
                IsVatPickerOpen = true;
                RefreshFilteredVatOptions();
                break;
            case LookupPickerKind.Tax:
                IsTaxPickerOpen = true;
                RefreshFilteredTaxOptions();
                break;
            case LookupPickerKind.PrimaryUnit:
                IsPrimaryUnitPickerOpen = true;
                RefreshFilteredPrimaryUnitOptions();
                break;
            case LookupPickerKind.SecondaryUnit:
                IsSecondaryUnitPickerOpen = true;
                RefreshFilteredSecondaryUnitOptions();
                break;
            case LookupPickerKind.CostAccount:
                IsCostAccountPickerOpen = true;
                RefreshFilteredCostAccountOptions();
                break;
            case LookupPickerKind.RevenueAccount:
                IsRevenueAccountPickerOpen = true;
                RefreshFilteredRevenueAccountOptions();
                break;
            case LookupPickerKind.MarkupCategory:
                IsMarkupCategoryPickerOpen = true;
                RefreshFilteredMarkupCategoryOptions();
                break;
            case LookupPickerKind.ArticleType:
                IsArticleTypePickerOpen = true;
                RefreshFilteredArticleTypeOptions();
                break;
            case LookupPickerKind.Traceability:
                IsTraceabilityPickerOpen = true;
                RefreshFilteredTraceabilityOptions();
                break;
            case LookupPickerKind.CostType:
                IsCostTypePickerOpen = true;
                RefreshFilteredCostTypeOptions();
                break;
            case LookupPickerKind.Variant1:
                IsVariant1PickerOpen = true;
                RefreshFilteredVariant1Options();
                break;
            case LookupPickerKind.Variant2:
                IsVariant2PickerOpen = true;
                RefreshFilteredVariant2Options();
                break;
        }
    }

    private void SyncSelectedCategoryOption()
    {
        var matchedOption = _selectedCategoryOid.HasValue
            ? CategoryOptions.FirstOrDefault(item => item.Oid == _selectedCategoryOid.Value)
            : null;

        if (!ReferenceEquals(_selectedCategoryOption, matchedOption))
        {
            _selectedCategoryOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedCategoryOption));
            NotifyPropertyChanged(nameof(SelectedCategoryLabel));
        }
    }

    private void SyncSelectedVatOption()
    {
        var matchedOption = _selectedVatOid.HasValue
            ? VatOptions.FirstOrDefault(item => item.Oid == _selectedVatOid.Value)
            : null;

        if (!ReferenceEquals(_selectedVatOption, matchedOption))
        {
            _selectedVatOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedVatOption));
            NotifyPropertyChanged(nameof(SelectedVatLabel));
        }
    }

    private void SyncSelectedTaxOption()
    {
        var matchedOption = _selectedTaxOid.HasValue
            ? TaxOptions.FirstOrDefault(item => item.Oid == _selectedTaxOid.Value)
            : null;

        if (!ReferenceEquals(_selectedTaxOption, matchedOption))
        {
            _selectedTaxOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedTaxOption));
            NotifyPropertyChanged(nameof(SelectedTaxLabel));
        }
    }

    private void SyncSelectedPrimaryUnitOption()
    {
        var matchedOption = _selectedPrimaryUnitOid.HasValue
            ? UnitOptions.FirstOrDefault(item => item.Oid == _selectedPrimaryUnitOid.Value)
            : null;

        if (!ReferenceEquals(_selectedPrimaryUnitOption, matchedOption))
        {
            _selectedPrimaryUnitOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedPrimaryUnitOption));
            NotifyPropertyChanged(nameof(SelectedPrimaryUnitLabel));
        }
    }

    private void SyncSelectedSecondaryUnitOption()
    {
        var matchedOption = _selectedSecondaryUnitOid.HasValue
            ? UnitOptions.FirstOrDefault(item => item.Oid == _selectedSecondaryUnitOid.Value)
            : null;

        if (!ReferenceEquals(_selectedSecondaryUnitOption, matchedOption))
        {
            _selectedSecondaryUnitOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedSecondaryUnitOption));
            NotifyPropertyChanged(nameof(SelectedSecondaryUnitLabel));
        }
    }

    private void SyncSelectedCostAccountOption()
    {
        var matchedOption = _selectedCostAccountOid.HasValue
            ? AccountOptions.FirstOrDefault(item => item.Oid == _selectedCostAccountOid.Value)
            : null;

        if (!ReferenceEquals(_selectedCostAccountOption, matchedOption))
        {
            _selectedCostAccountOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedCostAccountOption));
            NotifyPropertyChanged(nameof(SelectedCostAccountLabel));
        }
    }

    private void SyncSelectedRevenueAccountOption()
    {
        var matchedOption = _selectedRevenueAccountOid.HasValue
            ? AccountOptions.FirstOrDefault(item => item.Oid == _selectedRevenueAccountOid.Value)
            : null;

        if (!ReferenceEquals(_selectedRevenueAccountOption, matchedOption))
        {
            _selectedRevenueAccountOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedRevenueAccountOption));
            NotifyPropertyChanged(nameof(SelectedRevenueAccountLabel));
        }
    }

    private void SyncSelectedMarkupCategoryOption()
    {
        var matchedOption = _selectedMarkupCategoryOid.HasValue
            ? MarkupCategoryOptions.FirstOrDefault(item => item.Oid == _selectedMarkupCategoryOid.Value)
            : null;

        if (!ReferenceEquals(_selectedMarkupCategoryOption, matchedOption))
        {
            _selectedMarkupCategoryOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedMarkupCategoryOption));
            NotifyPropertyChanged(nameof(SelectedMarkupCategoryLabel));
        }
    }

    private void SyncSelectedArticleTypeOption()
    {
        var matchedOption = MatchOrCreateLookupOption(ArticleTypeOptions, _selectedArticleTypeCode, ArticleTypeLabel);

        if (!ReferenceEquals(_selectedArticleTypeOption, matchedOption))
        {
            _selectedArticleTypeOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedArticleTypeOption));
            NotifyPropertyChanged(nameof(SelectedArticleTypeLabel));
        }
    }

    private void SyncSelectedTraceabilityOption()
    {
        var matchedOption = MatchOrCreateLookupOption(TraceabilityOptions, _selectedTraceabilityCode, TraceabilityLabel);

        if (!ReferenceEquals(_selectedTraceabilityOption, matchedOption))
        {
            _selectedTraceabilityOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedTraceabilityOption));
            NotifyPropertyChanged(nameof(SelectedTraceabilityLabel));
        }
    }

    private void SyncSelectedCostTypeOption()
    {
        var matchedOption = MatchOrCreateLookupOption(CostTypeOptions, _selectedCostTypeCode, CostTypeLabel);

        if (!ReferenceEquals(_selectedCostTypeOption, matchedOption))
        {
            _selectedCostTypeOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedCostTypeOption));
            NotifyPropertyChanged(nameof(SelectedCostTypeLabel));
        }
    }

    private void SyncSelectedConditionOption()
    {
        var matchedOption = MatchOrCreateLookupOption(ConditionOptions, _selectedConditionCode, ConditionLabel);

        if (!ReferenceEquals(_selectedConditionOption, matchedOption))
        {
            _selectedConditionOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedConditionOption));
            NotifyPropertyChanged(nameof(SelectedConditionLabel));
        }
    }

    private void SyncSelectedLoyaltyOperationOption()
    {
        var matchedOption = MatchOrCreateLookupOption(LoyaltyOperationOptions, _selectedLoyaltyOperationCode, LoyaltyOperationLabel);

        if (!ReferenceEquals(_selectedLoyaltyOperationOption, matchedOption))
        {
            _selectedLoyaltyOperationOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedLoyaltyOperationOption));
            NotifyPropertyChanged(nameof(SelectedLoyaltyOperationLabel));
        }
    }

    private void SyncSelectedVariant1Option()
    {
        var matchedOption = _selectedVariant1Oid.HasValue
            ? VariantOptions.FirstOrDefault(item => item.Oid == _selectedVariant1Oid.Value)
            : null;

        if (!ReferenceEquals(_selectedVariant1Option, matchedOption))
        {
            _selectedVariant1Option = matchedOption;
            NotifyPropertyChanged(nameof(SelectedVariant1Option));
            NotifyPropertyChanged(nameof(SelectedVariant1Label));
        }
    }

    private void SyncSelectedVariant2Option()
    {
        var matchedOption = _selectedVariant2Oid.HasValue
            ? VariantOptions.FirstOrDefault(item => item.Oid == _selectedVariant2Oid.Value)
            : null;

        if (!ReferenceEquals(_selectedVariant2Option, matchedOption))
        {
            _selectedVariant2Option = matchedOption;
            NotifyPropertyChanged(nameof(SelectedVariant2Option));
            NotifyPropertyChanged(nameof(SelectedVariant2Label));
        }
    }

    private void RefreshFilteredCategoryOptions()
    {
        ReplaceCollection(FilteredCategoryOptions, FilterLookupOptions(CategoryOptions, _categoryPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredCategoryOptions));
    }

    private void RefreshFilteredVatOptions()
    {
        ReplaceCollection(FilteredVatOptions, FilterLookupOptions(VatOptions, _vatPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredVatOptions));
    }

    private void RefreshFilteredTaxOptions()
    {
        ReplaceCollection(FilteredTaxOptions, FilterLookupOptions(TaxOptions, _taxPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredTaxOptions));
    }

    private void RefreshFilteredPrimaryUnitOptions()
    {
        ReplaceCollection(FilteredPrimaryUnitOptions, FilterLookupOptions(UnitOptions, _primaryUnitPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredPrimaryUnitOptions));
    }

    private void RefreshFilteredSecondaryUnitOptions()
    {
        ReplaceCollection(FilteredSecondaryUnitOptions, FilterLookupOptions(UnitOptions, _secondaryUnitPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredSecondaryUnitOptions));
    }

    private void RefreshFilteredCostAccountOptions()
    {
        ReplaceCollection(FilteredCostAccountOptions, FilterLookupOptions(AccountOptions, _costAccountPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredCostAccountOptions));
    }

    private void RefreshFilteredRevenueAccountOptions()
    {
        ReplaceCollection(FilteredRevenueAccountOptions, FilterLookupOptions(AccountOptions, _revenueAccountPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredRevenueAccountOptions));
    }

    private void RefreshFilteredMarkupCategoryOptions()
    {
        ReplaceCollection(FilteredMarkupCategoryOptions, FilterLookupOptions(MarkupCategoryOptions, _markupCategoryPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredMarkupCategoryOptions));
    }

    private void RefreshFilteredArticleTypeOptions()
    {
        ReplaceCollection(FilteredArticleTypeOptions, FilterLookupOptions(ArticleTypeOptions, _articleTypePickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredArticleTypeOptions));
    }

    private void RefreshFilteredTraceabilityOptions()
    {
        ReplaceCollection(FilteredTraceabilityOptions, FilterLookupOptions(TraceabilityOptions, _traceabilityPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredTraceabilityOptions));
    }

    private void RefreshFilteredCostTypeOptions()
    {
        ReplaceCollection(FilteredCostTypeOptions, FilterLookupOptions(CostTypeOptions, _costTypePickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredCostTypeOptions));
    }

    private void RefreshFilteredConditionOptions()
    {
        ReplaceCollection(FilteredConditionOptions, FilterLookupOptions(ConditionOptions, _conditionPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredConditionOptions));
    }

    private void RefreshFilteredLoyaltyOperationOptions()
    {
        ReplaceCollection(FilteredLoyaltyOperationOptions, FilterLookupOptions(LoyaltyOperationOptions, _loyaltyOperationPickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredLoyaltyOperationOptions));
    }

    private void RefreshFilteredVariant1Options()
    {
        ReplaceCollection(FilteredVariant1Options, FilterLookupOptions(VariantOptions, _variant1PickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredVariant1Options));
    }

    private void RefreshFilteredVariant2Options()
    {
        ReplaceCollection(FilteredVariant2Options, FilterLookupOptions(VariantOptions, _variant2PickerSearchText));
        NotifyPropertyChanged(nameof(HasFilteredVariant2Options));
    }

    private void CloseAllLookupPickers()
    {
        IsCategoryPickerOpen = false;
        IsVatPickerOpen = false;
        IsTaxPickerOpen = false;
        IsPrimaryUnitPickerOpen = false;
        IsSecondaryUnitPickerOpen = false;
        IsCostAccountPickerOpen = false;
        IsRevenueAccountPickerOpen = false;
        IsMarkupCategoryPickerOpen = false;
        IsArticleTypePickerOpen = false;
        IsTraceabilityPickerOpen = false;
        IsCostTypePickerOpen = false;
        IsConditionPickerOpen = false;
        IsLoyaltyOperationPickerOpen = false;
        IsVariant1PickerOpen = false;
        IsVariant2PickerOpen = false;
    }

    private static IReadOnlyList<GestionaleLookupOption> FilterLookupOptions(IEnumerable<GestionaleLookupOption> source, string searchText)
    {
        IEnumerable<GestionaleLookupOption> filtered = source;
        var normalizedSearch = searchText.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var terms = normalizedSearch
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            filtered = filtered.Where(option =>
                terms.All(term =>
                    option.Label.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered.Take(80).ToList();
    }

    private static GestionaleLookupOption? MatchOrCreateLookupOption(
        ObservableCollection<GestionaleLookupOption> options,
        int? selectedCode,
        string fallbackLabel)
    {
        if (!selectedCode.HasValue)
        {
            return null;
        }

        var matchedOption = options.FirstOrDefault(item => item.Oid == selectedCode.Value);
        if (matchedOption is not null)
        {
            return matchedOption;
        }

        matchedOption = new GestionaleLookupOption
        {
            Oid = selectedCode.Value,
            Label = ResolveLegacyCodeLabel(selectedCode, fallbackLabel)
        };

        options.Add(matchedOption);
        return matchedOption;
    }

    private static string ResolveLegacyCodeLabel(int? code, string defaultLabel)
    {
        if (!code.HasValue)
        {
            return defaultLabel;
        }

        return code.Value == 0
            ? defaultLabel
            : $"Codice {code.Value}";
    }

    private void ClosePickerAfterSelection(LookupPickerKind kind, bool hasSelection, ref string searchText, string searchPropertyName, Action refreshAction)
    {
        if (!hasSelection)
        {
            return;
        }

        CloseAllLookupPickers();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = string.Empty;
            NotifyPropertyChanged(searchPropertyName);
            refreshAction();
        }
    }

    private static LookupPickerKind ResolveLookupPickerKind(string? pickerKey) =>
        pickerKey switch
        {
            "Category" => LookupPickerKind.Category,
            "Vat" => LookupPickerKind.Vat,
            "Tax" => LookupPickerKind.Tax,
            "PrimaryUnit" => LookupPickerKind.PrimaryUnit,
            "SecondaryUnit" => LookupPickerKind.SecondaryUnit,
            "CostAccount" => LookupPickerKind.CostAccount,
            "RevenueAccount" => LookupPickerKind.RevenueAccount,
            "MarkupCategory" => LookupPickerKind.MarkupCategory,
            "ArticleType" => LookupPickerKind.ArticleType,
            "Traceability" => LookupPickerKind.Traceability,
            "CostType" => LookupPickerKind.CostType,
            "Condition" => LookupPickerKind.Condition,
            "LoyaltyOperation" => LookupPickerKind.LoyaltyOperation,
            "Variant1" => LookupPickerKind.Variant1,
            "Variant2" => LookupPickerKind.Variant2,
            _ => LookupPickerKind.Category
        };

    private enum LookupPickerKind
    {
        Category,
        Vat,
        Tax,
        PrimaryUnit,
        SecondaryUnit,
        CostAccount,
        RevenueAccount,
        MarkupCategory,
        ArticleType,
        Traceability,
        CostType,
        Condition,
        LoyaltyOperation,
        Variant1,
        Variant2
    }

    private void ApplyCodeValidation(GestionaleArticleCodeValidationResult? validation, GestionaleArticleSearchResult article)
    {
        if (validation is null || string.IsNullOrWhiteSpace(article.CodiceArticolo))
        {
            HasArticleCodeValidation = false;
            IsArticleCodeUnique = false;
            IsArticleCodeDuplicate = false;
            CodeValidationSummary = "Nessun controllo codice eseguito.";
            return;
        }

        HasArticleCodeValidation = true;
        IsArticleCodeUnique = validation.IsUnique;
        IsArticleCodeDuplicate = validation.IsDuplicate;

        if (validation.IsUnique)
        {
            CodeValidationSummary = $"Codice univoco: {validation.CodiceArticolo} - {NormalizeLabel(validation.DescrizioneArticolo)}";
            return;
        }

        if (validation.IsDuplicate)
        {
            var details = validation.Matches.Count == 0
                ? string.Empty
                : $" Correggere in FM: {string.Join(" | ", validation.Matches.Select(match => match.DisplayLabel))}";
            CodeValidationSummary = $"Codice/barcode duplicato nel legacy: {validation.CodiceArticolo} ({validation.MatchCount} occorrenze).{details}";
            return;
        }

        CodeValidationSummary = $"Codice non presente nel legacy: {article.CodiceArticolo}";
    }

    private void NotifyQuickSearchStateChanged()
    {
        NotifyPropertyChanged(nameof(HasQuickSearchResults));
        NotifyPropertyChanged(nameof(IsQuickSearchVisible));
    }

    private static string BuildCategoryPathLabel(GestionaleArticleLookupDetail? lookupDetail)
    {
        if (lookupDetail is null)
        {
            return "-";
        }

        if (string.IsNullOrWhiteSpace(lookupDetail.SottoCategoria))
        {
            return NormalizeLabel(lookupDetail.Categoria);
        }

        if (string.IsNullOrWhiteSpace(lookupDetail.Categoria))
        {
            return NormalizeLabel(lookupDetail.SottoCategoria);
        }

        return $"{NormalizeLabel(lookupDetail.SottoCategoria)} > {NormalizeLabel(lookupDetail.Categoria)}";
    }

    private static string ResolvePrimaryUnit(GestionaleArticlePricingDetail? pricingDetail) =>
        string.IsNullOrWhiteSpace(pricingDetail?.UnitaMisuraPrincipale)
            ? "PZ"
            : pricingDetail.UnitaMisuraPrincipale;

    private static string ResolveUnitLabel(GestionaleArticlePricingDetail? pricingDetail)
    {
        if (pricingDetail is null)
        {
            return "PZ";
        }

        if (!pricingDetail.HasSecondaryUnit)
        {
            return pricingDetail.UnitaMisuraPrincipale;
        }

        return $"{pricingDetail.UnitaMisuraPrincipale} / {pricingDetail.UnitaMisuraSecondaria} x{pricingDetail.MoltiplicatoreUnitaSecondaria.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"))}";
    }

    private static string ResolveSalesConstraintLabel(GestionaleArticlePricingDetail? pricingDetail)
    {
        if (pricingDetail is null)
        {
            return "Nessun vincolo disponibile.";
        }

        var minimo = pricingDetail.QuantitaMinimaVendita.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
        var multiplo = pricingDetail.QuantitaMultiplaVendita.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
        return $"Minimo {minimo} | Multiplo {multiplo}";
    }

    private void SetArticleTags(IReadOnlyList<string>? tags)
    {
        ArticleTags.Clear();

        if (tags is null)
        {
            return;
        }

        foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ArticleTags.Add(tag);
        }
    }

    private async Task CacheArticleTagsAsync(
        int articoloOid,
        IReadOnlyList<string> tags)
    {
        if (articoloOid <= 0)
        {
            return;
        }

        await _articleTagRepository.SaveTagsAsync(articoloOid, tags);
    }

    private async Task RefreshArticleTagSuggestionsAsync(string? searchText = null)
    {
        try
        {
            var suggestions = await _articleTagRepository.GetSuggestedTagsAsync(searchText);
            var currentTags = ArticleTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
            ReplaceCollection(
                ArticleTagSuggestions,
                suggestions.Where(tag => !currentTags.Contains(tag)).ToArray());
        }
        catch
        {
            // La cache suggerimenti non deve bloccare la scheda articolo.
        }
    }

    private async Task<string?> ResolveArticleCoverImagePathAsync(
        int articoloOid,
        string? fallbackImagePath)
    {
        if (articoloOid <= 0)
        {
            return fallbackImagePath;
        }

        var images = await _articleReadService.GetArticleImagesAsync(articoloOid);
        return images.FirstOrDefault(image => image.Predefinita && !string.IsNullOrWhiteSpace(image.LocalPath))?.LocalPath
            ?? images.FirstOrDefault(image => !string.IsNullOrWhiteSpace(image.LocalPath))?.LocalPath
            ?? fallbackImagePath;
    }

    private async Task RemoveArticleTagAsync(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || CurrentArticleOid is not { } articoloOid || IsLoading)
        {
            return;
        }

        var currentTags = ArticleTags.ToList();
        var tagToRemove = tag.Trim();
        var remainingTags = currentTags
            .Where(item => !string.Equals(item, tagToRemove, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (remainingTags.Length == currentTags.Count)
        {
            return;
        }

        SetArticleTags(remainingTags);
        TagsLabel = ResolveTagsLabelFromTags(remainingTags);

        try
        {
            await SaveArticleTagsToLegacyAndCacheAsync(articoloOid, remainingTags);
            StatusMessage = remainingTags.Length == 0
                ? "Tag articolo aggiornati su FM."
                : $"Tag '{tagToRemove}' rimosso e salvato su FM.";
        }
        catch (Exception ex)
        {
            SetArticleTags(currentTags);
            TagsLabel = ResolveTagsLabelFromTags(currentTags);
            StatusMessage = $"Impossibile aggiornare i tag su FM: {ex.Message}";
        }
    }

    private async Task AddArticleTagAsync()
    {
        if (!CanAddArticleTag() || CurrentArticleOid is not { } articoloOid)
        {
            return;
        }

        var currentTags = ArticleTags.ToList();
        var tagToAdd = NewArticleTagText.Trim();
        var updatedTags = currentTags
            .Append(tagToAdd)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SetArticleTags(updatedTags);
        TagsLabel = ResolveTagsLabelFromTags(updatedTags);
        NewArticleTagText = string.Empty;

        try
        {
            await SaveArticleTagsToLegacyAndCacheAsync(articoloOid, updatedTags);
            StatusMessage = $"Tag '{tagToAdd}' salvato su FM.";
        }
        catch (Exception ex)
        {
            SetArticleTags(currentTags);
            TagsLabel = ResolveTagsLabelFromTags(currentTags);
            NewArticleTagText = tagToAdd;
            StatusMessage = $"Impossibile salvare il tag su FM: {ex.Message}";
        }
    }

    private bool CanAddArticleTag()
    {
        var tag = NewArticleTagText.Trim();
        return HasSelectedArticle &&
               !IsLoading &&
               !string.IsNullOrWhiteSpace(tag) &&
               !ArticleTags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveArticleTagsToLegacyAndCacheAsync(
        int articoloOid,
        IReadOnlyList<string> tags)
    {
        await _articleWriteService.SaveArticleTagsAsync(articoloOid, tags);
        await CacheArticleTagsAsync(articoloOid, tags);
        await RefreshArticleTagSuggestionsAsync();
    }

    private static string ResolveTagsLabelFromTags(IReadOnlyList<string> tags)
    {
        var normalizedTags = tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();
        return normalizedTags.Length == 0
            ? string.Empty
            : string.Join(" | ", normalizedTags);
    }

    private static string ResolveEcommerceAvailabilityLabel(GestionaleArticleLookupDetail? lookupDetail)
    {
        if (lookupDetail is null)
        {
            return "-";
        }

        if (!string.IsNullOrWhiteSpace(lookupDetail.DisponibilitaOnlineLabel))
        {
            return lookupDetail.DisponibilitaOnlineLabel;
        }

        return lookupDetail.DisponibilitaOnlineOid.HasValue
            ? $"Codice {lookupDetail.DisponibilitaOnlineOid.Value}"
            : "-";
    }

    private static string ResolveConditionLabel(int? conditionCode)
    {
        if (!conditionCode.HasValue)
        {
            return "-";
        }

        return $"Codice {conditionCode.Value}";
    }

    private static string FormatTaxMultiplierLabel(decimal? value, bool usesTax)
    {
        if (!usesTax)
        {
            return "-";
        }

        return value.HasValue
            ? value.Value.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"))
            : "-";
    }

    private static string FormatWarrantyMonthsLabel(int? value) =>
        value.HasValue && value.Value >= 0
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "0";

    private static string FormatSoldQuantityLabel(decimal value) =>
        value.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));

    private static string FormatDecimalLabel(decimal value) =>
        value > 0
            ? value.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"))
            : "0";

    private static string NormalizeLabel(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();

    private static string NormalizeMultilineText(string? htmlText)
    {
        if (string.IsNullOrWhiteSpace(htmlText))
        {
            return "Nessun contenuto disponibile.";
        }

        var decoded = WebUtility.HtmlDecode(htmlText);
        var withoutHtml = HtmlRegex.Replace(decoded, " ");
        var normalized = Regex.Replace(withoutHtml, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? "Nessun contenuto disponibile."
            : normalized;
    }
}
