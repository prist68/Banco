using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Banco.Riordino;
using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Banco.UI.Avalonia.Banco.Services;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Customers;
using Banco.Vendita.Fiscal;
using Banco.Vendita.PriceLists;

namespace Banco.UI.Avalonia.Banco.ViewModels;

public sealed class BancoSaleViewModel : ViewModelBase
{
    private readonly BancoSaleDataFacade _dataFacade;
    private readonly IBancoDocumentWorkflowService _documentWorkflowService;
    private readonly IReorderListRepository _reorderListRepository;
    private readonly IPosPaymentService _posPaymentService;
    private readonly IBancoSalePrintService _printService;
    private string _articleSearchText = string.Empty;
    private string _customerSearchText = string.Empty;
    private string _statusMessage = "Avvio vendita al banco Avalonia.";
    private string _legacyStatusTitle = "Legacy";
    private string _legacyStatusDetail = "Verifica in corso...";
    private bool _isLegacyOnline;
    private GestionaleArticleSearchResult? _selectedArticle;
    private GestionaleCustomerSummary? _selectedCustomer;
    private BancoSaleRowViewModel? _selectedRow;
    private GestionalePriceListSummary? _selectedPriceList;
    private string _selectedOperator = "Admin Banco";
    private decimal _cashAmount;
    private decimal _cardAmount;
    private decimal _suspendedAmount;
    private decimal _voucherAmount;
    private decimal _documentDiscount;
    private string? _lastAutoAssignedPaymentType;

    public BancoSaleViewModel(
        BancoSaleDataFacade dataFacade,
        IBancoDocumentWorkflowService documentWorkflowService,
        IReorderListRepository reorderListRepository,
        IPosPaymentService posPaymentService,
        IBancoSalePrintService printService)
    {
        _dataFacade = dataFacade;
        _documentWorkflowService = documentWorkflowService;
        _reorderListRepository = reorderListRepository;
        _posPaymentService = posPaymentService;
        _printService = printService;
        _reorderListRepository.CurrentListChanged += OnReorderListChanged;

        DocumentoLocaleCorrente = CreateNewOperationalDocument(_selectedOperator);
        Rows = [];
        Rows.CollectionChanged += Rows_OnCollectionChanged;

        ArticleResults = [];
        CustomerResults = [];
        Operators = ["Admin Banco", "Banco 1", "Banco 2"];
        PriceLists = [];

        SearchArticlesCommand = new RelayCommand(SearchArticlesAsync);
        SearchCustomersCommand = new RelayCommand(SearchCustomersAsync);
        AddSelectedArticleCommand = new RelayCommand(AddSelectedArticle, () => SelectedArticle is not null);
        NewDocumentCommand = new RelayCommand(NewDocument);
        ClearDocumentCommand = new RelayCommand(ClearDocument);
        SaveDocumentCommand = new RelayCommand(SaveDocumentAsync, CanPublishDocument);
        CourtesyCommand = new RelayCommand(PublishCourtesyAsync, CanPublishDocument);
        PrintPos80Command = new RelayCommand(PrintPos80Async, CanPublishDocument);
        PreviewPos80Command = new RelayCommand(PreviewPos80Async, CanPublishDocument);
        ReceiptCommand = new RelayCommand(PublishReceiptAsync, CanPublishReceipt);

        RecalculatePayments();
        _ = InitializeAsync();
    }

    public DocumentoLocale DocumentoLocaleCorrente { get; private set; }

    public ObservableCollection<BancoSaleRowViewModel> Rows { get; }

    public ObservableCollection<GestionaleArticleSearchResult> ArticleResults { get; }

    public ObservableCollection<GestionaleCustomerSummary> CustomerResults { get; }

    public ObservableCollection<string> Operators { get; }

    public ObservableCollection<GestionalePriceListSummary> PriceLists { get; }

    public BancoSaleDataFacade DataFacade => _dataFacade;

    public RelayCommand SearchArticlesCommand { get; }

    public RelayCommand SearchCustomersCommand { get; }

    public RelayCommand AddSelectedArticleCommand { get; }

    public RelayCommand NewDocumentCommand { get; }

    public RelayCommand ClearDocumentCommand { get; }

    public RelayCommand SaveDocumentCommand { get; }

    public RelayCommand CourtesyCommand { get; }

    public RelayCommand PrintPos80Command { get; }

    public RelayCommand PreviewPos80Command { get; }

    public RelayCommand ReceiptCommand { get; }

    public Func<GestionaleArticleSearchResult, GestionaleArticlePricingDetail, decimal, Task<decimal?>>? ArticleQuantitySelectionRequested { get; set; }

    public Func<GestionaleArticleSearchResult, decimal, Task<NegativeAvailabilityDecision>>? NegativeAvailabilityDecisionRequested { get; set; }

    public string ArticleSearchText
    {
        get => _articleSearchText;
        set => SetProperty(ref _articleSearchText, value);
    }

