using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Banco.Core.Infrastructure;
using Banco.Riordino;
using Banco.Vendita.Documents;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;

namespace Banco.Magazzino.ViewModels;

public sealed class MagazzinoArticleViewModel : ViewModelBase
{
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IGestionaleArticleWriteService _articleWriteService;
    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IReorderArticleSettingsRepository _settingsRepository;
    private readonly IPosProcessLogService _logService;
    private CancellationTokenSource? _searchDebounceCts;
    private string _searchText = string.Empty;
    private string _statusMessage = "Cerca un articolo del legacy per configurare i parametri locali di riordino.";
    private bool _isLoading;
    private MagazzinoArticleSearchRowViewModel? _selectedSearchRow;
    private GestionaleArticleSearchResult? _selectedSearchResult;
    private int _articoloOid;
    private string _codiceArticolo = string.Empty;
    private string _descrizioneArticolo = string.Empty;
    private string _giacenzaLabel = "-";
    private string _listinoLegacyLabel = "-";
    private string _prezzoVenditaLabel = "-";
    private string _unitaPrincipaleLabel = "PZ";
    private string _unitaSecondariaLabel = "-";
    private string _rapportoUnitaLabel = "-";
    private string _quantitaMinimaVenditaLabel = "1";
    private string _quantitaMultiplaVenditaLabel = "1";
    private string _descrizioneLegacyText = string.Empty;
    private string _prezzoVenditaLegacyText = string.Empty;
    private string _unitaPrincipaleLegacyText = "PZ";
    private string _unitaSecondariaLegacyText = string.Empty;
    private string _rapportoUnitaLegacyText = string.Empty;
    private bool _unitLookupsLoaded;
    private bool _isPrimaryUnitPickerOpen;
    private bool _isSecondaryUnitPickerOpen;
    private string _primaryUnitPickerSearchText = string.Empty;
    private string _secondaryUnitPickerSearchText = string.Empty;
    private GestionaleLookupOption? _selectedPrimaryUnitOption;
    private GestionaleLookupOption? _selectedSecondaryUnitOption;
    private string _quantitaMinimaVenditaLegacyText = "1";
    private string _quantitaMultiplaVenditaLegacyText = "1";
    private string _ultimoAcquistoLabel = "Nessun ultimo acquisto trovato.";
    private decimal _ultimoCostoAcquisto;
    private bool _acquistoAConfezione;
    private bool _venditaAPezzoSingolo;
    private string _pezziPerConfezioneText = string.Empty;
    private string _multiploOrdineText = string.Empty;
    private string _lottoMinimoText = string.Empty;
    private string _giorniCoperturaText = string.Empty;
    private string _prezzoConfezioneText = string.Empty;
    private string _prezzoSingoloText = string.Empty;
    private string _prezzoVenditaLocaleText = string.Empty;
    private string _quantitaPromoLocaleText = string.Empty;
    private string _prezzoPromoLocaleText = string.Empty;
    private string _note = string.Empty;
    private string _localSettingsInfo = "Nessun parametro locale salvato.";
    private string _legacyStatusInfo = "Legacy non ancora salvato in questa sessione.";
    private string _localStatusLabel = "Locale non ancora salvato in questa sessione.";
    private LegacyOfferRowViewModel? _selectedLegacyOfferRow;
    private string _defaultLegacyOfferExpiryText = string.Empty;

    public MagazzinoArticleViewModel(
        IGestionaleArticleReadService articleReadService,
        IGestionaleArticleWriteService articleWriteService,
        IGestionaleDocumentReadService documentReadService,
        IReorderArticleSettingsRepository settingsRepository,
        IPosProcessLogService logService)
    {
        _articleReadService = articleReadService;
        _articleWriteService = articleWriteService;
        _documentReadService = documentReadService;
        _settingsRepository = settingsRepository;
        _logService = logService;

        SearchRows = [];
        LegacyOfferRows = [];
        UnitOptions = [];
        SecondaryUnitOptions = [];
        FilteredPrimaryUnitOptions = [];
        FilteredSecondaryUnitOptions = [];
        SearchCommand = new RelayCommand(async () => await SearchAsync(), () => !IsLoading && !string.IsNullOrWhiteSpace(SearchText));
        RefreshCurrentArticleCommand = new RelayCommand(async () => await ReloadCurrentArticleAsync(), () => !IsLoading && SelectedSearchResult is not null);
        SaveLegacyCommand = new RelayCommand(async () => await SaveLegacyAsync(), () => !IsLoading && ArticoloOid > 0);
        SaveLocalSettingsCommand = new RelayCommand(async () => await SaveLocalSettingsAsync(), () => !IsLoading && ArticoloOid > 0);
        ApplyLocalSettingsToChildrenCommand = new RelayCommand(async () => await ApplyLocalSettingsToChildrenAsync(), () => !IsLoading && CanApplyLocalSettingsToChildren);
        CopyLocalSettingsCommand = new RelayCommand(CopyLocalSettings, () => ArticoloOid > 0);
        PasteLocalSettingsCommand = new RelayCommand(PasteLocalSettings, () => ArticoloOid > 0);
        AddLegacyOfferRowCommand = new RelayCommand(AddLegacyOfferRow, () => !IsLoading && SelectedSearchResult is not null);
        RemoveLegacyOfferRowCommand = new RelayCommand(RemoveSelectedLegacyOfferRow, () => !IsLoading && SelectedLegacyOfferRow is not null && SelectedLegacyOfferRow.CanEditTierValues);
        PropagateLegacyOfferRowCommand = new RelayCommand(async () => await PropagateSelectedLegacyOfferRowAsync(), () => !IsLoading && CanPropagateLegacyOfferRow);
    }

    public string Titolo => "Articolo magazzino";

    public ObservableCollection<MagazzinoArticleSearchRowViewModel> SearchRows { get; }

    public ObservableCollection<LegacyOfferRowViewModel> LegacyOfferRows { get; }

    public ObservableCollection<GestionaleLookupOption> UnitOptions { get; }

