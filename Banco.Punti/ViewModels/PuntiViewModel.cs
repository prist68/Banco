using System.Collections.ObjectModel;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Customers;
using Banco.Vendita.Configuration;
using Banco.Vendita.Points;

namespace Banco.Punti.ViewModels;

public sealed class PuntiViewModel : BindableBase
{
    private static readonly IReadOnlyList<InlinePickerOption> RewardTypeOptionsInternal =
    [
        new("ScontoFisso", "Sconto fisso (EUR)"),
        new("ScontoPercentuale", "Sconto percentuale (%)"),
        new("ArticoloPremio", "Articolo premio")
    ];
    private static readonly IReadOnlyList<string> BaseCalculationOptionsInternal =
    [
        "Su totale documento",
        "Sul singolo articolo",
        "Su totale imponibile",
        "Su valore ivato"
    ];

    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IGestionaleFidelityHistoryService _fidelityHistoryService;
    private readonly IGestionalePointsReadService _pointsReadService;
    private readonly IGestionalePointsWriteService _pointsWriteService;
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IPointsRewardRuleService _rewardRuleService;
    private readonly IPointsCustomerBalanceService _balanceService;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly HashSet<Guid> _checkedRewardRuleIds = [];
    private string _searchText = string.Empty;
    private string _rewardArticleSearchText = string.Empty;
    private string _statusMessage = "Modulo punti pronto.";
    private bool _isLoading;
    private bool _isRewardTypePickerOpen;
    private string _rewardTypePickerSearchText = string.Empty;
    private CancellationTokenSource? _customerSearchDebounceCts;
    private CancellationTokenSource? _articleSearchDebounceCts;
    private GestionaleCustomerSummary? _selectedCustomer;
    private GestionalePointsCampaignSummary? _selectedCampaignSummary;
    private bool _suppressSelectedCampaignAutoLoad;
    private GestionalePointsCampaignEditModel? _editedCampaign;
    private PointsRewardRule? _selectedRewardRule;
    private PointsCustomerRewardSummary _customerRewardSummary = new();
    private GestionaleArticleSearchResult? _selectedRewardArticleSearchResult;
    private FidelityCustomerHistory? _fidelityCustomerHistory;
    private FidelityHistoryEntry? _selectedFidelityHistoryEntry;
    private InlinePickerOption? _selectedRewardTypeOption;

    public event Action? PromotionsConfigurationSaved;
    public event Action<int>? OpenDocumentInBancoRequested;

    public PuntiViewModel(
        IApplicationConfigurationService configurationService,
        IGestionaleCustomerReadService customerReadService,
        IGestionaleFidelityHistoryService fidelityHistoryService,
        IGestionalePointsReadService pointsReadService,
        IGestionalePointsWriteService pointsWriteService,
        IGestionaleArticleReadService articleReadService,
        IPointsRewardRuleService rewardRuleService,
        IPointsCustomerBalanceService balanceService)
    {
        _configurationService = configurationService;
        _customerReadService = customerReadService;
        _fidelityHistoryService = fidelityHistoryService;
        _pointsReadService = pointsReadService;
        _pointsWriteService = pointsWriteService;
        _articleReadService = articleReadService;
        _rewardRuleService = rewardRuleService;
        _balanceService = balanceService;
        _configurationService.SettingsChanged += OnSettingsChanged;

        SearchCustomerCommand = new RelayCommand(() => _ = SearchCustomersAsync());
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
        RefreshFidelityHistoryCommand = new RelayCommand(() => _ = LoadFidelityHistoryAsync());
        RecalculateSelectedFidelityBalanceCommand = new RelayCommand(
            () => _ = RecalculateSelectedFidelityBalanceAsync(),
            () => SelectedCustomer?.Oid is > 0 && SelectedCustomer.HaRaccoltaPunti == true);
        RecalculateAllFidelityBalancesCommand = new RelayCommand(() => _ = RecalculateAllFidelityBalancesAsync());
        NewCampaignCommand = new RelayCommand(CreateNewCampaign);
        SaveCampaignCommand = new RelayCommand(() => _ = SaveCampaignAsync());
        CancelCampaignCommand = new RelayCommand(() => _ = CancelCampaignAsync(), () => EditedCampaign is not null);
        AddRewardRuleCommand = new RelayCommand(AddRewardRule, () => EditedCampaign is not null);
        EditRewardRuleCommand = new RelayCommand(EditSelectedRewardRule, () => SelectedRewardRule is not null);
        DeleteRewardRuleCommand = new RelayCommand(DeleteSelectedRewardRule, CanDeleteRewardRule);
        DuplicateRewardRuleCommand = new RelayCommand(DuplicateSelectedRewardRule, CanDuplicateRewardRule);
        ClearRewardArticleCommand = new RelayCommand(ClearSelectedRewardArticle, () => HasSelectedRewardArticle);

        _ = LoadAsync();
    }

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        if (!e.GestionaleDatabaseChanged)
        {
            return;
        }

        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    public string Titolo => "Raccolta Punti";

    public string Sottotitolo => "Campagna punti nativa del gestionale e regole premio locali del nuovo sistema.";

    public ObservableCollection<GestionaleCustomerSummary> CustomerResults { get; } = [];

