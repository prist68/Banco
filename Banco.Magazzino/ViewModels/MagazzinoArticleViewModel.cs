using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Banco.Core.Infrastructure;
using Banco.Riordino;
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
    private string _quantitaMinimaVenditaLegacyText = "1";
    private string _quantitaMultiplaVenditaLegacyText = "1";
    private string _ultimoAcquistoLabel = "Nessun ultimo acquisto trovato.";
    private bool _acquistoAConfezione;
    private bool _venditaAPezzoSingolo;
    private string _pezziPerConfezioneText = string.Empty;
    private string _multiploOrdineText = string.Empty;
    private string _lottoMinimoText = string.Empty;
    private string _giorniCoperturaText = string.Empty;
    private string _note = string.Empty;
    private string _localSettingsInfo = "Nessun parametro locale salvato.";

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
        SearchCommand = new RelayCommand(async () => await SearchAsync(), () => !IsLoading && !string.IsNullOrWhiteSpace(SearchText));
        RefreshCurrentArticleCommand = new RelayCommand(async () => await ReloadCurrentArticleAsync(), () => !IsLoading && SelectedSearchResult is not null);
        SaveLegacyCommand = new RelayCommand(async () => await SaveLegacyAsync(), () => !IsLoading && ArticoloOid > 0);
        SaveLocalSettingsCommand = new RelayCommand(async () => await SaveLocalSettingsAsync(), () => !IsLoading && ArticoloOid > 0);
        ApplyLocalSettingsToChildrenCommand = new RelayCommand(async () => await ApplyLocalSettingsToChildrenAsync(), () => !IsLoading && CanApplyLocalSettingsToChildren);
        CopyLocalSettingsCommand = new RelayCommand(CopyLocalSettings, () => ArticoloOid > 0);
        PasteLocalSettingsCommand = new RelayCommand(PasteLocalSettings, () => ArticoloOid > 0);
    }

    public string Titolo => "Articolo magazzino";

    public ObservableCollection<MagazzinoArticleSearchRowViewModel> SearchRows { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand RefreshCurrentArticleCommand { get; }

    public RelayCommand SaveLegacyCommand { get; }

    public RelayCommand SaveLocalSettingsCommand { get; }

    public RelayCommand ApplyLocalSettingsToChildrenCommand { get; }

    public RelayCommand CopyLocalSettingsCommand { get; }

    public RelayCommand PasteLocalSettingsCommand { get; }

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
                NotifyPropertyChanged(nameof(LocalSettingsScopeInfo));
                NotifyPropertyChanged(nameof(CanApplyLocalSettingsToChildren));
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
        set => SetProperty(ref _unitaPrincipaleLegacyText, value);
    }

    public string UnitaSecondariaLegacyText
    {
        get => _unitaSecondariaLegacyText;
        set => SetProperty(ref _unitaSecondariaLegacyText, value);
    }

    public string RapportoUnitaLegacyText
    {
        get => _rapportoUnitaLegacyText;
        set => SetProperty(ref _rapportoUnitaLegacyText, value);
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

    public string LocalSettingsScopeInfo =>
        SelectedSearchResult is null
            ? string.Empty
            : IsParentSelection(SelectedSearchResult)
                ? "I valori salvati sul padre diventano il default di tutte le varianti senza override locale, comprese quelle future."
                : "Questa variante puo` avere un override proprio. Se non salvi qui, continua a usare i valori ereditati dal padre.";

    public bool CanApplyLocalSettingsToChildren =>
        SelectedSearchResult is not null &&
        IsParentSelection(SelectedSearchResult) &&
        SearchRows.Any(row => row.IsChild && row.Article is not null && row.FamilyOid == SelectedSearchRow?.FamilyOid);

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
                    ? $"Articolo {bestMatch.CodiceArticolo} aperto automaticamente."
                    : $"{SearchRows.Count} righe disponibili. Il padre riepiloga i totali, sotto trovi le varianti.";
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
            StatusMessage = $"Apro la scheda articolo {article.CodiceArticolo} dal legacy...";

            var pricing = await _articleReadService.GetArticlePricingDetailAsync(article);
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

            DescrizioneLegacyText = article.Descrizione;
            PrezzoVenditaLegacyText = PrezzoVenditaLabel;
            UnitaPrincipaleLegacyText = UnitaPrincipaleLabel;
            UnitaSecondariaLegacyText = UnitaSecondariaLabel == "-" ? string.Empty : UnitaSecondariaLabel;
            RapportoUnitaLegacyText = RapportoUnitaLabel == "-" ? string.Empty : RapportoUnitaLabel;
            QuantitaMinimaVenditaLegacyText = QuantitaMinimaVenditaLabel;
            QuantitaMultiplaVenditaLegacyText = QuantitaMultiplaVenditaLabel;

            LoadLocalFields(localSettings, pricing);

            StatusMessage = $"Scheda articolo {article.CodiceArticolo} caricata. Legacy e parametri locali sono allineati sullo stesso articolo.";
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

    private void LoadLocalFields(ReorderArticleSettings? localSettings, GestionaleArticlePricingDetail? pricing)
    {
        AcquistoAConfezione = localSettings?.AcquistoAConfezione ?? pricing?.HasSecondaryUnit == true;
        VenditaAPezzoSingolo = localSettings?.VenditaAPezzoSingolo ?? pricing?.HasSecondaryUnit == true;
        PezziPerConfezioneText = FormatDecimal(localSettings?.PezziPerConfezione ?? (pricing?.HasSecondaryUnit == true ? pricing.MoltiplicatoreUnitaSecondaria : null));
        MultiploOrdineText = FormatDecimal(localSettings?.MultiploOrdine);
        LottoMinimoText = FormatDecimal(localSettings?.LottoMinimoOrdine);
        GiorniCoperturaText = localSettings?.GiorniCopertura?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
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

        try
        {
            IsLoading = true;
            await _settingsRepository.SaveAsync(settings);
            if (IsParentSelection(SelectedSearchResult))
            {
                LocalSettingsInfo = $"Parametri locali padre aggiornati il {settings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}. Restano il default di tutte le varianti senza override.";
                StatusMessage = $"Parametri locali padre salvati per {SelectedSearchResult.CodiceArticolo}: le varianti senza override, anche future, useranno questi valori.";
            }
            else
            {
                LocalSettingsInfo = $"Override locale variante aggiornato il {settings.UpdatedAt.LocalDateTime:dd/MM/yyyy HH:mm}.";
                StatusMessage = $"Override locale salvato solo per la variante {SelectedSearchResult.CodiceArticolo}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore salvataggio parametri locali: {ex.Message}";
            _logService.Error(nameof(MagazzinoArticleViewModel), $"Errore durante il salvataggio parametri locali dell'articolo {SelectedSearchResult.CodiceArticolo}.", ex);
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
        AcquistoAConfezione = false;
        VenditaAPezzoSingolo = false;
        PezziPerConfezioneText = string.Empty;
        MultiploOrdineText = string.Empty;
        LottoMinimoText = string.Empty;
        GiorniCoperturaText = string.Empty;
        Note = string.Empty;
        LocalSettingsInfo = "Nessun parametro locale salvato.";
        NotifyPropertyChanged(nameof(LocalSettingsScopeInfo));
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
            giorniCopertura);
        return true;
    }

    private ReorderArticleSettings CreateLocalSettingsForArticle(
        GestionaleArticleSearchResult article,
        decimal? pezziPerConfezione,
        decimal? multiploOrdine,
        decimal? lottoMinimo,
        int? giorniCopertura)
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
            Note = source.Note,
            UpdatedAt = source.UpdatedAt
        };
    }
}
