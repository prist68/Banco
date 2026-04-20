using System.Collections.ObjectModel;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Customers;
using Banco.Vendita.Configuration;
using Banco.Vendita.Points;

namespace Banco.Punti.ViewModels;

public sealed class PuntiViewModel : BindableBase
{
    private static readonly IReadOnlyList<string> RewardTypeOptionsInternal =
    [
        "ScontoFisso",
        "ScontoPercentuale",
        "ArticoloPremio"
    ];
    private static readonly IReadOnlyList<string> BaseCalculationOptionsInternal =
    [
        "Su totale documento",
        "Sul singolo articolo",
        "Su totale imponibile",
        "Su valore ivato"
    ];

    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IGestionalePointsReadService _pointsReadService;
    private readonly IGestionalePointsWriteService _pointsWriteService;
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IPointsRewardRuleService _rewardRuleService;
    private readonly IPointsCustomerBalanceService _balanceService;
    private readonly IApplicationConfigurationService _configurationService;
    private string _searchText = string.Empty;
    private string _rewardArticleSearchText = string.Empty;
    private string _statusMessage = "Modulo punti pronto.";
    private bool _isLoading;
    private CancellationTokenSource? _customerSearchDebounceCts;
    private CancellationTokenSource? _articleSearchDebounceCts;
    private GestionaleCustomerSummary? _selectedCustomer;
    private GestionalePointsCampaignSummary? _selectedCampaignSummary;
    private GestionalePointsCampaignEditModel? _editedCampaign;
    private PointsRewardRule? _selectedRewardRule;
    private PointsCustomerRewardSummary _customerRewardSummary = new();
    private GestionaleArticleSearchResult? _selectedRewardArticleSearchResult;

    public event Action? PromotionsConfigurationSaved;

    public PuntiViewModel(
        IApplicationConfigurationService configurationService,
        IGestionaleCustomerReadService customerReadService,
        IGestionalePointsReadService pointsReadService,
        IGestionalePointsWriteService pointsWriteService,
        IGestionaleArticleReadService articleReadService,
        IPointsRewardRuleService rewardRuleService,
        IPointsCustomerBalanceService balanceService)
    {
        _configurationService = configurationService;
        _customerReadService = customerReadService;
        _pointsReadService = pointsReadService;
        _pointsWriteService = pointsWriteService;
        _articleReadService = articleReadService;
        _rewardRuleService = rewardRuleService;
        _balanceService = balanceService;
        _configurationService.SettingsChanged += OnSettingsChanged;

        SearchCustomerCommand = new RelayCommand(() => _ = SearchCustomersAsync());
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
        NewCampaignCommand = new RelayCommand(CreateNewCampaign);
        SaveCampaignCommand = new RelayCommand(() => _ = SaveCampaignAsync());
        CancelCampaignCommand = new RelayCommand(() => _ = CancelCampaignAsync(), () => EditedCampaign is not null);
        AddRewardRuleCommand = new RelayCommand(AddRewardRule, () => EditedCampaign is not null);
        EditRewardRuleCommand = new RelayCommand(EditSelectedRewardRule, () => SelectedRewardRule is not null);
        DeleteRewardRuleCommand = new RelayCommand(DeleteSelectedRewardRule, () => SelectedRewardRule is not null);
        DuplicateRewardRuleCommand = new RelayCommand(DuplicateSelectedRewardRule, () => SelectedRewardRule is not null);
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

    public IReadOnlyList<string> RewardTypeOptions => RewardTypeOptionsInternal;

    public IReadOnlyList<string> BaseCalculationOptions => BaseCalculationOptionsInternal;

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
                _ = LoadCampaignDetailsAsync(value);
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

    public string SelectedRuleDisplayName => !string.IsNullOrWhiteSpace(SelectedRewardRule?.RuleName)
        ? SelectedRewardRule.RuleName
        : !string.IsNullOrWhiteSpace(CustomerRewardSummary.RuleName)
            ? CustomerRewardSummary.RuleName
            : "Nessuna regola selezionata";

    public string SelectedRewardArticleLabel => SelectedRewardRule?.RewardArticleOid.GetValueOrDefault() > 0
        ? $"{SelectedRewardRule.RewardArticleCode} - {SelectedRewardRule.RewardArticleDescription}"
        : "Nessun articolo premio selezionato";

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
        get => SelectedRewardRule?.RewardType.ToString() ?? RewardTypeOptionsInternal[0];
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
        SelectedRewardRule = RewardRules.FirstOrDefault();
        RewardArticleSearchText = string.Empty;
        RewardArticleResults.Clear();
        RefreshCustomerRewardSummary();
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
        AddRewardRule();
        StatusMessage = "Nuova campagna punti pronta. Aggiungi o modifica le regole premio locali.";
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
            SelectedCampaignSummary = Campaigns.FirstOrDefault(c => c.Oid == savedOid);
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
        SelectedRewardRule = newRule;
        StatusMessage = "Nuova regola premio aggiunta.";
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
        if (SelectedRewardRule is null)
        {
            return;
        }

        var ruleToRemove = SelectedRewardRule;
        var nextRule = RewardRules.FirstOrDefault(rule => !ReferenceEquals(rule, ruleToRemove));
        RewardRules.Remove(ruleToRemove);
        SelectedRewardRule = nextRule;
        StatusMessage = $"Regola '{ruleToRemove.RuleName}' rimossa.";
        RefreshCustomerRewardSummary();
    }

    private void DuplicateSelectedRewardRule()
    {
        if (SelectedRewardRule is null)
        {
            return;
        }

        var duplicate = SelectedRewardRule.Clone();
        duplicate.Id = Guid.NewGuid();
        duplicate.RuleName = string.IsNullOrWhiteSpace(duplicate.RuleName)
            ? "Copia regola"
            : $"{duplicate.RuleName} - copia";
        RewardRules.Add(duplicate);
        SelectedRewardRule = duplicate;
        StatusMessage = $"Creata copia della regola '{duplicate.RuleName}'.";
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
        NotifyPropertyChanged(nameof(HasSelectedRewardRule));
        NotifyPropertyChanged(nameof(SelectedRuleIsFixedDiscount));
        NotifyPropertyChanged(nameof(SelectedRuleIsPercentDiscount));
        NotifyPropertyChanged(nameof(SelectedRuleIsArticleReward));
        NotifyPropertyChanged(nameof(HasSelectedRewardArticle));
        NotifyPropertyChanged(nameof(EditedRuleName));
        NotifyPropertyChanged(nameof(EditedRuleActive));
        NotifyPropertyChanged(nameof(EditedRuleRequiredPoints));
        NotifyPropertyChanged(nameof(SelectedRewardTypeName));
        NotifyPropertyChanged(nameof(EditedRuleDiscountAmount));
        NotifyPropertyChanged(nameof(EditedRuleDiscountPercent));
        NotifyPropertyChanged(nameof(EditedRuleRewardQuantity));
        NotifyPropertyChanged(nameof(EditedRuleEnableSaleCheck));
        NotifyPropertyChanged(nameof(EditedRuleNotes));
        NotifyPropertyChanged(nameof(SelectedRewardArticleLabel));
        NotifyPropertyChanged(nameof(RewardConfigurationLabel));
        RaiseRewardRuleCommandStates();
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