    public string CustomerSearchText
    {
        get => _customerSearchText;
        set
        {
            if (SetProperty(ref _customerSearchText, value))
            {
                _ = SearchCustomersLiveAsync(value);
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LegacyStatusTitle
    {
        get => _legacyStatusTitle;
        set => SetProperty(ref _legacyStatusTitle, value);
    }

    public string LegacyStatusDetail
    {
        get => _legacyStatusDetail;
        set => SetProperty(ref _legacyStatusDetail, value);
    }

    public bool IsLegacyOnline
    {
        get => _isLegacyOnline;
        set
        {
            if (SetProperty(ref _isLegacyOnline, value))
            {
                OnPropertyChanged(nameof(LegacyBadgeText));
            }
        }
    }

    public string LegacyBadgeText => IsLegacyOnline ? "Online" : "Offline";

    public GestionaleArticleSearchResult? SelectedArticle
    {
        get => _selectedArticle;
        set
        {
            if (SetProperty(ref _selectedArticle, value))
            {
                AddSelectedArticleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GestionaleCustomerSummary? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                ApplySelectedCustomerToDocument();
                OnPropertyChanged(nameof(CustomerDisplay));
                OnPropertyChanged(nameof(CustomerPointsDisplay));
            }
        }
    }

    public BancoSaleRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    public GestionalePriceListSummary? SelectedPriceList
    {
        get => _selectedPriceList;
        set
        {
            if (SetProperty(ref _selectedPriceList, value))
            {
                DocumentoLocaleCorrente.ImpostaListino(value?.Oid, value?.DisplayLabel);
                _ = RepriceDocumentRowsAsync();
            }
        }
    }

    public int? SelectedPriceListOid => SelectedPriceList?.Oid;

    public string SelectedOperator
    {
        get => _selectedOperator;
        set
        {
            if (SetProperty(ref _selectedOperator, string.IsNullOrWhiteSpace(value) ? "Admin Banco" : value.Trim()))
            {
                DocumentoLocaleCorrente.Operatore = _selectedOperator;
            }
        }
    }

    public string CustomerDisplay => SelectedCustomer?.DisplayName ?? "Cliente generico";

    public string CustomerPointsDisplay => SelectedCustomer?.PuntiLabel ?? "Punti n.d.";

    public decimal DocumentDiscount
    {
        get => _documentDiscount;
        set
        {
            if (SetProperty(ref _documentDiscount, value))
            {
                SyncDocumentDiscount();
                RecalculatePayments();
            }
        }
    }

    public decimal CashAmount
    {
        get => _cashAmount;
        set
        {
            if (SetProperty(ref _cashAmount, value))
            {
                SyncDocumentPayments();
                RefreshPaymentTotals();
            }
        }
    }

    public decimal CardAmount
    {
        get => _cardAmount;
        set
        {
            if (SetProperty(ref _cardAmount, value))
            {
                SyncDocumentPayments();
                RefreshPaymentTotals();
            }
        }
    }

    public decimal SuspendedAmount
    {
        get => _suspendedAmount;
        set
        {
            if (SetProperty(ref _suspendedAmount, value))
            {
                SyncDocumentPayments();
                RefreshPaymentTotals();
            }
        }
    }

    public decimal VoucherAmount
    {
        get => _voucherAmount;
        set
        {
            if (SetProperty(ref _voucherAmount, value))
            {
                SyncDocumentPayments();
                RefreshPaymentTotals();
            }
        }
    }

    public decimal Subtotal => DocumentoLocaleCorrente.TotaleDocumento;

    public decimal Total => DocumentoLocaleCorrente.TotaleDaIncassareLocale;

    public string TotalDisplay => $"{Total:N2} EUR";

    public string SubtotalDisplay => $"{Subtotal:N2} EUR";

    public string DocumentDiscountDisplay => $"{DocumentDiscount:N2} EUR";

    public decimal PaidTotal => DocumentoLocaleCorrente.TotaleIncassatoLocale + DocumentoLocaleCorrente.TotaleSospesoLocale;

    public decimal Residual => DocumentoLocaleCorrente.Residuo;

    public string PaidTotalDisplay => $"{PaidTotal:N2} EUR";

    public string ResidualDisplay => $"{Residual:N2} EUR";

    public string CashAmountDisplay => $"{CashAmount:N2} EUR";

    public string CardAmountDisplay => $"{CardAmount:N2} EUR";

    public string SuspendedAmountDisplay => $"{SuspendedAmount:N2} EUR";

    private async Task InitializeAsync()
    {
        var status = await _dataFacade.ProbeLegacyAsync();
        IsLegacyOnline = status.IsOnline;
        LegacyStatusTitle = status.Title;
        LegacyStatusDetail = status.Detail;
        StatusMessage = status.IsOnline
            ? "Legacy disponibile: le ricerche usano i servizi reali."
            : "Modalita offline/demo: la UI resta operativa senza DB legacy.";
        await LoadPriceListsAsync();
        await RefreshReorderMarkersAsync();
    }

    private async Task SearchArticlesAsync()
    {
        var result = await _dataFacade.SearchArticlesAsync(ArticleSearchText, SelectedPriceList?.Oid);
        ArticleResults.Clear();
        foreach (var article in result.Articles)
        {
            ArticleResults.Add(article);
        }

        IsLegacyOnline = result.IsOnline;
        StatusMessage = result.Message;
    }

    private async Task SearchCustomersAsync()
    {
        await SearchCustomersLiveAsync(CustomerSearchText);
    }

    public async Task SearchCustomersLiveAsync(string searchText)
    {
        var result = await _dataFacade.SearchCustomersAsync(searchText);
        CustomerResults.Clear();
        foreach (var customer in result.Customers)
        {
            CustomerResults.Add(customer);
        }

        if (SelectedCustomer is null && CustomerResults.Count == 1)
        {
            SelectedCustomer = CustomerResults[0];
            ApplySelectedCustomerToDocument();
        }

        IsLegacyOnline = result.IsOnline || IsLegacyOnline;
        StatusMessage = result.Message;
    }

    public async Task<bool> TryInsertDirectArticleAsync()
    {
        var result = await _dataFacade.FindArticleByCodeOrBarcodeAsync(ArticleSearchText, SelectedPriceList?.Oid);
        if (!result.IsFound || result.Article is null)
        {
            StatusMessage = result.Message;
            return false;
        }

        await AddArticleAsync(result.Article);
        ArticleSearchText = string.Empty;
        IsLegacyOnline = result.IsOnline;
        StatusMessage = result.Message;
        return true;
    }

    public void InsertArticleFromLookup(GestionaleArticleSearchResult article)
    {
        _ = AddArticleAsync(article);
        ArticleSearchText = string.Empty;
        StatusMessage = $"Articolo selezionato dal lookup: {article.DisplayLabel}";
    }

    public async void InsertArticleIntoRow(BancoSaleRowViewModel row, GestionaleArticleSearchResult article)
    {
        await ApplyArticleToRowAsync(row, article);
        RefreshTotals();
        RecalculatePayments();
        StatusMessage = $"Articolo selezionato nella griglia: {article.DisplayLabel}";
    }

    public async Task<bool> ResolveArticleForRowAsync(
        BancoSaleRowViewModel row,
        string searchText,
        bool allowFirstMatch = false)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        var directResult = await _dataFacade.FindArticleByCodeOrBarcodeAsync(searchText, SelectedPriceList?.Oid);
        if (directResult.IsFound && directResult.Article is not null)
        {
            await ApplyArticleToRowAsync(row, directResult.Article);
            IsLegacyOnline = directResult.IsOnline || IsLegacyOnline;
            StatusMessage = directResult.Message;
            RefreshTotals();
            RecalculatePayments();
            return true;
        }

        if (searchText.Trim().Length < 3)
        {
            return false;
        }

        var lookupResult = await _dataFacade.SearchArticlesAsync(searchText, SelectedPriceList?.Oid);
        IsLegacyOnline = lookupResult.IsOnline || IsLegacyOnline;

        if (lookupResult.Articles.Count == 1 || allowFirstMatch && lookupResult.Articles.Count > 0)
        {
            await ApplyArticleToRowAsync(row, lookupResult.Articles[0]);
            StatusMessage = $"Articolo risolto dalla griglia: {lookupResult.Articles[0].DisplayLabel}";
            RefreshTotals();
            RecalculatePayments();
            return true;
        }

        StatusMessage = lookupResult.Articles.Count == 0
            ? "Nessun articolo trovato dalla griglia."
            : $"Trovati {lookupResult.Articles.Count} articoli: premi Invio nel codice per scegliere.";
        return false;
    }

    public void RemoveRow(BancoSaleRowViewModel row)
    {
        if (!Rows.Remove(row))
        {
            return;
        }

        DocumentoLocaleCorrente.RimuoviRiga(row.Model.Id);
        if (ReferenceEquals(SelectedRow, row))
        {
            SelectedRow = null;
        }

        RefreshTotals();
        RecalculatePayments();
        StatusMessage = $"Riga eliminata: {row.Codice}";
    }

    public void ClearArticleCode(BancoSaleRowViewModel row)
    {
        row.ArticoloOid = null;
        row.Codice = string.Empty;
        row.Model.TipoRiga = TipoRigaDocumento.Manuale;
        row.Model.FlagManuale = true;
        row.Stato = "Manuale";
        if (string.IsNullOrWhiteSpace(row.Descrizione) || row.Descrizione == "-")
        {
            row.Descrizione = "Articolo manuale";
        }

        RefreshTotals();
        RecalculatePayments();
        StatusMessage = "Codice articolo rimosso dalla riga selezionata.";
    }

    public void AddManualRow()
    {
        var model = new RigaDocumentoLocale
        {
            OrdineRiga = DocumentoLocaleCorrente.Righe.Count + 1,
            TipoRiga = TipoRigaDocumento.Manuale,
            Quantita = 1,
            Descrizione = "Articolo manuale",
            UnitaMisura = "PZ",
            IvaOid = 1,
            AliquotaIva = 0,
            FlagManuale = true
        };
        DocumentoLocaleCorrente.AggiungiRiga(model);

        var row = new BancoSaleRowViewModel(model) { Stato = "Manuale" };
        Rows.Add(row);
        SelectedRow = row;
        RefreshTotals();
        RecalculatePayments();
        StatusMessage = "Riga manuale inserita.";
    }

    public void ChangeSelectedRowQuantity(decimal delta)
    {
        if (SelectedRow is null)
        {
            return;
        }

        SelectedRow.Quantita = Math.Max(1, SelectedRow.Quantita + delta);
        RefreshTotals();
        RecalculatePayments();
        StatusMessage = $"Quantita riga aggiornata: {SelectedRow.Quantita:N2}.";
    }

    public async Task<bool> AddSelectedRowToReorderListAsync()
    {
        if (SelectedRow is null)
        {
            return false;
        }

        var added = await AddRowToReorderListAsync(SelectedRow, ReorderReason.Manuale);
        if (added)
        {
            StatusMessage = $"Articolo {SelectedRow.Codice} aggiunto alla lista riordino.";
        }

        return added;
    }

    public async Task<bool> RemoveSelectedRowFromReorderListAsync()
    {
        if (SelectedRow?.Model.ArticoloOid is not int articleOid || articleOid <= 0)
        {
            return false;
        }

        try
        {
            var snapshot = await _reorderListRepository.GetCurrentListAsync();
            var item = snapshot.Items.FirstOrDefault(entry => IsSameReorderIdentity(entry, SelectedRow));
            if (item is null)
            {
                StatusMessage = "La riga selezionata non risulta nella lista riordino.";
                return false;
            }

            await _reorderListRepository.RemoveItemAsync(item.Id);
            await RefreshReorderMarkersAsync();
            StatusMessage = $"Articolo {SelectedRow.Codice} rimosso dalla lista riordino.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile rimuovere dalla lista riordino: {ex.Message}";
            return false;
        }
    }

    private void AddSelectedArticle()
    {
        if (SelectedArticle is null)
        {
            return;
        }

        _ = AddArticleAsync(SelectedArticle);
        ArticleSearchText = string.Empty;
        StatusMessage = $"Articolo aggiunto: {SelectedArticle.DisplayLabel}";
    }

    private async Task AddArticleAsync(GestionaleArticleSearchResult article)
    {
        var pricingDetail = await GetArticlePricingDetailAsync(article);
        var quantity = await ResolveArticleQuantityAsync(article, pricingDetail, NormalizeArticleQuantity(1, pricingDetail));
        if (!quantity.HasValue)
        {
            StatusMessage = "Inserimento articolo annullato.";
            return;
        }

        var availabilityDecision = await ResolveNegativeAvailabilityDecisionAsync(article, quantity.Value);
        if (availabilityDecision == NegativeAvailabilityDecision.Annulla)
        {
            StatusMessage = "Inserimento articolo annullato per giacenza non disponibile.";
            return;
        }

        var asManualRow = availabilityDecision == NegativeAvailabilityDecision.ConvertiInManuale;
        var addToReorder = availabilityDecision == NegativeAvailabilityDecision.VendiEAggiungiALista;
        var mergedRow = asManualRow ? null : TryMergeArticleRow(article, quantity.Value, pricingDetail);
        if (mergedRow is not null)
        {
            SelectedRow = Rows.FirstOrDefault(row => row.Model.Id == mergedRow.Id);
        }
        else
        {
            var unitPrice = ResolveArticleUnitPrice(pricingDetail, quantity.Value, article.PrezzoVendita);
            var model = CreateArticleRow(article, pricingDetail, quantity.Value, unitPrice, asManualRow);
            DocumentoLocaleCorrente.AggiungiRiga(model);
            var row = new BancoSaleRowViewModel(model);
            Rows.Add(row);
            SelectedRow = row;
        }

        if (addToReorder && SelectedRow is not null)
        {
            await AddRowToReorderListAsync(SelectedRow, ReorderReason.GiacenzaZero);
        }

        RefreshTotals();
        RecalculatePayments();
    }

    private async Task ApplyArticleToRowAsync(BancoSaleRowViewModel row, GestionaleArticleSearchResult article)
    {
        var pricingDetail = await GetArticlePricingDetailAsync(article);
        var quantity = await ResolveArticleQuantityAsync(
            article,
            pricingDetail,
            NormalizeArticleQuantity(row.Quantita <= 0 ? 1 : row.Quantita, pricingDetail));
        if (!quantity.HasValue)
        {
            StatusMessage = "Inserimento articolo annullato.";
            return;
        }

        var availabilityDecision = await ResolveNegativeAvailabilityDecisionAsync(article, quantity.Value);
        if (availabilityDecision == NegativeAvailabilityDecision.Annulla)
        {
            StatusMessage = "Inserimento articolo annullato per giacenza non disponibile.";
            return;
        }

        var unitPrice = ResolveArticleUnitPrice(pricingDetail, quantity.Value, article.PrezzoVendita);
        var asManualRow = availabilityDecision == NegativeAvailabilityDecision.ConvertiInManuale;
        row.ArticoloOid = asManualRow ? null : article.Oid;
        row.Codice = asManualRow ? string.Empty : article.CodiceArticolo;
        row.Descrizione = BuildDocumentRowDescription(article);
        row.UnitaMisura = pricingDetail?.UnitaMisuraPrincipale ?? "PZ";
        row.Quantita = quantity.Value;
        row.Prezzo = unitPrice;
        row.Disponibilita = article.Giacenza;
        row.AliquotaIva = article.AliquotaIva;
        row.Model.TipoRiga = asManualRow ? TipoRigaDocumento.Manuale : TipoRigaDocumento.Articolo;
        row.Model.FlagManuale = asManualRow;
        row.Model.BarcodeArticolo = asManualRow ? null : NormalizeBarcodeIdentity(article.BarcodeAlternativo);
        row.Model.VarianteDettaglioOid1 = asManualRow ? null : article.VarianteDettaglioOid1;
        row.Model.VarianteDettaglioOid2 = asManualRow ? null : article.VarianteDettaglioOid2;
        row.Model.IvaOid = article.IvaOid <= 0 ? 1 : article.IvaOid;
        row.Model.OrdineRiga = ResolveRowOrder(row.Model);
        row.Stato = asManualRow ? "Manuale" : article.Giacenza <= 0 ? "Disponibilita" : "Normale";

        if (availabilityDecision == NegativeAvailabilityDecision.VendiEAggiungiALista)
        {
            await AddRowToReorderListAsync(row, ReorderReason.GiacenzaZero);
        }
    }

    private async Task<decimal?> ResolveArticleQuantityAsync(
        GestionaleArticleSearchResult article,
        GestionaleArticlePricingDetail? pricingDetail,
        decimal defaultQuantity)
    {
        if (pricingDetail is null)
        {
            return defaultQuantity;
        }

        if (!pricingDetail.RichiedeSceltaQuantita || ArticleQuantitySelectionRequested is null)
        {
            return NormalizeArticleQuantity(defaultQuantity, pricingDetail);
        }

        var selectedQuantity = await ArticleQuantitySelectionRequested(article, pricingDetail, defaultQuantity);
        return selectedQuantity.HasValue
            ? NormalizeArticleQuantity(selectedQuantity.Value, pricingDetail)
            : null;
    }

    private async Task<NegativeAvailabilityDecision> ResolveNegativeAvailabilityDecisionAsync(
        GestionaleArticleSearchResult article,
        decimal quantity)
    {
        if (article.Giacenza > 0)
        {
            return NegativeAvailabilityDecision.ScaricaComunque;
        }

        return NegativeAvailabilityDecisionRequested is null
            ? NegativeAvailabilityDecision.ScaricaComunque
            : await NegativeAvailabilityDecisionRequested(article, quantity);
    }

    private void NewDocument()
    {
        DocumentoLocaleCorrente = CreateNewOperationalDocument(SelectedOperator);
        Rows.Clear();
        DocumentDiscount = 0;
        RecalculatePayments();
        RefreshTotals();
        StatusMessage = "Nuovo documento operativo aperto.";
    }

    private void ClearDocument()
    {
        DocumentoLocaleCorrente.AzzeraContenuto();
        Rows.Clear();
        RecalculatePayments();
        StatusMessage = "Righe documento azzerate.";
    }

    private void RecalculatePayments()
    {
        CashAmount = Total;
        CardAmount = 0;
        SuspendedAmount = 0;
        VoucherAmount = 0;
        _lastAutoAssignedPaymentType = "contanti";
        RefreshTotals();
    }

    public void ApplyPaymentAmount(string paymentType)
    {
        var normalizedType = paymentType.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(_lastAutoAssignedPaymentType) &&
            !_lastAutoAssignedPaymentType.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
        {
            ClearPaymentAmount(_lastAutoAssignedPaymentType);
        }

        var currentOtherPayments = normalizedType switch
        {
            "contanti" => CardAmount + SuspendedAmount + VoucherAmount,
            "carta" => CashAmount + SuspendedAmount + VoucherAmount,
            "sospeso" => CashAmount + CardAmount + VoucherAmount,
            "buoni" or "ticket" => CashAmount + CardAmount + SuspendedAmount,
            _ => PaidTotal
        };
        var amount = Math.Max(0, Total - currentOtherPayments);

        switch (normalizedType)
        {
            case "contanti":
                CashAmount = amount;
                break;
            case "carta":
                CardAmount = amount;
                break;
            case "sospeso":
                SuspendedAmount = amount;
                break;
            case "buoni":
            case "ticket":
                VoucherAmount = amount;
                break;
        }

        StatusMessage = $"Importo {paymentType} impostato a {amount:N2} EUR.";
        _lastAutoAssignedPaymentType = normalizedType;
        RefreshPaymentTotals();
        RaisePublishCommandsCanExecuteChanged();
    }

    private void ClearPaymentAmount(string paymentType)
    {
        switch (paymentType.Trim().ToLowerInvariant())
        {
            case "contanti":
                CashAmount = 0;
                break;
            case "carta":
            case "bancomat":
                CardAmount = 0;
                break;
            case "sospeso":
                SuspendedAmount = 0;
                break;
            case "buoni":
            case "ticket":
                VoucherAmount = 0;
                break;
        }
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
        RefreshPaymentTotals();
        RaisePublishCommandsCanExecuteChanged();
    }

    private void RefreshPaymentTotals()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalDisplay));
        OnPropertyChanged(nameof(PaidTotal));
        OnPropertyChanged(nameof(Residual));
        OnPropertyChanged(nameof(PaidTotalDisplay));
        OnPropertyChanged(nameof(ResidualDisplay));
        OnPropertyChanged(nameof(DocumentDiscountDisplay));
        OnPropertyChanged(nameof(CashAmountDisplay));
        OnPropertyChanged(nameof(CardAmountDisplay));
        OnPropertyChanged(nameof(SuspendedAmountDisplay));
    }

    private void Rows_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (BancoSaleRowViewModel row in e.OldItems)
            {
                row.PropertyChanged -= Row_OnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (BancoSaleRowViewModel row in e.NewItems)
            {
                AttachRow(row);
            }

            _ = RefreshReorderMarkersAsync();
        }
    }

    private void AttachRow(BancoSaleRowViewModel row)
    {
        row.PropertyChanged -= Row_OnPropertyChanged;
        row.PropertyChanged += Row_OnPropertyChanged;
    }

    private void Row_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BancoSaleRowViewModel.Quantita)
            or nameof(BancoSaleRowViewModel.Prezzo)
            or nameof(BancoSaleRowViewModel.Sconto)
            or nameof(BancoSaleRowViewModel.Importo))
        {
            RefreshTotals();
            RecalculatePayments();
            RaisePublishCommandsCanExecuteChanged();
        }
    }

    private bool CanPublishDocument()
    {
        return DocumentoLocaleCorrente.Righe.Count > 0;
    }

    private bool CanPublishReceipt()
    {
        return CanPublishDocument() && DocumentoLocaleCorrente.Residuo <= 0;
    }

    private async Task SaveDocumentAsync()
    {
        if (!CanPublishDocument())
        {
            StatusMessage = "Inserire almeno una riga prima di salvare sul legacy.";
            return;
        }

        var savePlan = BancoSavePlanResolver.Resolve(DocumentoLocaleCorrente);
        await PublishDocumentAsync(savePlan.CategoriaDocumentoBanco, "Salva", savePlan.PublishOptions);
    }

    private async Task PublishCourtesyAsync()
    {
        if (!CanPublishDocument())
        {
            StatusMessage = "Inserire almeno una riga prima di pubblicare la cortesia.";
            return;
        }

        SyncDocumentDiscount();
        SyncDocumentPayments();
        DocumentoLocaleCorrente.SegnaInChiusura();
        StatusMessage = "Cortesia: pubblicazione documento Banco sul legacy in corso...";

        try
        {
            var result = await _documentWorkflowService.PublishAsync(
                DocumentoLocaleCorrente,
                CategoriaDocumentoBanco.Cortesia);

            StatusMessage = "Cortesia pubblicata. Preparazione stampa POS80 in corso...";
            var printResult = await _printService.PrintPos80Async(DocumentoLocaleCorrente, SelectedCustomer);
            var successMessage = BuildPublishSuccessMessage("Cortesia", result, printResult.Message);

            if (!printResult.Succeeded)
            {
                StatusMessage = $"{successMessage} Stampa non completata.";
                return;
            }

            NewDocument();
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            DocumentoLocaleCorrente.Riapri();
            StatusMessage = $"Cortesia non completata: {ex.Message}";
        }
    }

    private Task PublishReceiptAsync()
    {
        return PublishReceiptCoreAsync();
    }

    private async Task PrintPos80Async()
    {
        if (!CanPublishDocument())
        {
            StatusMessage = "Inserire almeno una riga prima di stampare il POS80.";
            return;
        }

        SyncDocumentDiscount();
        SyncDocumentPayments();
        StatusMessage = "Stampa POS80: preparazione documento in corso...";

        try
        {
            var printResult = await _printService.PrintPos80Async(DocumentoLocaleCorrente, SelectedCustomer);
            StatusMessage = printResult.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stampa POS80 non completata: {ex.Message}";
        }
    }

    private async Task PreviewPos80Async()
    {
        if (!CanPublishDocument())
        {
            StatusMessage = "Inserire almeno una riga prima di aprire l'anteprima POS80.";
            return;
        }

        SyncDocumentDiscount();
        SyncDocumentPayments();
        StatusMessage = "Anteprima POS80: preparazione documento in corso...";

        try
        {
            var previewResult = await _printService.PreviewPos80Async(DocumentoLocaleCorrente, SelectedCustomer);
            StatusMessage = previewResult.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Anteprima POS80 non completata: {ex.Message}";
        }
    }

    private async Task PublishReceiptCoreAsync()
    {
        if (!CanPublishReceipt())
        {
            StatusMessage = $"Pagamenti incompleti. Residuo da coprire: {Residual:N2} EUR.";
            return;
        }

        SyncDocumentDiscount();
        SyncDocumentPayments();

        var posMessage = await ExecuteCardPaymentIfRequiredAsync();
        if (posMessage is null)
        {
            return;
        }

        await PublishDocumentAsync(CategoriaDocumentoBanco.Scontrino, "Scontrino", successDetail: posMessage);
    }

    private async Task PublishDocumentAsync(
        CategoriaDocumentoBanco category,
        string operationName,
        BancoPublishOptions? publishOptions = null,
        string? successDetail = null)
    {
        if (!CanPublishDocument())
        {
            StatusMessage = "Inserire almeno una riga prima di pubblicare il documento Banco.";
            return;
        }

        SyncDocumentDiscount();
        SyncDocumentPayments();
        DocumentoLocaleCorrente.SegnaInChiusura();
        StatusMessage = $"{operationName}: pubblicazione documento Banco sul legacy in corso...";

        try
        {
            var result = await _documentWorkflowService.PublishAsync(
                DocumentoLocaleCorrente,
                category,
                publishOptions);

            var successMessage = BuildPublishSuccessMessage(operationName, result, successDetail);
            NewDocument();
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            DocumentoLocaleCorrente.Riapri();
            StatusMessage = $"{operationName} non completato: {ex.Message}";
        }
    }

    private async Task<string?> ExecuteCardPaymentIfRequiredAsync()
    {
        if (CardAmount <= 0)
        {
            return string.Empty;
        }

        StatusMessage = $"Invio di {CardAmount:N2} EUR al POS Nexi in corso...";
        try
        {
            var posResult = await _posPaymentService.ExecutePaymentAsync(CardAmount);
            if (posResult.IsSuccess)
            {
                return string.IsNullOrWhiteSpace(posResult.Message)
                    ? "Pagamento POS autorizzato."
                    : posResult.Message;
            }

            StatusMessage = posResult.RequiresManualInterventionWarning
                ? $"Pagamento POS da verificare manualmente: {posResult.Message}"
                : $"Pagamento POS non autorizzato: {posResult.Message}";
            return null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Pagamento POS non completato: {ex.Message}";
            return null;
        }
    }

    private static string BuildPublishSuccessMessage(string operationName, FiscalizationResult result, string? successDetail)
    {
        var message = $"{operationName} completato: documento {result.NumeroDocumentoGestionale}/{result.AnnoDocumentoGestionale} pubblicato sul legacy.";
        if (!string.IsNullOrWhiteSpace(successDetail))
        {
            message += $" {successDetail}";
        }

        if (!string.IsNullOrWhiteSpace(result.WinEcrMessage))
        {
            message += $" {result.WinEcrMessage}";
        }

        if (!string.IsNullOrWhiteSpace(result.TechnicalWarningMessage))
        {
            message += $" Avviso: {result.TechnicalWarningMessage}";
        }

        return message;
    }

    private void RaisePublishCommandsCanExecuteChanged()
    {
        SaveDocumentCommand.RaiseCanExecuteChanged();
        CourtesyCommand.RaiseCanExecuteChanged();
        PrintPos80Command.RaiseCanExecuteChanged();
        PreviewPos80Command.RaiseCanExecuteChanged();
        ReceiptCommand.RaiseCanExecuteChanged();
    }

    private static DocumentoLocale CreateNewOperationalDocument(string operatore)
    {
        return DocumentoLocale.Reidrata(
            Guid.NewGuid(),
            StatoDocumentoLocale.BozzaLocale,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            string.IsNullOrWhiteSpace(operatore) ? "Admin Banco" : operatore.Trim(),
            "Cliente generico",
            null,
            null,
            null,
            string.Empty,
            ModalitaChiusuraDocumento.BozzaLocale,
            CategoriaDocumentoBanco.Indeterminata,
            false,
            StatoFiscaleBanco.Nessuno,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            []);
    }

    private RigaDocumentoLocale CreateArticleRow(
        GestionaleArticleSearchResult article,
        GestionaleArticlePricingDetail? pricingDetail,
        decimal quantity,
        decimal unitPrice,
        bool asManualRow)
    {
        return new RigaDocumentoLocale
        {
            OrdineRiga = DocumentoLocaleCorrente.Righe.Count + 1,
            TipoRiga = asManualRow ? TipoRigaDocumento.Manuale : TipoRigaDocumento.Articolo,
            ArticoloOid = asManualRow ? null : article.Oid,
            CodiceArticolo = asManualRow ? null : article.CodiceArticolo,
            BarcodeArticolo = asManualRow ? null : NormalizeBarcodeIdentity(article.BarcodeAlternativo),
            VarianteDettaglioOid1 = asManualRow ? null : article.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = asManualRow ? null : article.VarianteDettaglioOid2,
            Descrizione = BuildDocumentRowDescription(article),
            UnitaMisura = pricingDetail?.UnitaMisuraPrincipale ?? "PZ",
            Quantita = quantity,
            DisponibilitaRiferimento = article.Giacenza,
            PrezzoUnitario = unitPrice,
            IvaOid = article.IvaOid <= 0 ? 1 : article.IvaOid,
            AliquotaIva = article.AliquotaIva,
            FlagManuale = asManualRow
        };
    }

    private async Task<bool> AddRowToReorderListAsync(BancoSaleRowViewModel row, ReorderReason reason)
    {
        if (!row.Model.ArticoloOid.HasValue)
        {
            StatusMessage = "La riga manuale non puo essere aggiunta alla lista riordino.";
            return false;
        }

        try
        {
            await _reorderListRepository.AddOrIncrementItemAsync(new ReorderListItem
            {
                ArticoloOid = row.Model.ArticoloOid,
                CodiceArticolo = row.Codice,
                Descrizione = row.Descrizione,
                Quantita = row.Quantita,
                QuantitaDaOrdinare = row.Quantita,
                UnitaMisura = row.UnitaMisura,
                IvaOid = row.Model.IvaOid <= 0 ? 1 : row.Model.IvaOid,
                Motivo = reason,
                Stato = ReorderItemStatus.DaOrdinare,
                Operatore = DocumentoLocaleCorrente.Operatore,
                Note = reason == ReorderReason.GiacenzaZero
                    ? "Inserito da vendita Avalonia per giacenza non disponibile"
                    : "Inserito manualmente da vendita Avalonia"
            });
            await RefreshReorderMarkersAsync();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Articolo inserito, ma non aggiunto alla lista riordino: {ex.Message}";
            return false;
        }
    }

    private async Task RefreshReorderMarkersAsync()
    {
        try
        {
            var snapshot = await _reorderListRepository.GetCurrentListAsync();
            foreach (var row in Rows)
            {
                row.IsInReorderList = snapshot.Items.Any(item => IsSameReorderIdentity(item, row));
            }
        }
        catch
        {
            foreach (var row in Rows)
            {
                row.IsInReorderList = false;
            }
        }
    }

    private void OnReorderListChanged()
    {
        _ = RefreshReorderMarkersAsync();
    }

    private static bool IsSameReorderIdentity(ReorderListItem item, BancoSaleRowViewModel row)
    {
        if (row.Model.ArticoloOid is not int articleOid || articleOid <= 0)
        {
            return false;
        }

        return item.ArticoloOid == articleOid &&
               string.Equals(NormalizeReorderText(item.CodiceArticolo), NormalizeReorderText(row.Codice), StringComparison.Ordinal) &&
               string.Equals(NormalizeReorderText(item.Descrizione), NormalizeReorderText(row.Descrizione), StringComparison.Ordinal);
    }

    private static string NormalizeReorderText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private async Task LoadPriceListsAsync()
    {
        var result = await _dataFacade.GetSalesPriceListsAsync();
        PriceLists.Clear();
        foreach (var priceList in result.PriceLists)
        {
            PriceLists.Add(priceList);
        }

        SelectedPriceList = ResolvePreferredPriceList();
        IsLegacyOnline = result.IsOnline || IsLegacyOnline;
    }

    private GestionalePriceListSummary? ResolvePreferredPriceList()
    {
        if (SelectedCustomer?.ClienteListinoOid is int customerPriceListOid)
        {
            var customerMatch = PriceLists.FirstOrDefault(item => item.Oid == customerPriceListOid);
            if (customerMatch is not null)
            {
                return customerMatch;
            }
        }

        return PriceLists.FirstOrDefault(item => item.IsWeb)
            ?? PriceLists.FirstOrDefault(item => item.IsDefault)
            ?? PriceLists.FirstOrDefault();
    }

    private async Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(GestionaleArticleSearchResult article)
    {
        var result = await _dataFacade.GetArticlePricingDetailAsync(article, SelectedPriceList?.Oid);
        IsLegacyOnline = result.IsOnline || IsLegacyOnline;
        if (!result.IsOnline)
        {
            StatusMessage = result.Message;
        }

        return result.Detail;
    }

    private RigaDocumentoLocale? TryMergeArticleRow(
        GestionaleArticleSearchResult article,
        decimal quantityToAdd,
        GestionaleArticlePricingDetail? pricingDetail)
    {
        var barcode = NormalizeBarcodeIdentity(article.BarcodeAlternativo);
        var existing = DocumentoLocaleCorrente.Righe.FirstOrDefault(row =>
            row.TipoRiga == TipoRigaDocumento.Articolo &&
            !row.FlagManuale &&
            !row.IsPromoRow &&
            IsMatchingArticleRowForMerge(row, article, barcode));

        if (existing is null)
        {
            return null;
        }

        existing.Quantita = NormalizeArticleQuantity(existing.Quantita + quantityToAdd, pricingDetail);
        existing.DisponibilitaRiferimento = article.Giacenza;
        existing.PrezzoUnitario = ResolveArticleUnitPrice(pricingDetail, existing.Quantita, article.PrezzoVendita);

        var viewRow = Rows.FirstOrDefault(row => row.Model.Id == existing.Id);
        if (viewRow is not null)
        {
            viewRow.Quantita = existing.Quantita;
            viewRow.Disponibilita = existing.DisponibilitaRiferimento;
            viewRow.Prezzo = existing.PrezzoUnitario;
        }

        return existing;
    }

    private static bool IsMatchingArticleRowForMerge(
        RigaDocumentoLocale row,
        GestionaleArticleSearchResult article,
        string? barcode)
    {
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            return string.Equals(NormalizeBarcodeIdentity(row.BarcodeArticolo), barcode, StringComparison.OrdinalIgnoreCase);
        }

        if (article.IsVariante)
        {
            return row.ArticoloOid == article.Oid &&
                   (row.VarianteDettaglioOid1 ?? 0) == (article.VarianteDettaglioOid1 ?? 0) &&
                   (row.VarianteDettaglioOid2 ?? 0) == (article.VarianteDettaglioOid2 ?? 0);
        }

        return row.ArticoloOid == article.Oid;
    }

    private async Task RepriceDocumentRowsAsync()
    {
        foreach (var row in Rows.Where(row => row.ArticoloOid.HasValue && !row.Model.FlagManuale))
        {
            var article = BuildSearchResultFromRow(row.Model);
            var pricingDetail = await GetArticlePricingDetailAsync(article);
            row.Quantita = NormalizeArticleQuantity(row.Quantita, pricingDetail);
            row.Prezzo = ResolveArticleUnitPrice(pricingDetail, row.Quantita, row.Prezzo);
            row.UnitaMisura = pricingDetail?.UnitaMisuraPrincipale ?? row.UnitaMisura;
        }

        RefreshTotals();
        RecalculatePayments();
    }

    private static GestionaleArticleSearchResult BuildSearchResultFromRow(RigaDocumentoLocale row)
    {
        return new GestionaleArticleSearchResult
        {
            Oid = row.ArticoloOid.GetValueOrDefault(),
            CodiceArticolo = row.CodiceArticolo ?? string.Empty,
            Descrizione = row.Descrizione,
            PrezzoVendita = row.PrezzoUnitario,
            Giacenza = row.DisponibilitaRiferimento,
            IvaOid = row.IvaOid,
            AliquotaIva = row.AliquotaIva,
            BarcodeAlternativo = row.BarcodeArticolo,
            VarianteDettaglioOid1 = row.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = row.VarianteDettaglioOid2
        };
    }

    private static decimal NormalizeArticleQuantity(decimal requestedQuantity, GestionaleArticlePricingDetail? pricingDetail)
    {
        var normalized = requestedQuantity <= 0 ? 1 : requestedQuantity;
        if (pricingDetail is null)
        {
            return normalized;
        }

        var minimumQuantity = pricingDetail.QuantitaMinimaVendita <= 0 ? 1 : pricingDetail.QuantitaMinimaVendita;
        normalized = Math.Max(normalized, minimumQuantity);

        var multipleQuantity = pricingDetail.QuantitaMultiplaVendita <= 0 ? 1 : pricingDetail.QuantitaMultiplaVendita;
        if (multipleQuantity > 1)
        {
            normalized = Math.Ceiling(normalized / multipleQuantity) * multipleQuantity;
        }

        return normalized;
    }

    private static decimal ResolveArticleUnitPrice(
        GestionaleArticlePricingDetail? pricingDetail,
        decimal quantity,
        decimal fallbackUnitPrice)
    {
        if (pricingDetail is null || pricingDetail.FascePrezzoQuantita.Count == 0)
        {
            return fallbackUnitPrice;
        }

        var tier = pricingDetail.FascePrezzoQuantita
            .Where(item => quantity >= item.QuantitaMinima)
            .OrderByDescending(item => item.QuantitaMinima)
            .FirstOrDefault();

        return tier?.PrezzoUnitario
            ?? (fallbackUnitPrice > 0
                ? fallbackUnitPrice
                : pricingDetail.FascePrezzoQuantita.OrderBy(item => item.QuantitaMinima).First().PrezzoUnitario);
    }

    private static string BuildDocumentRowDescription(GestionaleArticleSearchResult article)
    {
        return string.IsNullOrWhiteSpace(article.VarianteLabel)
            ? article.Descrizione
            : $"{article.Descrizione} - {article.VarianteLabel}";
    }

    private static string? NormalizeBarcodeIdentity(string? barcode)
    {
        return string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim().ToUpperInvariant();
    }

    private int ResolveRowOrder(RigaDocumentoLocale model)
    {
        return model.OrdineRiga > 0
            ? model.OrdineRiga
            : DocumentoLocaleCorrente.Righe.Count + 1;
    }

    private void ApplySelectedCustomerToDocument()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        DocumentoLocaleCorrente.ImpostaCliente(SelectedCustomer.Oid, SelectedCustomer.DisplayLabel);
        OnPropertyChanged(nameof(CustomerDisplay));
        OnPropertyChanged(nameof(CustomerPointsDisplay));
    }

    private void SyncDocumentDiscount()
    {
        DocumentoLocaleCorrente.ImpostaScontoDocumento(DocumentDiscount);
    }

    private void SyncDocumentPayments()
    {
        var now = DateTimeOffset.Now;
        var payments = new List<PagamentoLocale>();
        if (CashAmount > 0)
        {
            payments.Add(new PagamentoLocale { TipoPagamento = "contanti", Importo = CashAmount, DataOra = now });
        }

        if (CardAmount > 0)
        {
            payments.Add(new PagamentoLocale { TipoPagamento = "carta", Importo = CardAmount, DataOra = now.AddSeconds(1) });
        }

        if (SuspendedAmount > 0)
        {
            payments.Add(new PagamentoLocale { TipoPagamento = "sospeso", Importo = SuspendedAmount, DataOra = now.AddSeconds(2) });
        }

        if (VoucherAmount > 0)
        {
            payments.Add(new PagamentoLocale { TipoPagamento = "buoni", Importo = VoucherAmount, DataOra = now.AddSeconds(3) });
        }

        DocumentoLocaleCorrente.SostituisciPagamenti(payments);
    }
}