    public ObservableCollection<GestionaleLookupOption> SecondaryUnitOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredPrimaryUnitOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredSecondaryUnitOptions { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand RefreshCurrentArticleCommand { get; }

    public RelayCommand SaveLegacyCommand { get; }

    public RelayCommand SaveLocalSettingsCommand { get; }

    public RelayCommand ApplyLocalSettingsToChildrenCommand { get; }

    public RelayCommand CopyLocalSettingsCommand { get; }

    public RelayCommand PasteLocalSettingsCommand { get; }

    public RelayCommand AddLegacyOfferRowCommand { get; }

    public RelayCommand RemoveLegacyOfferRowCommand { get; }

    public RelayCommand PropagateLegacyOfferRowCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
                ScheduleAutoSearch();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
                RefreshCurrentArticleCommand.RaiseCanExecuteChanged();
                SaveLegacyCommand.RaiseCanExecuteChanged();
                SaveLocalSettingsCommand.RaiseCanExecuteChanged();
                ApplyLocalSettingsToChildrenCommand.RaiseCanExecuteChanged();
                CopyLocalSettingsCommand.RaiseCanExecuteChanged();
                PasteLocalSettingsCommand.RaiseCanExecuteChanged();
                AddLegacyOfferRowCommand.RaiseCanExecuteChanged();
                RemoveLegacyOfferRowCommand.RaiseCanExecuteChanged();
                PropagateLegacyOfferRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public MagazzinoArticleSearchRowViewModel? SelectedSearchRow
    {
        get => _selectedSearchRow;
        set
        {
            if (value is not null && !value.IsSelectable)
            {
                return;
            }

            if (SetProperty(ref _selectedSearchRow, value))
            {
                SelectedSearchResult = value?.Article;
            }
        }
    }

    public GestionaleArticleSearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (SetProperty(ref _selectedSearchResult, value))
            {
                RefreshCurrentArticleCommand.RaiseCanExecuteChanged();
                SaveLocalSettingsCommand.RaiseCanExecuteChanged();
                ApplyLocalSettingsToChildrenCommand.RaiseCanExecuteChanged();
                CopyLocalSettingsCommand.RaiseCanExecuteChanged();
                PasteLocalSettingsCommand.RaiseCanExecuteChanged();
                AddLegacyOfferRowCommand.RaiseCanExecuteChanged();
                RemoveLegacyOfferRowCommand.RaiseCanExecuteChanged();
                PropagateLegacyOfferRowCommand.RaiseCanExecuteChanged();
                NotifyPropertyChanged(nameof(LocalSettingsScopeInfo));
                NotifyPropertyChanged(nameof(CanApplyLocalSettingsToChildren));
                NotifyPropertyChanged(nameof(CurrentVariantLegacyLabel));
                NotifyPropertyChanged(nameof(LegacyOfferSectionInfo));
                if (value is not null)
                {
                    _ = LoadSelectedArticleAsync(value);
                }
                else
                {
                    ResetArticleState();
                }
            }
        }
    }

    public bool HasSelectedArticle => ArticoloOid > 0;

    public int ArticoloOid
    {
        get => _articoloOid;
        private set
        {
            if (SetProperty(ref _articoloOid, value))
            {
                NotifyPropertyChanged(nameof(HasSelectedArticle));
                SaveLegacyCommand.RaiseCanExecuteChanged();
                SaveLocalSettingsCommand.RaiseCanExecuteChanged();
                ApplyLocalSettingsToChildrenCommand.RaiseCanExecuteChanged();
                CopyLocalSettingsCommand.RaiseCanExecuteChanged();
                PasteLocalSettingsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CodiceArticolo
    {
        get => _codiceArticolo;
        private set => SetProperty(ref _codiceArticolo, value);
    }

    public string DescrizioneArticolo
    {
        get => _descrizioneArticolo;
        private set => SetProperty(ref _descrizioneArticolo, value);
    }

    public string GiacenzaLabel
    {
        get => _giacenzaLabel;
        private set => SetProperty(ref _giacenzaLabel, value);
    }

    public string ListinoLegacyLabel
    {
        get => _listinoLegacyLabel;
        private set => SetProperty(ref _listinoLegacyLabel, value);
    }

    public string PrezzoVenditaLabel
    {
        get => _prezzoVenditaLabel;
        private set => SetProperty(ref _prezzoVenditaLabel, value);
    }

    public string UnitaPrincipaleLabel
    {
        get => _unitaPrincipaleLabel;
        private set => SetProperty(ref _unitaPrincipaleLabel, value);
    }

    public string UnitaSecondariaLabel
    {
        get => _unitaSecondariaLabel;
        private set => SetProperty(ref _unitaSecondariaLabel, value);
    }

    public string RapportoUnitaLabel
    {
        get => _rapportoUnitaLabel;
        private set => SetProperty(ref _rapportoUnitaLabel, value);
    }

    public string QuantitaMinimaVenditaLabel
    {
        get => _quantitaMinimaVenditaLabel;
        private set => SetProperty(ref _quantitaMinimaVenditaLabel, value);
    }

    public string QuantitaMultiplaVenditaLabel
    {
        get => _quantitaMultiplaVenditaLabel;
        private set => SetProperty(ref _quantitaMultiplaVenditaLabel, value);
    }

    public string UltimoAcquistoLabel
    {
        get => _ultimoAcquistoLabel;
        private set => SetProperty(ref _ultimoAcquistoLabel, value);
    }

    public LegacyOfferRowViewModel? SelectedLegacyOfferRow
    {
        get => _selectedLegacyOfferRow;
        set
        {
            if (SetProperty(ref _selectedLegacyOfferRow, value))
            {
                RemoveLegacyOfferRowCommand.RaiseCanExecuteChanged();
                PropagateLegacyOfferRowCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DescrizioneLegacyText
    {
        get => _descrizioneLegacyText;
        set => SetProperty(ref _descrizioneLegacyText, value);
    }

    public string PrezzoVenditaLegacyText
    {
        get => _prezzoVenditaLegacyText;
        set => SetProperty(ref _prezzoVenditaLegacyText, value);
    }

    public string UnitaPrincipaleLegacyText
    {
        get => _unitaPrincipaleLegacyText;
        set
        {
            if (SetProperty(ref _unitaPrincipaleLegacyText, value))
            {
                SyncSelectedPrimaryUnitOption();
            }
        }
    }

    public string UnitaSecondariaLegacyText
    {
        get => _unitaSecondariaLegacyText;
        set
        {
            if (SetProperty(ref _unitaSecondariaLegacyText, value))
            {
                SyncSelectedSecondaryUnitOption();
            }
        }
    }

    public string RapportoUnitaLegacyText
    {
        get => _rapportoUnitaLegacyText;
        set => SetProperty(ref _rapportoUnitaLegacyText, value);
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

    public GestionaleLookupOption? SelectedPrimaryUnitOption
    {
        get => _selectedPrimaryUnitOption;
        set
        {
            if (!SetProperty(ref _selectedPrimaryUnitOption, value))
            {
                return;
            }

            if (value is not null)
            {
                UnitaPrincipaleLegacyText = value.Label;
                IsPrimaryUnitPickerOpen = false;
                if (!string.IsNullOrWhiteSpace(_primaryUnitPickerSearchText))
                {
                    _primaryUnitPickerSearchText = string.Empty;
                    NotifyPropertyChanged(nameof(PrimaryUnitPickerSearchText));
                    RefreshFilteredPrimaryUnitOptions();
                }
            }
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

            if (value is not null)
            {
                UnitaSecondariaLegacyText = value.Oid == 0 ? string.Empty : value.Label;
                IsSecondaryUnitPickerOpen = false;
                if (!string.IsNullOrWhiteSpace(_secondaryUnitPickerSearchText))
                {
                    _secondaryUnitPickerSearchText = string.Empty;
                    NotifyPropertyChanged(nameof(SecondaryUnitPickerSearchText));
                    RefreshFilteredSecondaryUnitOptions();
                }
            }
        }
    }

    public string QuantitaMinimaVenditaLegacyText
    {
        get => _quantitaMinimaVenditaLegacyText;
        set => SetProperty(ref _quantitaMinimaVenditaLegacyText, value);
    }

    public string QuantitaMultiplaVenditaLegacyText
    {
        get => _quantitaMultiplaVenditaLegacyText;
        set => SetProperty(ref _quantitaMultiplaVenditaLegacyText, value);
    }

    public bool AcquistoAConfezione
    {
        get => _acquistoAConfezione;
        set => SetProperty(ref _acquistoAConfezione, value);
    }

    public bool VenditaAPezzoSingolo
    {
        get => _venditaAPezzoSingolo;
        set => SetProperty(ref _venditaAPezzoSingolo, value);
    }

    public string PezziPerConfezioneText
    {
        get => _pezziPerConfezioneText;
        set => SetProperty(ref _pezziPerConfezioneText, value);
    }

    public string MultiploOrdineText
    {
        get => _multiploOrdineText;
        set => SetProperty(ref _multiploOrdineText, value);
    }

    public string LottoMinimoText
    {
        get => _lottoMinimoText;
        set => SetProperty(ref _lottoMinimoText, value);
    }

    public string GiorniCoperturaText
    {
        get => _giorniCoperturaText;
        set => SetProperty(ref _giorniCoperturaText, value);
    }

    public string PrezzoConfezioneText
    {
        get => _prezzoConfezioneText;
        set => SetProperty(ref _prezzoConfezioneText, value);
    }

    public string PrezzoSingoloText
    {
        get => _prezzoSingoloText;
        set => SetProperty(ref _prezzoSingoloText, value);
    }

    public string PrezzoVenditaLocaleText
    {
        get => _prezzoVenditaLocaleText;
        set => SetProperty(ref _prezzoVenditaLocaleText, value);
    }

    public string QuantitaPromoLocaleText
    {
        get => _quantitaPromoLocaleText;
        set => SetProperty(ref _quantitaPromoLocaleText, value);
    }

    public string PrezzoPromoLocaleText
    {
        get => _prezzoPromoLocaleText;
        set => SetProperty(ref _prezzoPromoLocaleText, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string LocalSettingsInfo
    {
        get => _localSettingsInfo;
        private set => SetProperty(ref _localSettingsInfo, value);
    }

    public string LegacyStatusInfo
    {
        get => _legacyStatusInfo;
        private set => SetProperty(ref _legacyStatusInfo, value);
    }

    public string LocalStatusLabel
    {
        get => _localStatusLabel;
        private set => SetProperty(ref _localStatusLabel, value);
    }

    public string LocalSettingsScopeInfo =>
        SelectedSearchResult is null
            ? string.Empty
            : IsParentSelection(SelectedSearchResult)
                ? "Salva sul padre per il default delle varianti senza override."
                : "Override variante: se non salvi qui resta l'eredita` del padre.";

    public bool CanApplyLocalSettingsToChildren =>
        SelectedSearchResult is not null &&
        IsParentSelection(SelectedSearchResult) &&
        SearchRows.Any(row => row.IsChild && row.Article is not null && row.FamilyOid == SelectedSearchRow?.FamilyOid);

    public bool HasLegacyOfferRows => LegacyOfferRows.Count > 0;

    public string LegacyOfferSectionInfo =>
        SelectedSearchResult is null
            ? "Seleziona un articolo o una variante per leggere i listini legacy reali."
            : HasLegacyOfferRows
                ? $"{LegacyOfferRows.Count} righe | {ListinoLegacyLabel} | {CurrentVariantLegacyLabel}"
                : $"Nessuna riga | {ListinoLegacyLabel} | {CurrentVariantLegacyLabel}";

    public string CurrentVariantLegacyLabel =>
        SelectedSearchResult is null
            ? "variante"
            : string.IsNullOrWhiteSpace(SelectedSearchResult.VarianteLabel)
                ? "articolo base"
                : SelectedSearchResult.VarianteLabel;

    public bool CanPropagateLegacyOfferRow =>
        SelectedSearchResult is not null &&
        SelectedLegacyOfferRow is not null &&
        SelectedLegacyOfferRow.CanEditTierValues &&
        SearchRows.Any(row =>
            row.IsChild &&
            row.Article is not null &&
            row.FamilyOid == SelectedSearchRow?.FamilyOid &&
            !ReferenceEquals(row.Article, SelectedSearchResult));

    private async Task SearchAsync()
    {
        var input = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            SearchRows.Clear();
            SelectedSearchRow = null;
            StatusMessage = "Inserisci codice o descrizione: la ricerca parte da sola.";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Ricerca articoli legacy in corso...";

            var results = await _articleReadService.SearchArticlesAsync(input, 40);
            SearchRows.Clear();
            foreach (var item in BuildSearchRows(results))
            {
                SearchRows.Add(item);
            }

            var bestMatch = ResolveBestAutoSelection(input, results);
            if (bestMatch is not null)
            {
                SelectedSearchRow = SearchRows.FirstOrDefault(item => ReferenceEquals(item.Article, bestMatch));
            }
            else
            {
                SelectedSearchRow = null;
            }

            StatusMessage = SearchRows.Count == 0
                ? "Nessun articolo trovato con i criteri inseriti."
                : bestMatch is not null
                    ? $"Scheda {bestMatch.CodiceArticolo} caricata."
                    : $"{SearchRows.Count} righe disponibili.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore ricerca articoli: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), "Errore durante la ricerca articoli legacy.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ReloadCurrentArticleAsync()
    {
        if (SelectedSearchResult is null)
        {
            return;
        }

        await LoadSelectedArticleAsync(SelectedSearchResult);
    }

    private async Task LoadSelectedArticleAsync(GestionaleArticleSearchResult article)
    {
        try
        {
            IsLoading = true;

            if (!_unitLookupsLoaded)
            {
                await EnsureUnitOptionsAsync();
            }

            var pricing = await _articleReadService.GetArticlePricingDetailAsync(article);
            var legacyListinoRows = await _articleReadService.GetArticleLegacyListinoRowsAsync(article);
            var latestPurchase = await _documentReadService.GetLatestArticlePurchaseAsync(article.Oid);
            var localSettings = await _settingsRepository.GetByArticleAsync(
                article.Oid,
                article.VarianteDettaglioOid1,
                article.VarianteDettaglioOid2,
                article.BarcodeAlternativo);

            ArticoloOid = article.Oid;
            CodiceArticolo = article.CodiceArticolo;
            DescrizioneArticolo = article.DisplayLabel;
            GiacenzaLabel = article.Giacenza.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
            ListinoLegacyLabel = string.IsNullOrWhiteSpace(pricing?.ListinoNome) ? "-" : pricing.ListinoNome;
            PrezzoVenditaLabel = ResolveDisplayedSalePrice(article, pricing).ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));

            UnitaPrincipaleLabel = pricing?.UnitaMisuraPrincipale ?? "PZ";
            UnitaSecondariaLabel = pricing?.HasSecondaryUnit == true ? pricing.UnitaMisuraSecondaria ?? "-" : "-";
            RapportoUnitaLabel = pricing?.HasSecondaryUnit == true
                ? pricing.MoltiplicatoreUnitaSecondaria.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"))
                : "-";
            QuantitaMinimaVenditaLabel = pricing?.QuantitaMinimaVendita.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) ?? "1";
            QuantitaMultiplaVenditaLabel = pricing?.QuantitaMultiplaVendita.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) ?? "1";

            UltimoAcquistoLabel = latestPurchase is null
                ? "Nessun ultimo acquisto trovato."
                : $"{latestPurchase.FornitoreNominativo} - {latestPurchase.DataUltimoAcquisto:dd/MM/yyyy} - {latestPurchase.PrezzoUnitario.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"))}";
            _ultimoCostoAcquisto = latestPurchase?.PrezzoUnitario ?? 0m;

            DescrizioneLegacyText = article.Descrizione;
            PrezzoVenditaLegacyText = PrezzoVenditaLabel;
            UnitaPrincipaleLegacyText = UnitaPrincipaleLabel;
            UnitaSecondariaLegacyText = UnitaSecondariaLabel == "-" ? string.Empty : UnitaSecondariaLabel;
            RapportoUnitaLegacyText = RapportoUnitaLabel == "-" ? string.Empty : RapportoUnitaLabel;
            SyncSelectedPrimaryUnitOption();
            SyncSelectedSecondaryUnitOption();
            QuantitaMinimaVenditaLegacyText = QuantitaMinimaVenditaLabel;
            QuantitaMultiplaVenditaLegacyText = QuantitaMultiplaVenditaLabel;
            LoadLocalFields(localSettings, pricing);
            PopulateLegacyOfferRows(article, legacyListinoRows, latestPurchase);

            StatusMessage = $"Scheda articolo {article.CodiceArticolo} caricata. Legacy e parametri locali sono allineati sullo stesso articolo.";
            LegacyStatusInfo = $"Legacy riletto da db_diltech alle {DateTime.Now:HH:mm}.";
            LocalStatusLabel = localSettings is null
                ? "Nessun salvataggio locale presente."
                : $"Locale pronto. Ultimo salvataggio {localSettings.UpdatedAt.LocalDateTime:dd/MM HH:mm}.";
            NotifyPropertyChanged(nameof(CurrentVariantLegacyLabel));
            NotifyPropertyChanged(nameof(LegacyOfferSectionInfo));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore apertura scheda articolo: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), $"Errore durante il caricamento dell'articolo {article.CodiceArticolo}.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EnsureUnitOptionsAsync(CancellationToken cancellationToken = default)
    {
        var unitOptions = await _articleReadService.GetUnitOptionsAsync(cancellationToken);

        ReplaceCollection(UnitOptions, unitOptions);

        var secondaryUnitOptions = new List<GestionaleLookupOption>
        {
            new() { Oid = 0, Label = "-" }
        };
        secondaryUnitOptions.AddRange(unitOptions);

        ReplaceCollection(SecondaryUnitOptions, secondaryUnitOptions);
        RefreshFilteredPrimaryUnitOptions();
        RefreshFilteredSecondaryUnitOptions();
        _unitLookupsLoaded = true;
    }

    private void LoadLocalFields(ReorderArticleSettings? localSettings, GestionaleArticlePricingDetail? pricing)
    {
        var prezzoVenditaRiferimento = ResolveDisplayedSalePrice(SelectedSearchResult!, pricing);
        var promoTier = pricing?.FascePrezzoQuantita
            .Where(item => item.QuantitaMinima > 1 && item.PrezzoUnitario > 0)
            .OrderBy(item => item.QuantitaMinima)
            .FirstOrDefault();
        var pezziPerConfezioneDefault = localSettings?.PezziPerConfezione ?? (pricing?.HasSecondaryUnit == true ? pricing.MoltiplicatoreUnitaSecondaria : null);
        var prezzoSingoloDefault = prezzoVenditaRiferimento > 0 ? prezzoVenditaRiferimento : (decimal?)null;
        var prezzoConfezioneDefault = ResolvePackagePrice(pezziPerConfezioneDefault, prezzoSingoloDefault);

        AcquistoAConfezione = localSettings?.AcquistoAConfezione ?? pricing?.HasSecondaryUnit == true;
        VenditaAPezzoSingolo = localSettings?.VenditaAPezzoSingolo ?? pricing?.HasSecondaryUnit == true;
        PezziPerConfezioneText = FormatDecimal(pezziPerConfezioneDefault);
        MultiploOrdineText = FormatDecimal(localSettings?.MultiploOrdine);
        LottoMinimoText = FormatDecimal(localSettings?.LottoMinimoOrdine);
        GiorniCoperturaText = localSettings?.GiorniCopertura?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        PrezzoConfezioneText = FormatPrice(localSettings?.PrezzoConfezione ?? prezzoConfezioneDefault);
        PrezzoSingoloText = FormatPrice(localSettings?.PrezzoSingolo ?? prezzoSingoloDefault);
        PrezzoVenditaLocaleText = FormatPrice(localSettings?.PrezzoVenditaRiferimento ?? prezzoVenditaRiferimento);
        QuantitaPromoLocaleText = FormatDecimal(localSettings?.QuantitaPromo ?? promoTier?.QuantitaMinima);
        PrezzoPromoLocaleText = FormatPrice(localSettings?.PrezzoPromo ?? promoTier?.PrezzoUnitario);
        Note = localSettings?.Note ?? string.Empty;
        LocalSettingsInfo = localSettings is null
            ? IsParentSelection(SelectedSearchResult)
                ? "Nessun parametro locale padre salvato: il primo salvataggio qui diventera` il default di tutte le varianti senza override."
                : "Nessun parametro locale variante salvato: se non salvi qui, questa variante usa il default del padre."
            : localSettings.InheritedFromParent
                ? $"Parametri locali ereditati dall'articolo padre, aggiornati il {localSettings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}. Salva qui solo se vuoi un override per questa variante."
                : IsParentSelection(SelectedSearchResult)
                ? $"Parametri locali padre aggiornati il {localSettings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}. Restano il default di tutte le varianti senza override."
                : $"Override locale variante aggiornato il {localSettings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}.";
        LocalStatusLabel = localSettings is null
            ? "Nessun salvataggio locale presente."
            : $"Locale pronto. Ultimo salvataggio {localSettings.UpdatedAt.LocalDateTime:dd/MM HH:mm}.";
    }

    private void CopyLocalSettings()
    {
        if (ArticoloOid <= 0 || SelectedSearchResult is null)
        {
            return;
        }

        if (!TryParseOptionalDecimal(PezziPerConfezioneText, out var pezziPerConfezione)
            || !TryParseOptionalDecimal(MultiploOrdineText, out var multiploOrdine)
            || !TryParseOptionalDecimal(LottoMinimoText, out var lottoMinimo)
            || !TryParseOptionalDecimal(PrezzoConfezioneText, out var prezzoConfezione)
            || !TryParseOptionalDecimal(PrezzoSingoloText, out var prezzoSingolo)
            || !TryParseOptionalDecimal(PrezzoVenditaLocaleText, out var prezzoVenditaRiferimento)
            || !TryParseOptionalDecimal(QuantitaPromoLocaleText, out var quantitaPromo)
            || !TryParseOptionalDecimal(PrezzoPromoLocaleText, out var prezzoPromo)
            || !TryParseOptionalInt(GiorniCoperturaText, out var giorniCopertura))
        {
            StatusMessage = "Parametri locali non copiati: controlla prima i valori numerici.";
            return;
        }

        var payload = new ReorderArticleSettings
        {
            SettingsKey = ReorderArticleSettings.BuildSettingsKey(
                ArticoloOid,
                SelectedSearchResult.VarianteDettaglioOid1,
                SelectedSearchResult.VarianteDettaglioOid2,
                SelectedSearchResult.BarcodeAlternativo),
            ArticoloOid = ArticoloOid,
            CodiceArticolo = SelectedSearchResult.CodiceArticolo,
            DescrizioneArticolo = SelectedSearchResult.Descrizione,
            BarcodeAlternativo = SelectedSearchResult.BarcodeAlternativo,
            VarianteDettaglioOid1 = SelectedSearchResult.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = SelectedSearchResult.VarianteDettaglioOid2,
            VarianteLabel = SelectedSearchResult.VarianteLabel,
            AcquistoAConfezione = AcquistoAConfezione,
            VenditaAPezzoSingolo = VenditaAPezzoSingolo,
            PezziPerConfezione = pezziPerConfezione,
            MultiploOrdine = multiploOrdine,
            LottoMinimoOrdine = lottoMinimo,
            GiorniCopertura = giorniCopertura,
            PrezzoConfezione = prezzoConfezione,
            PrezzoSingolo = prezzoSingolo,
            PrezzoVenditaRiferimento = prezzoVenditaRiferimento,
            QuantitaPromo = quantitaPromo,
            PrezzoPromo = prezzoPromo,
            Note = Note?.Trim() ?? string.Empty
        };

        Clipboard.SetText(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        StatusMessage = $"Parametri locali copiati negli appunti per {SelectedSearchResult.CodiceArticolo}.";
    }

    private void PasteLocalSettings()
    {
        if (!Clipboard.ContainsText())
        {
            StatusMessage = "Appunti vuoti: nessun parametro locale da incollare.";
            return;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ReorderArticleSettings>(Clipboard.GetText());
            if (payload is null)
            {
                StatusMessage = "Contenuto appunti non valido per i parametri locali.";
                return;
            }

            AcquistoAConfezione = payload.AcquistoAConfezione;
            VenditaAPezzoSingolo = payload.VenditaAPezzoSingolo;
            PezziPerConfezioneText = FormatDecimal(payload.PezziPerConfezione);
            MultiploOrdineText = FormatDecimal(payload.MultiploOrdine);
            LottoMinimoText = FormatDecimal(payload.LottoMinimoOrdine);
            GiorniCoperturaText = payload.GiorniCopertura?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            PrezzoConfezioneText = FormatPrice(payload.PrezzoConfezione);
            PrezzoSingoloText = FormatPrice(payload.PrezzoSingolo);
            PrezzoVenditaLocaleText = FormatPrice(payload.PrezzoVenditaRiferimento);
            QuantitaPromoLocaleText = FormatDecimal(payload.QuantitaPromo);
            PrezzoPromoLocaleText = FormatPrice(payload.PrezzoPromo);
            Note = payload.Note ?? string.Empty;
            StatusMessage = "Parametri locali incollati dagli appunti. Controlla e salva.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile incollare i parametri locali: {ex.Message}";
        }
    }

    private async Task SaveLocalSettingsAsync()
    {
        if (!TryBuildCurrentLocalSettings(out var settings))
        {
            return;
        }

        var selectedArticle = SelectedSearchResult;
        if (selectedArticle is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _settingsRepository.SaveAsync(settings);
            if (IsParentSelection(selectedArticle))
            {
                LocalSettingsInfo = $"Parametri locali padre aggiornati il {settings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}. Restano il default di tutte le varianti senza override.";
                LocalStatusLabel = $"Locale salvato alle {DateTime.Now:HH:mm}.";
                StatusMessage = $"Parametri locali padre salvati per {selectedArticle.CodiceArticolo}: le varianti senza override, anche future, useranno questi valori.";
            }
            else
            {
                LocalSettingsInfo = $"Override locale variante aggiornato il {settings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}.";
                LocalStatusLabel = $"Locale salvato alle {DateTime.Now:HH:mm}.";
                StatusMessage = $"Override locale salvato solo per la variante {selectedArticle.CodiceArticolo}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore salvataggio parametri locali: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), $"Errore durante il salvataggio parametri locali dell'articolo {selectedArticle.CodiceArticolo}.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyLocalSettingsToChildrenAsync()
    {
        if (!TryBuildCurrentLocalSettings(out var parentSettings) || SelectedSearchResult is null || SelectedSearchRow is null)
        {
            return;
        }

        if (!IsParentSelection(SelectedSearchResult))
        {
            StatusMessage = "Per propagare i parametri locali devi selezionare il padre della famiglia articolo.";
            return;
        }

        var children = SearchRows
            .Where(row => row.IsChild && row.Article is not null && row.FamilyOid == SelectedSearchRow.FamilyOid)
            .Select(row => row.Article!)
            .GroupBy(article => ReorderArticleSettings.BuildSettingsKey(article.Oid, article.VarianteDettaglioOid1, article.VarianteDettaglioOid2, article.BarcodeAlternativo))
            .Select(group => group.First())
            .ToList();

        if (children.Count == 0)
        {
            StatusMessage = "Nessuna variante figlia disponibile a cui applicare i parametri locali.";
            return;
        }

        try
        {
            IsLoading = true;
            await _settingsRepository.SaveAsync(parentSettings);

            foreach (var child in children)
            {
                await _settingsRepository.SaveAsync(CloneLocalSettingsForArticle(parentSettings, child));
            }

            LocalSettingsInfo = $"Parametri locali padre aggiornati il {parentSettings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}. Propagati anche a {children.Count} varianti correnti; le future continuano a ereditare dal padre.";
            LocalStatusLabel = $"Locale salvato alle {DateTime.Now:HH:mm}.";
            StatusMessage = $"Parametri locali salvati sul padre e propagati automaticamente a {children.Count} varianti.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore propagazione parametri locali ai figli: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), $"Errore durante la propagazione parametri locali dal padre {SelectedSearchResult.CodiceArticolo}.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveLegacyAsync()
    {
        if (ArticoloOid <= 0 || SelectedSearchResult is null)
        {
            return;
        }

        if (!TryParseRequiredDecimal(PrezzoVenditaLegacyText, out var prezzoVendita)
            || !TryParseRequiredDecimal(QuantitaMinimaVenditaLegacyText, out var quantitaMinimaVendita)
            || !TryParseRequiredDecimal(QuantitaMultiplaVenditaLegacyText, out var quantitaMultiplaVendita)
            || !TryParseOptionalDecimal(RapportoUnitaLegacyText, out var rapportoUnita))
        {
            StatusMessage = "I dati legacy non sono validi. Controlla prezzo, rapporto e quantita`.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DescrizioneLegacyText) || string.IsNullOrWhiteSpace(UnitaPrincipaleLegacyText))
        {
            StatusMessage = "Descrizione e U.M. principale legacy sono obbligatorie.";
            return;
        }

        if (LegacyOfferRows.Any(row => row.CanEditTierValues && !row.IsValid))
        {
            StatusMessage = "Le fasce legacy devono avere quantita` maggiore di 1 e prezzo valido.";
            return;
        }

        try
        {
            IsLoading = true;
            await _articleWriteService.SaveArticleAsync(new GestionaleArticleLegacyUpdate
            {
                ArticoloOid = ArticoloOid,
                VarianteDettaglioOid1 = SelectedSearchResult.VarianteDettaglioOid1,
                VarianteDettaglioOid2 = SelectedSearchResult.VarianteDettaglioOid2,
                DescrizioneArticolo = DescrizioneLegacyText.Trim(),
                UnitaMisuraPrincipale = UnitaPrincipaleLegacyText.Trim(),
                UnitaMisuraSecondaria = string.IsNullOrWhiteSpace(UnitaSecondariaLegacyText) ? null : UnitaSecondariaLegacyText.Trim(),
                MoltiplicatoreUnitaSecondaria = rapportoUnita,
                QuantitaMinimaVendita = quantitaMinimaVendita,
                QuantitaMultiplaVendita = quantitaMultiplaVendita,
                PrezzoVendita = prezzoVendita
            });

            await _articleWriteService.SaveQuantityPriceTiersAsync(new GestionaleArticleLegacyOffersUpdate
            {
                ArticoloOid = ArticoloOid,
                VarianteDettaglioOid1 = SelectedSearchResult.VarianteDettaglioOid1,
                VarianteDettaglioOid2 = SelectedSearchResult.VarianteDettaglioOid2,
                AliquotaIva = SelectedSearchResult.AliquotaIva,
                PriceTiers = LegacyOfferRows
                    .Where(row => row.CanEditTierValues)
                    .OrderBy(row => row.QuantitaMinima)
                    .Select(row => new GestionaleArticleLegacyPriceTierUpdate
                    {
                        QuantitaMinima = row.QuantitaMinima,
                        PrezzoNetto = row.PrezzoNetto,
                        PrezzoIvato = row.PrezzoIvato,
                        DataFine = row.DataFine
                    })
                    .ToList()
            });

            LegacyStatusInfo = $"Legacy salvato alle {DateTime.Now:HH:mm}.";
            StatusMessage = $"Dati legacy salvati per l'articolo {SelectedSearchResult.CodiceArticolo}.";
            await ReloadCurrentArticleAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore salvataggio dati legacy: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), $"Errore durante il salvataggio legacy dell'articolo {SelectedSearchResult.CodiceArticolo}.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetArticleState()
    {
        ArticoloOid = 0;
        CodiceArticolo = string.Empty;
        DescrizioneArticolo = string.Empty;
        GiacenzaLabel = "-";
        ListinoLegacyLabel = "-";
        PrezzoVenditaLabel = "-";
        UnitaPrincipaleLabel = "PZ";
        UnitaSecondariaLabel = "-";
        RapportoUnitaLabel = "-";
        QuantitaMinimaVenditaLabel = "1";
        QuantitaMultiplaVenditaLabel = "1";
        UltimoAcquistoLabel = "Nessun ultimo acquisto trovato.";
        DescrizioneLegacyText = string.Empty;
        PrezzoVenditaLegacyText = string.Empty;
        UnitaPrincipaleLegacyText = "PZ";
        UnitaSecondariaLegacyText = string.Empty;
        RapportoUnitaLegacyText = string.Empty;
        QuantitaMinimaVenditaLegacyText = "1";
        QuantitaMultiplaVenditaLegacyText = "1";
        _ultimoCostoAcquisto = 0m;
        _defaultLegacyOfferExpiryText = string.Empty;
        LegacyOfferRows.Clear();
        SelectedLegacyOfferRow = null;
        AcquistoAConfezione = false;
        VenditaAPezzoSingolo = false;
        PezziPerConfezioneText = string.Empty;
        MultiploOrdineText = string.Empty;
        LottoMinimoText = string.Empty;
        GiorniCoperturaText = string.Empty;
        PrezzoConfezioneText = string.Empty;
        PrezzoSingoloText = string.Empty;
        PrezzoVenditaLocaleText = string.Empty;
        QuantitaPromoLocaleText = string.Empty;
        PrezzoPromoLocaleText = string.Empty;
        Note = string.Empty;
        LocalSettingsInfo = "Nessun parametro locale salvato.";
        LegacyStatusInfo = "Legacy non ancora salvato in questa sessione.";
        LocalStatusLabel = "Locale non ancora salvato in questa sessione.";
        NotifyPropertyChanged(nameof(LocalSettingsScopeInfo));
        NotifyPropertyChanged(nameof(CurrentVariantLegacyLabel));
        NotifyPropertyChanged(nameof(LegacyOfferSectionInfo));
        NotifyPropertyChanged(nameof(HasLegacyOfferRows));
    }

    private void ScheduleAutoSearch()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();

        var trimmed = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            SearchRows.Clear();
            SelectedSearchRow = null;
            StatusMessage = "Inserisci codice o descrizione: la ricerca parte da sola.";
            return;
        }

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(220, cts.Token);
                if (!cts.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () => await SearchAsync());
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private static string FormatDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) : string.Empty;

    private static string FormatPrice(decimal? value) =>
        value.HasValue ? value.Value.ToString("0.00", CultureInfo.GetCultureInfo("it-IT")) : string.Empty;

    private static GestionaleArticleSearchResult? ResolveBestAutoSelection(string input, IReadOnlyList<GestionaleArticleSearchResult> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        var normalized = input.Trim();
        var exact = results.FirstOrDefault(item =>
            string.Equals(item.BarcodeAlternativo, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.CodiceArticolo, normalized, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        return results.Count == 1 ? results[0] : null;
    }

    private static IReadOnlyList<MagazzinoArticleSearchRowViewModel> BuildSearchRows(IReadOnlyList<GestionaleArticleSearchResult> results)
    {
        if (results.Count == 0)
        {
            return [];
        }

        var orderedFamilies = results
            .Select((item, index) => new { Item = item, Index = index })
            .GroupBy(entry => MagazzinoArticleSearchRowViewModel.ResolveFamilyOid(entry.Item))
            .OrderBy(group => group.Min(entry => entry.Index));

        var rows = new List<MagazzinoArticleSearchRowViewModel>();
        foreach (var family in orderedFamilies)
        {
            var familyItems = family
                .OrderBy(entry => entry.Index)
                .Select(entry => entry.Item)
                .ToList();

            var variantItems = familyItems
                .Where(IsRealVariantItem)
                .OrderBy(item => item.VarianteLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var articleItems = familyItems
                .Where(item => !IsGhostPseudoVariant(item))
                .ToList();

            if (variantItems.Count == 0)
            {
                rows.Add(MagazzinoArticleSearchRowViewModel.CreateArticle(articleItems.FirstOrDefault() ?? familyItems[0]));
                continue;
            }

            var rootArticle = articleItems.FirstOrDefault(item => !IsRealVariantItem(item)) ?? familyItems[0];
            rows.Add(MagazzinoArticleSearchRowViewModel.CreateSummary(
                rootArticle,
                family.Key,
                rootArticle.CodiceArticolo,
                rootArticle.Descrizione,
                variantItems.Sum(item => item.Giacenza),
                variantItems.Count));

            foreach (var variantItem in variantItems)
            {
                rows.Add(MagazzinoArticleSearchRowViewModel.CreateArticle(variantItem));
            }
        }

        return rows;
    }

    private static bool IsRealVariantItem(GestionaleArticleSearchResult item) =>
        item.ArticoloPadreOid.HasValue
        || item.VarianteDettaglioOid1.HasValue
        || item.VarianteDettaglioOid2.HasValue
        || !string.IsNullOrWhiteSpace(item.BarcodeAlternativo);

    private static bool IsGhostPseudoVariant(GestionaleArticleSearchResult item) =>
        !IsRealVariantItem(item) && !string.IsNullOrWhiteSpace(item.VarianteDescrizione);

    private static decimal ResolveDisplayedSalePrice(GestionaleArticleSearchResult article, GestionaleArticlePricingDetail? pricing)
    {
        var tierPrice = pricing?.FascePrezzoQuantita
            .OrderBy(item => item.QuantitaMinima <= 0 ? 1 : item.QuantitaMinima)
            .FirstOrDefault();

        if (tierPrice is not null && tierPrice.PrezzoUnitario > 0)
        {
            return tierPrice.PrezzoUnitario;
        }

        return article.PrezzoVendita;
    }

    private void PopulateLegacyOfferRows(
        GestionaleArticleSearchResult article,
        IReadOnlyList<GestionaleArticleLegacyListinoRow> legacyListinoRows,
        GestionaleArticlePurchaseQuickInfo? latestPurchase)
    {
        LegacyOfferRows.Clear();

        var hasVariantSelection = article.VarianteDettaglioOid1.HasValue || article.VarianteDettaglioOid2.HasValue;

        foreach (var row in legacyListinoRows)
        {
            var isGenericRow = !row.IsVariantSpecific;
            var matchesCurrentVariant = row.MatchesVariantScope(article.VarianteDettaglioOid1, article.VarianteDettaglioOid2);
            var canEditTierValues = row.QuantitaMinima > 1 && (isGenericRow || matchesCurrentVariant);

            LegacyOfferRows.Add(new LegacyOfferRowViewModel(row.ListinoNome, row.VarianteLabel)
            {
                TipoRigaLabel = BuildLegacyRowKindLabel(row.RowKind),
                ScopeLabel = ResolveLegacyRowScopeLabel(hasVariantSelection, isGenericRow, matchesCurrentVariant),
                UltimoCostoLegacy = row.UltimoCostoLegacy,
                IsBasePriceRow = row.IsBaseRow,
                CanEditTierValues = canEditTierValues,
                MatchesCurrentVariantScope = matchesCurrentVariant,
                VarianteDettaglioOid1 = row.VarianteDettaglioOid1,
                VarianteDettaglioOid2 = row.VarianteDettaglioOid2,
                QuantitaMinimaText = row.QuantitaMinima.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")),
                PrezzoNettoText = row.PrezzoNetto.ToString("0.0000", CultureInfo.GetCultureInfo("it-IT")),
                PrezzoIvatoText = row.PrezzoIvato.ToString("0.00", CultureInfo.GetCultureInfo("it-IT")),
                DataFineText = row.DataFine?.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("it-IT")) ?? string.Empty
            });
        }

        _defaultLegacyOfferExpiryText = legacyListinoRows
            .Select(item => item.DataFine)
            .Where(item => item.HasValue)
            .OrderBy(item => item)
            .Select(item => item!.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("it-IT")))
            .FirstOrDefault() ?? string.Empty;

        SelectedLegacyOfferRow = LegacyOfferRows.FirstOrDefault();
        NotifyPropertyChanged(nameof(HasLegacyOfferRows));
        NotifyPropertyChanged(nameof(LegacyOfferSectionInfo));
        AddLegacyOfferRowCommand.RaiseCanExecuteChanged();
        RemoveLegacyOfferRowCommand.RaiseCanExecuteChanged();
        PropagateLegacyOfferRowCommand.RaiseCanExecuteChanged();
    }

    private void AddLegacyOfferRow()
    {
        if (SelectedSearchResult is null)
        {
            return;
        }

        var suggestedQuantity = ResolveSuggestedLegacyOfferQuantity();
        var suggestedPrice = LegacyOfferRows.LastOrDefault()?.PrezzoIvato
            ?? ParseDecimalOrDefault(PrezzoVenditaLegacyText, SelectedSearchResult.PrezzoVendita);

        var row = new LegacyOfferRowViewModel(
            ListinoLegacyLabel == "-" ? "Legacy" : ListinoLegacyLabel,
            string.IsNullOrWhiteSpace(SelectedSearchResult.VarianteLabel) ? "Articolo base" : SelectedSearchResult.VarianteLabel)
        {
            TipoRigaLabel = ">> Q.tà",
            ScopeLabel = IsParentSelection(SelectedSearchResult) ? "Modifica padre" : "Nuovo override variante",
            UltimoCostoLegacy = LegacyOfferRows.FirstOrDefault(item => item.MatchesCurrentVariantScope)?.UltimoCostoLegacy ?? 0m,
            CanEditTierValues = true,
            QuantitaMinimaText = suggestedQuantity.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")),
            PrezzoNettoText = CalculateLegacyNetPriceText(suggestedPrice, SelectedSearchResult.AliquotaIva),
            PrezzoIvatoText = suggestedPrice > 0 ? suggestedPrice.ToString("0.00", CultureInfo.GetCultureInfo("it-IT")) : string.Empty,
            DataFineText = ResolveDefaultLegacyOfferExpiryText(),
            VarianteDettaglioOid1 = SelectedSearchResult.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = SelectedSearchResult.VarianteDettaglioOid2,
            MatchesCurrentVariantScope = true
        };

        LegacyOfferRows.Add(row);
        SelectedLegacyOfferRow = row;
        NotifyPropertyChanged(nameof(HasLegacyOfferRows));
        NotifyPropertyChanged(nameof(LegacyOfferSectionInfo));
        LegacyStatusInfo = "Legacy modificato localmente: salva per confermare sul DB.";
        StatusMessage = $"Nuova riga listino pronta per {CurrentVariantLegacyLabel}. Completa Q.tà e prezzo, poi salva.";
    }

    private void RemoveSelectedLegacyOfferRow()
    {
        if (SelectedLegacyOfferRow is null || !SelectedLegacyOfferRow.CanEditTierValues)
        {
            return;
        }

        var rowToRemove = SelectedLegacyOfferRow;
        var removedQuantity = rowToRemove.QuantitaMinimaText;
        LegacyOfferRows.Remove(rowToRemove);
        SelectedLegacyOfferRow = LegacyOfferRows.LastOrDefault();
        NotifyPropertyChanged(nameof(HasLegacyOfferRows));
        NotifyPropertyChanged(nameof(LegacyOfferSectionInfo));
        LegacyStatusInfo = "Legacy modificato localmente: salva per confermare sul DB.";
        StatusMessage = $"Riga listino {removedQuantity} rimossa dalla bozza corrente. Salva per confermare sul DB legacy.";
    }

    private async Task PropagateSelectedLegacyOfferRowAsync()
    {
        if (SelectedSearchResult is null || SelectedSearchRow is null || SelectedLegacyOfferRow is null)
        {
            return;
        }

        var sourceArticle = SelectedSearchResult;
        var selectedTier = SelectedLegacyOfferRow;
        if (!selectedTier.IsValid)
        {
            StatusMessage = "La fascia selezionata non e` valida: controlla Q.tà e prezzo prima di propagare.";
            return;
        }

        var targets = SearchRows
            .Where(row =>
                row.IsChild &&
                row.Article is not null &&
                row.FamilyOid == SelectedSearchRow.FamilyOid &&
                !ReferenceEquals(row.Article, sourceArticle))
            .Select(row => row.Article!)
            .GroupBy(article => $"{article.VarianteDettaglioOid1 ?? 0}-{article.VarianteDettaglioOid2 ?? 0}")
            .Select(group => group.First())
            .Select(article => new GestionaleArticleLegacyOfferPropagationTarget
            {
                VarianteDettaglioOid1 = article.VarianteDettaglioOid1,
                VarianteDettaglioOid2 = article.VarianteDettaglioOid2
            })
            .ToList();

        if (targets.Count == 0)
        {
            StatusMessage = "Nessuna variante sorella disponibile per la propagazione della fascia legacy.";
            return;
        }

        try
        {
            IsLoading = true;
            await _articleWriteService.PropagateQuantityPriceTierAsync(new GestionaleArticleLegacyOfferPropagationRequest
            {
                ArticoloOid = sourceArticle.Oid,
                AliquotaIva = sourceArticle.AliquotaIva,
                PriceTier = new GestionaleArticleLegacyPriceTierUpdate
                {
                    QuantitaMinima = selectedTier.QuantitaMinima,
                    PrezzoNetto = selectedTier.PrezzoNetto,
                    PrezzoIvato = selectedTier.PrezzoIvato,
                    DataFine = selectedTier.DataFine
                },
                Targets = targets
            });

            StatusMessage = $"Fascia {selectedTier.QuantitaMinima:0.##} propagata a {targets.Count} varianti legacy collegate.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore propagazione fascia legacy: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), "Errore durante la propagazione fascia legacy alle varianti.", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private decimal ResolveSuggestedLegacyOfferQuantity()
    {
        if (TryParseOptionalDecimal(PezziPerConfezioneText, out var pezziPerConfezione) && pezziPerConfezione.HasValue && pezziPerConfezione.Value > 1)
        {
            return pezziPerConfezione.Value;
        }

        var maxExisting = LegacyOfferRows
            .Select(row => row.QuantitaMinima)
            .DefaultIfEmpty(1m)
            .Max();

        return maxExisting > 1 ? maxExisting + 1 : 5m;
    }

    private string ResolveDefaultLegacyOfferExpiryText()
    {
        var existing = LegacyOfferRows
            .Select(row => row.DataFineText)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        return _defaultLegacyOfferExpiryText;
    }

    private static string BuildLegacyRowKindLabel(GestionaleArticleLegacyListinoRowKind rowKind) =>
        rowKind switch
        {
            GestionaleArticleLegacyListinoRowKind.Base => "Listino base",
            GestionaleArticleLegacyListinoRowKind.Quantity => ">> Q.tà",
            GestionaleArticleLegacyListinoRowKind.Variant => ">> Variante",
            GestionaleArticleLegacyListinoRowKind.VariantQuantity => ">> Variante + q.tà",
            _ => "Legacy"
        };

    private static string ResolveLegacyRowScopeLabel(bool hasVariantSelection, bool isGenericRow, bool matchesCurrentVariant)
    {
        if (!hasVariantSelection)
        {
            return isGenericRow ? "Modifica padre" : "Seleziona la variante per modificare l'override";
        }

        if (matchesCurrentVariant)
        {
            return "Override variante corrente";
        }

        return isGenericRow ? "Ereditato dal padre" : "Riga variante non corrente";
    }

    private static decimal ParseDecimalOrDefault(string? value, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string CalculateLegacyNetPriceText(decimal prezzoIvato, decimal aliquotaIva)
    {
        if (prezzoIvato <= 0)
        {
            return string.Empty;
        }

        if (aliquotaIva <= 0)
        {
            return prezzoIvato.ToString("0.0000", CultureInfo.GetCultureInfo("it-IT"));
        }

        var divisore = 1m + (aliquotaIva / 100m);
        var prezzoNetto = divisore <= 0
            ? prezzoIvato
            : decimal.Round(prezzoIvato / divisore, 4, MidpointRounding.AwayFromZero);
        return prezzoNetto.ToString("0.0000", CultureInfo.GetCultureInfo("it-IT"));
    }

    private static bool TryParseOptionalDecimal(string? value, out decimal? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseOptionalInt(string? value, out int? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseRequiredDecimal(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool IsParentSelection(GestionaleArticleSearchResult? article) =>
        article is not null
        && !article.ArticoloPadreOid.HasValue
        && !article.VarianteDettaglioOid1.HasValue
        && !article.VarianteDettaglioOid2.HasValue
        && string.IsNullOrWhiteSpace(article.BarcodeAlternativo);

    private bool TryBuildCurrentLocalSettings(out ReorderArticleSettings settings)
    {
        settings = new ReorderArticleSettings();

        if (ArticoloOid <= 0 || SelectedSearchResult is null)
        {
            return false;
        }

        if (!TryParseOptionalDecimal(PezziPerConfezioneText, out var pezziPerConfezione)
            || !TryParseOptionalDecimal(MultiploOrdineText, out var multiploOrdine)
            || !TryParseOptionalDecimal(LottoMinimoText, out var lottoMinimo)
            || !TryParseOptionalDecimal(PrezzoConfezioneText, out var prezzoConfezione)
            || !TryParseOptionalDecimal(PrezzoSingoloText, out var prezzoSingolo)
            || !TryParseOptionalDecimal(PrezzoVenditaLocaleText, out var prezzoVenditaRiferimento)
            || !TryParseOptionalDecimal(QuantitaPromoLocaleText, out var quantitaPromo)
            || !TryParseOptionalDecimal(PrezzoPromoLocaleText, out var prezzoPromo)
            || !TryParseOptionalInt(GiorniCoperturaText, out var giorniCopertura))
        {
            StatusMessage = "Uno o piu` valori locali non sono validi. Controlla numeri e separatori prima di salvare.";
            return false;
        }

        settings = CreateLocalSettingsForArticle(
            SelectedSearchResult,
            pezziPerConfezione,
            multiploOrdine,
            lottoMinimo,
            giorniCopertura,
            prezzoConfezione,
            prezzoSingolo,
            prezzoVenditaRiferimento,
            quantitaPromo,
            prezzoPromo);
        return true;
    }

    private ReorderArticleSettings CreateLocalSettingsForArticle(
        GestionaleArticleSearchResult article,
        decimal? pezziPerConfezione,
        decimal? multiploOrdine,
        decimal? lottoMinimo,
        int? giorniCopertura,
        decimal? prezzoConfezione,
        decimal? prezzoSingolo,
        decimal? prezzoVenditaRiferimento,
        decimal? quantitaPromo,
        decimal? prezzoPromo)
    {
        return new ReorderArticleSettings
        {
            SettingsKey = ReorderArticleSettings.BuildSettingsKey(
                article.Oid,
                article.VarianteDettaglioOid1,
                article.VarianteDettaglioOid2,
                article.BarcodeAlternativo),
            ArticoloOid = article.Oid,
            CodiceArticolo = article.CodiceArticolo,
            DescrizioneArticolo = article.Descrizione,
            BarcodeAlternativo = article.BarcodeAlternativo,
            VarianteDettaglioOid1 = article.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = article.VarianteDettaglioOid2,
            VarianteLabel = article.VarianteLabel,
            AcquistoAConfezione = AcquistoAConfezione,
            VenditaAPezzoSingolo = VenditaAPezzoSingolo,
            PezziPerConfezione = pezziPerConfezione,
            MultiploOrdine = multiploOrdine,
            LottoMinimoOrdine = lottoMinimo,
            GiorniCopertura = giorniCopertura,
            PrezzoConfezione = prezzoConfezione,
            PrezzoSingolo = prezzoSingolo,
            PrezzoVenditaRiferimento = prezzoVenditaRiferimento,
            QuantitaPromo = quantitaPromo,
            PrezzoPromo = prezzoPromo,
            Note = Note?.Trim() ?? string.Empty,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private static ReorderArticleSettings CloneLocalSettingsForArticle(ReorderArticleSettings source, GestionaleArticleSearchResult article)
    {
        return new ReorderArticleSettings
        {
            SettingsKey = ReorderArticleSettings.BuildSettingsKey(
                article.Oid,
                article.VarianteDettaglioOid1,
                article.VarianteDettaglioOid2,
                article.BarcodeAlternativo),
            ArticoloOid = article.Oid,
            CodiceArticolo = article.CodiceArticolo,
            DescrizioneArticolo = article.Descrizione,
            BarcodeAlternativo = article.BarcodeAlternativo,
            VarianteDettaglioOid1 = article.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = article.VarianteDettaglioOid2,
            VarianteLabel = article.VarianteLabel,
            AcquistoAConfezione = source.AcquistoAConfezione,
            VenditaAPezzoSingolo = source.VenditaAPezzoSingolo,
            PezziPerConfezione = source.PezziPerConfezione,
            MultiploOrdine = source.MultiploOrdine,
            LottoMinimoOrdine = source.LottoMinimoOrdine,
            GiorniCopertura = source.GiorniCopertura,
            PrezzoConfezione = source.PrezzoConfezione,
            PrezzoSingolo = source.PrezzoSingolo,
            PrezzoVenditaRiferimento = source.PrezzoVenditaRiferimento,
            QuantitaPromo = source.QuantitaPromo,
            PrezzoPromo = source.PrezzoPromo,
            Note = source.Note,
            UpdatedAt = source.UpdatedAt
        };
    }

    private decimal ResolveNormalizedPurchaseUnitCost(GestionaleArticlePurchaseQuickInfo? latestPurchase)
    {
        if (latestPurchase is null || latestPurchase.PrezzoUnitario <= 0)
        {
            return 0m;
        }

        if (!AcquistoAConfezione || !VenditaAPezzoSingolo)
        {
            return latestPurchase.PrezzoUnitario;
        }

        if (!TryParseOptionalDecimal(PezziPerConfezioneText, out var pezziPerConfezione)
            || !pezziPerConfezione.HasValue
            || pezziPerConfezione.Value <= 1)
        {
            return latestPurchase.PrezzoUnitario;
        }

        return decimal.Round(latestPurchase.PrezzoUnitario / pezziPerConfezione.Value, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal? ResolvePackagePrice(
        decimal? pezziPerConfezione,
        decimal? prezzoSingolo)
    {
        if (!pezziPerConfezione.HasValue || pezziPerConfezione.Value <= 1)
        {
            return prezzoSingolo;
        }

        if (!prezzoSingolo.HasValue || prezzoSingolo.Value <= 0)
        {
            return null;
        }

        return decimal.Round(prezzoSingolo.Value * pezziPerConfezione.Value, 2, MidpointRounding.AwayFromZero);
    }

    private void RefreshFilteredPrimaryUnitOptions()
    {
        ReplaceCollection(FilteredPrimaryUnitOptions, FilterLookupOptions(UnitOptions, _primaryUnitPickerSearchText));
    }

    private void RefreshFilteredSecondaryUnitOptions()
    {
        ReplaceCollection(FilteredSecondaryUnitOptions, FilterLookupOptions(SecondaryUnitOptions, _secondaryUnitPickerSearchText));
    }

    private void SyncSelectedPrimaryUnitOption()
    {
        var normalizedValue = (_unitaPrincipaleLegacyText ?? string.Empty).Trim();
        var matchedOption = string.IsNullOrWhiteSpace(normalizedValue)
            ? null
            : UnitOptions.FirstOrDefault(item => string.Equals(item.Label, normalizedValue, StringComparison.OrdinalIgnoreCase));

        if (!ReferenceEquals(_selectedPrimaryUnitOption, matchedOption))
        {
            _selectedPrimaryUnitOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedPrimaryUnitOption));
        }
    }

    private void SyncSelectedSecondaryUnitOption()
    {
        var normalizedValue = (_unitaSecondariaLegacyText ?? string.Empty).Trim();
        GestionaleLookupOption? matchedOption;

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            matchedOption = SecondaryUnitOptions.FirstOrDefault(item => item.Oid == 0);
        }
        else
        {
            matchedOption = SecondaryUnitOptions.FirstOrDefault(item => string.Equals(item.Label, normalizedValue, StringComparison.OrdinalIgnoreCase));
        }

        if (!ReferenceEquals(_selectedSecondaryUnitOption, matchedOption))
        {
            _selectedSecondaryUnitOption = matchedOption;
            NotifyPropertyChanged(nameof(SelectedSecondaryUnitOption));
        }
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
                terms.All(term => option.Label.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered.Take(80).ToList();
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