    public ObservableCollection<GestionalePointsCampaignSummary> Campaigns { get; } = [];

    public ObservableCollection<PointsRewardRule> RewardRules { get; } = [];

    public ObservableCollection<GestionaleArticleSearchResult> RewardArticleResults { get; } = [];

    public IReadOnlyList<InlinePickerOption> RewardTypeOptions => RewardTypeOptionsInternal;

    public IReadOnlyList<string> BaseCalculationOptions => BaseCalculationOptionsInternal;

    public ObservableCollection<InlinePickerOption> FilteredRewardTypeOptions { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                _customerSearchDebounceCts?.Cancel();
                CustomerResults.Clear();
                SelectedCustomer = null;
                StatusMessage = "Inserire un testo per simulare un cliente.";
                return;
            }

            _ = ScheduleCustomerSearchAsync();
        }
    }

    public string RewardArticleSearchText
    {
        get => _rewardArticleSearchText;
        set
        {
            if (!SetProperty(ref _rewardArticleSearchText, value))
            {
                return;
            }

            if (!SelectedRuleIsArticleReward)
            {
                RewardArticleResults.Clear();
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                _articleSearchDebounceCts?.Cancel();
                RewardArticleResults.Clear();
                _selectedRewardArticleSearchResult = null;
                return;
            }

            _ = ScheduleRewardArticleSearchAsync();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsRewardTypePickerOpen
    {
        get => _isRewardTypePickerOpen;
        set => SetProperty(ref _isRewardTypePickerOpen, value);
    }

    public string RewardTypePickerSearchText
    {
        get => _rewardTypePickerSearchText;
        set
        {
            if (!SetProperty(ref _rewardTypePickerSearchText, value))
            {
                return;
            }

            RefreshRewardTypeOptions();
        }
    }

    public GestionaleCustomerSummary? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                NotifyPropertyChanged(nameof(SelectedCustomerCardCodeLabel));
                NotifyPropertyChanged(nameof(SelectedCustomerLoyaltyLabel));
                RefreshCustomerRewardSummary();
                _ = LoadFidelityHistoryAsync();
                RecalculateSelectedFidelityBalanceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GestionalePointsCampaignSummary? SelectedCampaignSummary
    {
        get => _selectedCampaignSummary;
        set
        {
            if (SetProperty(ref _selectedCampaignSummary, value))
            {
                if (!_suppressSelectedCampaignAutoLoad)
                {
                    _ = LoadCampaignDetailsAsync(value);
                }
            }
        }
    }

    public GestionalePointsCampaignEditModel? EditedCampaign
    {
        get => _editedCampaign;
        private set
        {
            if (SetProperty(ref _editedCampaign, value))
            {
                NotifyPropertyChanged(nameof(EditedCampaignTitle));
                NotifyPropertyChanged(nameof(CanEditRewardRules));
                NotifyPropertyChanged(nameof(SelectedBaseCalculation));
                RaiseRewardRuleCommandStates();
            }
        }
    }

    public PointsRewardRule? SelectedRewardRule
    {
        get => _selectedRewardRule;
        set
        {
            if (!SetProperty(ref _selectedRewardRule, value))
            {
                return;
            }

            _selectedRewardArticleSearchResult = null;
            if (!SelectedRuleIsArticleReward)
            {
                RewardArticleSearchText = string.Empty;
                RewardArticleResults.Clear();
            }

            NotifyRewardRuleEditorChanged();
            RefreshCustomerRewardSummary();
            RaiseRewardRuleCommandStates();
        }
    }

    public GestionaleArticleSearchResult? SelectedRewardArticleSearchResult
    {
        get => _selectedRewardArticleSearchResult;
        set
        {
            if (!SetProperty(ref _selectedRewardArticleSearchResult, value) || value is null || SelectedRewardRule is null)
            {
                return;
            }

            SelectedRewardRule.ApplyArticle(value);
            RewardArticleSearchText = string.Empty;
            RewardArticleResults.Clear();
            NotifyRewardRuleEditorChanged();
            RefreshCustomerRewardSummary();
            StatusMessage = $"Articolo premio selezionato: {value.DisplayLabel}.";
        }
    }

    public PointsCustomerRewardSummary CustomerRewardSummary
    {
        get => _customerRewardSummary;
        private set
        {
            if (SetProperty(ref _customerRewardSummary, value))
            {
                NotifyPropertyChanged(nameof(HistoricalPointsLabel));
                NotifyPropertyChanged(nameof(CurrentDocumentPointsLabel));
                NotifyPropertyChanged(nameof(TotalAvailablePointsLabel));
                NotifyPropertyChanged(nameof(RequiredPointsLabel));
                NotifyPropertyChanged(nameof(RewardDistanceLabel));
                NotifyPropertyChanged(nameof(RewardStatusLabel));
                NotifyPropertyChanged(nameof(RewardConfigurationLabel));
                NotifyPropertyChanged(nameof(SelectedRuleDisplayName));
            }
        }
    }

    public string EditedCampaignTitle => EditedCampaign is null
        ? "Nessuna campagna selezionata"
        : EditedCampaign.IsNuovo
            ? "Nuova campagna punti"
            : $"Campagna punti #{EditedCampaign.Oid}";

    public bool CanEditRewardRules => EditedCampaign is not null;

    public bool HasSelectedRewardRule => SelectedRewardRule is not null;

    public bool SelectedRuleIsFixedDiscount => SelectedRewardRule?.RewardType == PointsRewardType.ScontoFisso;

    public bool SelectedRuleIsPercentDiscount => SelectedRewardRule?.RewardType == PointsRewardType.ScontoPercentuale;

    public bool SelectedRuleIsArticleReward => SelectedRewardRule?.RewardType == PointsRewardType.ArticoloPremio;

    public bool HasSelectedRewardArticle => SelectedRewardRule?.RewardArticleOid.GetValueOrDefault() > 0;

    public string HistoricalPointsLabel => CustomerRewardSummary.HistoricalPoints.ToString("N2");

    public string CurrentDocumentPointsLabel => CustomerRewardSummary.CurrentDocumentPoints.ToString("N2");

    public string TotalAvailablePointsLabel => CustomerRewardSummary.TotalAvailablePoints.ToString("N2");

    public string SelectedCustomerCardCodeLabel => SelectedCustomer?.CodiceCartaFedelta ?? string.Empty;

    public string SelectedCustomerLoyaltyLabel => SelectedCustomer is null
        ? "Seleziona un soggetto per verificare la carta fedeltà."
        : SelectedCustomer.HaRaccoltaPunti == true
            ? $"Carta fedeltà {SelectedCustomerCardCodeLabel}"
            : "Cliente non agganciato alla carta fedeltà";

    public string RequiredPointsLabel => CustomerRewardSummary.RequiredPoints > 0
        ? CustomerRewardSummary.RequiredPoints.ToString("N2")
        : "n.d.";

    public string RewardDistanceLabel => CustomerRewardSummary.RequiredPoints <= 0
        ? "Nessuna soglia definita"
        : CustomerRewardSummary.MissingPoints <= 0
            ? "Premio disponibile"
            : $"{CustomerRewardSummary.MissingPoints:N2} punti mancanti";

    public string RewardStatusLabel => string.IsNullOrWhiteSpace(CustomerRewardSummary.StatusLabel)
        ? "Nessuna promo configurata"
        : CustomerRewardSummary.StatusLabel;

    public string RewardConfigurationLabel => string.IsNullOrWhiteSpace(CustomerRewardSummary.RewardDescription)
        ? "Premio non configurato"
        : CustomerRewardSummary.RewardDescription;

    public ObservableCollection<FidelityHistoryEntry> FidelityHistoryEntries { get; } = [];

    public FidelityHistoryEntry? SelectedFidelityHistoryEntry
    {
        get => _selectedFidelityHistoryEntry;
        set
        {
            if (SetProperty(ref _selectedFidelityHistoryEntry, value))
            {
                NotifyPropertyChanged(nameof(FidelityHistoryDetailText));
            }
        }
    }

    public string FidelityCardCodeLabel => _fidelityCustomerHistory?.CardCode ?? SelectedCustomer?.CodiceCartaFedelta ?? string.Empty;

    public string FidelityInitialPointsLabel => _fidelityCustomerHistory?.InitialPoints.ToString("N0") ?? "0";

    public string FidelityLegacyCurrentPointsLabel => _fidelityCustomerHistory?.LegacyCurrentPoints.ToString("N0") ?? "0";

    public string FidelityComputedCurrentPointsLabel => _fidelityCustomerHistory?.ComputedCurrentPoints.ToString("N0") ?? "0";

    public string FidelityDeltaPointsLabel => _fidelityCustomerHistory?.DeltaPoints.ToString("N0") ?? "0";

    public string FidelityHistorySummaryLabel => _fidelityCustomerHistory is null
        ? "Seleziona un cliente fidelity per leggere i movimenti."
        : $"Movimenti Banco: {_fidelityCustomerHistory.Entries.Count} | Legacy {_fidelityCustomerHistory.LegacyCurrentPoints:N0} | Ricalcolato {_fidelityCustomerHistory.ComputedCurrentPoints:N0}";

    public string FidelityHistoryDetailText => SelectedFidelityHistoryEntry?.DetailLines ?? "Seleziona un movimento per vedere il dettaglio.";

    public string SelectedRuleDisplayName => !string.IsNullOrWhiteSpace(SelectedRewardRule?.RuleName)
        ? SelectedRewardRule.RuleName
        : !string.IsNullOrWhiteSpace(CustomerRewardSummary.RuleName)
            ? CustomerRewardSummary.RuleName
            : "Nessuna regola selezionata";

    public string SelectedRewardArticleLabel => SelectedRewardRule?.RewardArticleOid.GetValueOrDefault() > 0
        ? $"{SelectedRewardRule.RewardArticleCode} - {SelectedRewardRule.RewardArticleDescription}"
        : "Nessun articolo premio selezionato";

    public string RewardRulesSummary
    {
        get
        {
            if (RewardRules.Count == 0)
            {
                return "Nessuna regola premio.";
            }

            if (CheckedRewardRuleCount > 0)
            {
                return $"Regole premio: {RewardRules.Count}. Selezionate: {CheckedRewardRuleCount}.";
            }

            if (SelectedRewardRule is null)
            {
                return $"Regole premio: {RewardRules.Count}.";
            }

            var index = RewardRules.IndexOf(SelectedRewardRule);
            return index >= 0
                ? $"Record {index + 1} di {RewardRules.Count}."
                : $"Regole premio: {RewardRules.Count}.";
        }
    }

    public int CheckedRewardRuleCount => _checkedRewardRuleIds.Count;

    public string? SelectedBaseCalculation
    {
        get => EditedCampaign?.BaseCalcolo;
        set
        {
            if (EditedCampaign is null || string.Equals(EditedCampaign.BaseCalcolo, value, StringComparison.Ordinal))
            {
                return;
            }

            EditedCampaign.BaseCalcolo = string.IsNullOrWhiteSpace(value) ? null : value;
            NotifyPropertyChanged();
        }
    }

    public string EditedRuleName
    {
        get => SelectedRewardRule?.RuleName ?? string.Empty;
        set
        {
            if (SelectedRewardRule is null || SelectedRewardRule.RuleName == value)
            {
                return;
            }

            SelectedRewardRule.RuleName = value;
            NotifyRewardRuleCollectionChanged();
        }
    }

    public bool EditedRuleActive
    {
        get => SelectedRewardRule?.IsActive ?? false;
        set
        {
            if (SelectedRewardRule is null || SelectedRewardRule.IsActive == value)
            {
                return;
            }

            SelectedRewardRule.IsActive = value;
            NotifyRewardRuleCollectionChanged();
        }
    }

    public decimal? EditedRuleRequiredPoints
    {
        get => SelectedRewardRule?.RequiredPoints;
        set
        {
            if (SelectedRewardRule is null || SelectedRewardRule.RequiredPoints == value)
            {
                return;
            }

            SelectedRewardRule.RequiredPoints = value;
            NotifyRewardRuleCollectionChanged();
            RefreshCustomerRewardSummary();
        }
    }

    public string SelectedRewardTypeName
    {
        get => SelectedRewardRule?.RewardType.ToString() ?? RewardTypeOptionsInternal[0].Key;
        set
        {
            if (SelectedRewardRule is null)
            {
                return;
            }

            var rewardType = value switch
            {
                "ScontoPercentuale" => PointsRewardType.ScontoPercentuale,
                "ArticoloPremio" => PointsRewardType.ArticoloPremio,
                _ => PointsRewardType.ScontoFisso
            };

            if (SelectedRewardRule.RewardType == rewardType)
            {
                return;
            }

            SelectedRewardRule.RewardType = rewardType;
            if (rewardType != PointsRewardType.ScontoFisso)
            {
                SelectedRewardRule.DiscountAmount = null;
            }

            if (rewardType != PointsRewardType.ScontoPercentuale)
            {
                SelectedRewardRule.DiscountPercent = null;
            }

            if (rewardType != PointsRewardType.ArticoloPremio)
            {
                SelectedRewardRule.ClearArticle();
                RewardArticleSearchText = string.Empty;
                RewardArticleResults.Clear();
            }

            NotifyRewardRuleEditorChanged();
            NotifyRewardRuleCollectionChanged();
        }
    }

    public InlinePickerOption? SelectedRewardTypeOption
    {
        get => _selectedRewardTypeOption;
        set
        {
            if (!SetProperty(ref _selectedRewardTypeOption, value) || value is null)
            {
                return;
            }

            SelectedRewardTypeName = value.Key;
            if (!string.IsNullOrWhiteSpace(RewardTypePickerSearchText))
            {
                RewardTypePickerSearchText = string.Empty;
            }

            IsRewardTypePickerOpen = false;
            NotifyPropertyChanged(nameof(SelectedRewardTypeLabel));
        }
    }

    public string SelectedRewardTypeLabel
    {
        get
        {
            var key = SelectedRewardTypeName;
            return RewardTypeOptionsInternal.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal))?.Label
                ?? RewardTypeOptionsInternal[0].Label;
        }
    }

    public decimal? EditedRuleDiscountAmount
    {
        get => SelectedRewardRule?.DiscountAmount;
        set
        {
            if (SelectedRewardRule is null || SelectedRewardRule.DiscountAmount == value)
            {
                return;
            }

            SelectedRewardRule.DiscountAmount = value;
            NotifyRewardRuleCollectionChanged();
        }
    }

    public decimal? EditedRuleDiscountPercent
    {
        get => SelectedRewardRule?.DiscountPercent;
        set
        {
            if (SelectedRewardRule is null || SelectedRewardRule.DiscountPercent == value)
            {
                return;
            }

            SelectedRewardRule.DiscountPercent = value;
            NotifyRewardRuleCollectionChanged();
        }
    }

    public decimal EditedRuleRewardQuantity
    {
        get => SelectedRewardRule?.RewardQuantity ?? 1m;
        set
        {
            if (SelectedRewardRule is null)
            {
                return;
            }

            var normalized = value <= 0 ? 1m : value;
            if (SelectedRewardRule.RewardQuantity == normalized)
            {
                return;
            }

            SelectedRewardRule.RewardQuantity = normalized;
            NotifyRewardRuleCollectionChanged();
        }
    }

    public bool EditedRuleEnableSaleCheck
    {
        get => SelectedRewardRule?.EnableSaleCheck ?? false;
        set
        {
            if (SelectedRewardRule is null || SelectedRewardRule.EnableSaleCheck == value)
            {
                return;
            }

            SelectedRewardRule.EnableSaleCheck = value;
            NotifyRewardRuleCollectionChanged();
        }
    }

    public string EditedRuleNotes
    {
        get => SelectedRewardRule?.Notes ?? string.Empty;
        set
        {
            if (SelectedRewardRule is null || string.Equals(SelectedRewardRule.Notes, value, StringComparison.Ordinal))
            {
                return;
            }

            SelectedRewardRule.Notes = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            NotifyRewardRuleCollectionChanged();
        }
    }

    public RelayCommand SearchCustomerCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand RefreshFidelityHistoryCommand { get; }

    public RelayCommand RecalculateSelectedFidelityBalanceCommand { get; }

    public RelayCommand RecalculateAllFidelityBalancesCommand { get; }

    public RelayCommand NewCampaignCommand { get; }

    public RelayCommand SaveCampaignCommand { get; }

    public RelayCommand CancelCampaignCommand { get; }

    public RelayCommand AddRewardRuleCommand { get; }

    public RelayCommand EditRewardRuleCommand { get; }

    public RelayCommand DeleteRewardRuleCommand { get; }

    public RelayCommand DuplicateRewardRuleCommand { get; }

    public RelayCommand ClearRewardArticleCommand { get; }

    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var campaigns = await _pointsReadService.GetCampaignsAsync();
            ReplaceCollection(Campaigns, campaigns);

            if (Campaigns.Count > 0)
            {
                SelectedCampaignSummary = Campaigns.FirstOrDefault(c => c.Attiva == true) ?? Campaigns[0];
            }
            else
            {
                SelectedCampaignSummary = null;
                EditedCampaign = null;
                RewardRules.Clear();
                SelectedRewardRule = null;
                ClearFidelityHistory();
            }

            StatusMessage = Campaigns.Count > 0
                ? $"Caricate {Campaigns.Count} campagne punti."
                : "Nessuna campagna punti trovata nel gestionale.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore caricamento punti: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCampaignDetailsAsync(GestionalePointsCampaignSummary? campaign)
    {
        if (campaign is null)
        {
            EditedCampaign = null;
            RewardRules.Clear();
            SelectedRewardRule = null;
            ClearCheckedRewardRules();
            RefreshCustomerRewardSummary();
            return;
        }

        EditedCampaign = new GestionalePointsCampaignEditModel
        {
            Oid = campaign.Oid,
            NomeOperazione = campaign.NomeOperazione,
            Inizio = campaign.Inizio,
            Fine = campaign.Fine,
            Attiva = campaign.Attiva,
            EuroPerPunto = campaign.EuroPerPunto,
            BaseCalcolo = NormalizeBaseCalcolo(campaign.BaseCalcolo),
            ImportoMinimo = campaign.ImportoMinimo,
            CalcolaSuValoreIva = campaign.CalcolaSuValoreIva
        };

        var rewardRules = await _rewardRuleService.GetAsync(campaign.Oid);
        ReplaceCollection(RewardRules, rewardRules);
        SelectedRewardRule = null;
        ClearCheckedRewardRules();
        RewardArticleSearchText = string.Empty;
        RewardArticleResults.Clear();
        RefreshCustomerRewardSummary();
        NotifyPropertyChanged(nameof(RewardRulesSummary));
    }

    private void CreateNewCampaign()
    {
        EditedCampaign = new GestionalePointsCampaignEditModel
        {
            Attiva = true,
            BaseCalcolo = "Su totale documento",
            CalcolaSuValoreIva = true,
            EuroPerPunto = 3m,
            Fine = DateTime.Today.AddYears(5),
            ImportoMinimo = 0m,
            Inizio = DateTime.Today,
            NomeOperazione = "Nuova raccolta punti"
        };

        SelectedCampaignSummary = null;
        RewardRules.Clear();
        SelectedRewardRule = null;
        ClearCheckedRewardRules();
        AddRewardRule();
        StatusMessage = "Nuova campagna punti pronta. Aggiungi o modifica le regole premio locali.";
        NotifyPropertyChanged(nameof(RewardRulesSummary));
    }

    private async Task SaveCampaignAsync()
    {
        if (EditedCampaign is null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var savedOid = await _pointsWriteService.SaveCampaignAsync(EditedCampaign);

            foreach (var rule in RewardRules)
            {
                rule.CampaignOid = savedOid;
            }

            await _rewardRuleService.SaveAsync(savedOid, RewardRules.ToList());
            await LoadAsync();
            var savedCampaign = Campaigns.FirstOrDefault(c => c.Oid == savedOid);
            if (savedCampaign is not null)
            {
                _suppressSelectedCampaignAutoLoad = true;
                SelectedCampaignSummary = savedCampaign;
                _suppressSelectedCampaignAutoLoad = false;
                await LoadCampaignDetailsAsync(savedCampaign);
            }

            StatusMessage = "Campagna punti e regole premio salvate.";
            PromotionsConfigurationSaved?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore salvataggio campagna: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CancelCampaignAsync()
    {
        if (EditedCampaign is null)
        {
            return;
        }

        if (EditedCampaign.IsNuovo)
        {
            EditedCampaign = null;
            RewardRules.Clear();
            SelectedRewardRule = null;
            ClearCheckedRewardRules();
            StatusMessage = "Creazione campagna annullata.";
            return;
        }

        IsLoading = true;
        try
        {
            await _pointsWriteService.CancelCampaignAsync(EditedCampaign.Oid);
            await LoadAsync();
            StatusMessage = "Campagna punti annullata.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore annullo campagna: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddRewardRule()
    {
        var campaignOid = EditedCampaign?.Oid ?? SelectedCampaignSummary?.Oid ?? 0;
        var newRule = new PointsRewardRule
        {
            CampaignOid = campaignOid,
            RuleName = $"Regola premio {RewardRules.Count + 1}",
            IsActive = true,
            RewardType = PointsRewardType.ScontoFisso,
            RewardQuantity = 1,
            EnableSaleCheck = true
        };

        RewardRules.Add(newRule);
        ClearCheckedRewardRules();
        SelectedRewardRule = newRule;
        StatusMessage = "Nuova regola premio aggiunta.";
        NotifyPropertyChanged(nameof(RewardRulesSummary));
    }

    private void EditSelectedRewardRule()
    {
        if (SelectedRewardRule is null)
        {
            return;
        }

        NotifyRewardRuleEditorChanged();
        StatusMessage = $"Modifica diretta della regola '{SelectedRewardRule.RuleName}'.";
    }

    private void DeleteSelectedRewardRule()
    {
        var rulesToRemove = GetTargetRewardRules().ToList();
        if (rulesToRemove.Count == 0)
        {
            return;
        }

        foreach (var rule in rulesToRemove)
        {
            RewardRules.Remove(rule);
            _checkedRewardRuleIds.Remove(rule.Id);
        }

        SelectedRewardRule = RewardRules.FirstOrDefault();
        StatusMessage = rulesToRemove.Count > 1
            ? $"Rimosse {rulesToRemove.Count} regole premio selezionate."
            : $"Regola '{rulesToRemove[0].RuleName}' rimossa.";
        RefreshCustomerRewardSummary();
        NotifyPropertyChanged(nameof(RewardRulesSummary));
        RaiseRewardRuleCommandStates();
    }

    private void DuplicateSelectedRewardRule()
    {
        var sourceRules = GetTargetRewardRules().ToList();
        if (sourceRules.Count == 0)
        {
            return;
        }

        var duplicates = new List<PointsRewardRule>(sourceRules.Count);
        foreach (var sourceRule in sourceRules)
        {
            var duplicate = sourceRule.Clone();
            duplicate.Id = Guid.NewGuid();
            duplicate.RuleName = string.IsNullOrWhiteSpace(duplicate.RuleName)
                ? "Copia regola"
                : $"{duplicate.RuleName} - copia";
            RewardRules.Add(duplicate);
            duplicates.Add(duplicate);
        }

        ClearCheckedRewardRules();
        SelectedRewardRule = duplicates.LastOrDefault();
        StatusMessage = duplicates.Count > 1
            ? $"Create {duplicates.Count} copie delle regole selezionate."
            : $"Creata copia della regola '{duplicates[0].RuleName}'.";
        NotifyPropertyChanged(nameof(RewardRulesSummary));
        RaiseRewardRuleCommandStates();
    }

    private void ClearSelectedRewardArticle()
    {
        if (SelectedRewardRule is null)
        {
            return;
        }

        SelectedRewardRule.ClearArticle();
        _selectedRewardArticleSearchResult = null;
        RewardArticleSearchText = string.Empty;
        RewardArticleResults.Clear();
        NotifyRewardRuleEditorChanged();
        RefreshCustomerRewardSummary();
        StatusMessage = "Articolo premio rimosso dalla regola selezionata.";
    }

    private async Task SearchCustomersAsync(CancellationToken cancellationToken = default)
    {
        var normalizedSearch = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            CustomerResults.Clear();
            SelectedCustomer = null;
            return;
        }

        try
        {
            var customers = await _customerReadService.SearchCustomersAsync(normalizedSearch, 25, cancellationToken);
            ReplaceCollection(CustomerResults, customers);
            SelectedCustomer = CustomerResults.Count == 1 ? CustomerResults.FirstOrDefault() : null;
            StatusMessage = $"Trovati {CustomerResults.Count} soggetti.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore ricerca soggetti: {ex.Message}";
        }
    }

    private async Task SearchRewardArticlesAsync(CancellationToken cancellationToken = default)
    {
        if (!SelectedRuleIsArticleReward)
        {
            RewardArticleResults.Clear();
            return;
        }

        var normalizedSearch = RewardArticleSearchText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            RewardArticleResults.Clear();
            return;
        }

        try
        {
            var articles = await _articleReadService.SearchArticlesAsync(normalizedSearch, null, 20, cancellationToken);
            ReplaceCollection(RewardArticleResults, articles);
            StatusMessage = RewardArticleResults.Count > 0
                ? $"Trovati {RewardArticleResults.Count} articoli per il premio."
                : "Nessun articolo trovato per il premio.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore ricerca articolo premio: {ex.Message}";
        }
    }

    private async Task ScheduleCustomerSearchAsync()
    {
        _customerSearchDebounceCts?.Cancel();
        _customerSearchDebounceCts?.Dispose();
        _customerSearchDebounceCts = new CancellationTokenSource();
        var token = _customerSearchDebounceCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(280), token);
            if (!token.IsCancellationRequested)
            {
                await SearchCustomersAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ScheduleRewardArticleSearchAsync()
    {
        _articleSearchDebounceCts?.Cancel();
        _articleSearchDebounceCts?.Dispose();
        _articleSearchDebounceCts = new CancellationTokenSource();
        var token = _articleSearchDebounceCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(280), token);
            if (!token.IsCancellationRequested)
            {
                await SearchRewardArticlesAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RefreshCustomerRewardSummary()
    {
        var ruleSet = SelectedRewardRule is null
            ? RewardRules.ToList()
            : [SelectedRewardRule];

        CustomerRewardSummary = _balanceService.BuildSummary(
            SelectedCustomer,
            SelectedCampaignSummary,
            ruleSet,
            document: null);
    }

    private async Task LoadFidelityHistoryAsync()
    {
        if (SelectedCustomer?.Oid is not > 0 || SelectedCustomer.HaRaccoltaPunti != true)
        {
            ClearFidelityHistory();
            return;
        }

        try
        {
            var history = await _fidelityHistoryService.GetCustomerHistoryAsync(SelectedCustomer.Oid);
            _fidelityCustomerHistory = history;
            ReplaceCollection(FidelityHistoryEntries, history?.Entries ?? []);
            SelectedFidelityHistoryEntry = FidelityHistoryEntries.LastOrDefault();
            NotifyFidelityHistoryChanged();
        }
        catch (Exception ex)
        {
            ClearFidelityHistory();
            StatusMessage = $"Errore storico fidelity: {ex.Message}";
        }
    }

    private async Task RecalculateSelectedFidelityBalanceAsync()
    {
        if (SelectedCustomer?.Oid is not > 0 || SelectedCustomer.HaRaccoltaPunti != true)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _fidelityHistoryService.RecalculateCustomerBalanceAsync(
                SelectedCustomer.Oid,
                persistToLegacy: true,
                operatore: "Modulo Punti");
            await ReloadSelectedCustomerAsync(SelectedCustomer.Oid);
            await LoadFidelityHistoryAsync();

            if (result is null)
            {
                StatusMessage = "Cliente fidelity non trovato per il ricalcolo.";
                return;
            }

            StatusMessage = result.LegacyUpdated
                ? $"Saldo fidelity cliente {result.CustomerOid} riallineato: {result.PreviousLegacyCurrentPoints:N0} -> {result.ComputedCurrentPoints:N0}."
                : $"Saldo fidelity cliente {result.CustomerOid} gia' coerente a {result.ComputedCurrentPoints:N0}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore ricalcolo saldo cliente: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RecalculateAllFidelityBalancesAsync()
    {
        IsLoading = true;
        try
        {
            var results = await _fidelityHistoryService.RecalculateAllActiveCustomersAsync(
                persistToLegacy: true,
                operatore: "Modulo Punti");
            var updatedCount = results.Count(item => item.LegacyUpdated);
            var unchangedCount = results.Count - updatedCount;

            if (SelectedCustomer?.Oid is > 0)
            {
                await ReloadSelectedCustomerAsync(SelectedCustomer.Oid);
                await LoadFidelityHistoryAsync();
            }

            StatusMessage = $"Ricalcolo fidelity completato: {results.Count} clienti, {updatedCount} aggiornati, {unchangedCount} gia' coerenti.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore ricalcolo globale fidelity: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearFidelityHistory()
    {
        _fidelityCustomerHistory = null;
        FidelityHistoryEntries.Clear();
        SelectedFidelityHistoryEntry = null;
        NotifyFidelityHistoryChanged();
    }

    public void OpenSelectedFidelityDocumentInBanco(FidelityHistoryEntry? entry)
    {
        if (entry?.DocumentoOid is not > 0)
        {
            return;
        }

        SelectedFidelityHistoryEntry = entry;
        OpenDocumentInBancoRequested?.Invoke(entry.DocumentoOid);
        StatusMessage = $"Apertura vendita Banco {entry.DocumentoShortLabel} richiesta.";
    }

    private async Task ReloadSelectedCustomerAsync(int customerOid)
    {
        var refreshedCustomer = await _customerReadService.GetCustomerByOidAsync(customerOid);
        if (refreshedCustomer is null)
        {
            return;
        }

        SelectedCustomer = refreshedCustomer;
    }

    private void NotifyFidelityHistoryChanged()
    {
        NotifyPropertyChanged(nameof(FidelityCardCodeLabel));
        NotifyPropertyChanged(nameof(FidelityInitialPointsLabel));
        NotifyPropertyChanged(nameof(FidelityLegacyCurrentPointsLabel));
        NotifyPropertyChanged(nameof(FidelityComputedCurrentPointsLabel));
        NotifyPropertyChanged(nameof(FidelityDeltaPointsLabel));
        NotifyPropertyChanged(nameof(FidelityHistorySummaryLabel));
        NotifyPropertyChanged(nameof(FidelityHistoryDetailText));
    }

    private void NotifyRewardRuleCollectionChanged()
    {
        var currentRule = SelectedRewardRule;
        if (currentRule is null)
        {
            return;
        }

        currentRule.UpdatedAt = DateTimeOffset.Now;
        var index = RewardRules.IndexOf(currentRule);
        if (index >= 0)
        {
            RewardRules[index] = currentRule;
            SelectedRewardRule = RewardRules[index];
        }

        NotifyRewardRuleEditorChanged();
        RefreshCustomerRewardSummary();
    }

    private void NotifyRewardRuleEditorChanged()
    {
        SyncRewardTypeSelection();
        NotifyPropertyChanged(nameof(HasSelectedRewardRule));
        NotifyPropertyChanged(nameof(SelectedRuleIsFixedDiscount));
        NotifyPropertyChanged(nameof(SelectedRuleIsPercentDiscount));
        NotifyPropertyChanged(nameof(SelectedRuleIsArticleReward));
        NotifyPropertyChanged(nameof(HasSelectedRewardArticle));
        NotifyPropertyChanged(nameof(EditedRuleName));
        NotifyPropertyChanged(nameof(EditedRuleActive));
        NotifyPropertyChanged(nameof(EditedRuleRequiredPoints));
        NotifyPropertyChanged(nameof(SelectedRewardTypeName));
        NotifyPropertyChanged(nameof(SelectedRewardTypeLabel));
        NotifyPropertyChanged(nameof(EditedRuleDiscountAmount));
        NotifyPropertyChanged(nameof(EditedRuleDiscountPercent));
        NotifyPropertyChanged(nameof(EditedRuleRewardQuantity));
        NotifyPropertyChanged(nameof(EditedRuleEnableSaleCheck));
        NotifyPropertyChanged(nameof(EditedRuleNotes));
        NotifyPropertyChanged(nameof(SelectedRewardArticleLabel));
        NotifyPropertyChanged(nameof(RewardConfigurationLabel));
        NotifyPropertyChanged(nameof(RewardRulesSummary));
        RaiseRewardRuleCommandStates();
    }

    public bool IsRewardRuleChecked(PointsRewardRule? rule)
    {
        return rule is not null && _checkedRewardRuleIds.Contains(rule.Id);
    }

    public void SetRewardRuleChecked(PointsRewardRule? rule, bool isChecked)
    {
        if (rule is null)
        {
            return;
        }

        if (isChecked)
        {
            _checkedRewardRuleIds.Add(rule.Id);
        }
        else
        {
            _checkedRewardRuleIds.Remove(rule.Id);
        }

        NotifyPropertyChanged(nameof(RewardRulesSummary));
        RaiseRewardRuleCommandStates();
    }

    public void ClearCheckedRewardRules()
    {
        if (_checkedRewardRuleIds.Count == 0)
        {
            return;
        }

        _checkedRewardRuleIds.Clear();
        NotifyPropertyChanged(nameof(RewardRulesSummary));
        RaiseRewardRuleCommandStates();
    }

    private void RefreshRewardTypeOptions()
    {
        var filter = RewardTypePickerSearchText?.Trim() ?? string.Empty;
        var options = string.IsNullOrWhiteSpace(filter)
            ? RewardTypeOptionsInternal
            : RewardTypeOptionsInternal
                .Where(option => option.Label.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        ReplaceCollection(FilteredRewardTypeOptions, options);
    }

    private void SyncRewardTypeSelection()
    {
        RefreshRewardTypeOptions();

        var selectedKey = SelectedRewardTypeName;
        _selectedRewardTypeOption = RewardTypeOptionsInternal.FirstOrDefault(
            option => string.Equals(option.Key, selectedKey, StringComparison.Ordinal));
        NotifyPropertyChanged(nameof(SelectedRewardTypeOption));
    }

    private void RaiseRewardRuleCommandStates()
    {
        CancelCampaignCommand.RaiseCanExecuteChanged();
        AddRewardRuleCommand.RaiseCanExecuteChanged();
        EditRewardRuleCommand.RaiseCanExecuteChanged();
        DeleteRewardRuleCommand.RaiseCanExecuteChanged();
        DuplicateRewardRuleCommand.RaiseCanExecuteChanged();
        ClearRewardArticleCommand.RaiseCanExecuteChanged();
    }

    private bool CanDeleteRewardRule()
    {
        return CheckedRewardRuleCount > 0 || SelectedRewardRule is not null;
    }

    private bool CanDuplicateRewardRule()
    {
        return CheckedRewardRuleCount > 0 || SelectedRewardRule is not null;
    }

    private IEnumerable<PointsRewardRule> GetTargetRewardRules()
    {
        if (CheckedRewardRuleCount > 0)
        {
            return RewardRules.Where(rule => _checkedRewardRuleIds.Contains(rule.Id));
        }

        return SelectedRewardRule is null
            ? []
            : [SelectedRewardRule];
    }

    private static string? NormalizeBaseCalcolo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

}
