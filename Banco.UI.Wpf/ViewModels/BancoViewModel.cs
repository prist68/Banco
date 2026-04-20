using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Banco.Vendita.Configuration;
using Banco.Vendita.Customers;
using Banco.Vendita.Documents;
using Banco.Vendita.Fiscal;
using Banco.Vendita.Operators;
using Banco.Vendita.Pos;
using Banco.Vendita.PriceLists;
using Banco.Vendita.Points;
using Banco.Riordino;
using Banco.Stampa;
using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;

namespace Banco.UI.Wpf.ViewModels;

public sealed class BancoViewModel : ViewModelBase
{
    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly IGestionaleCustomerReadService _customerReadService;
    private readonly IGestionaleOperatorReadService _operatorReadService;
    private readonly IGestionalePriceListReadService _priceListReadService;
    private readonly IGestionalePointsReadService _pointsReadService;
    private readonly IPointsRewardRuleService _rewardRuleService;
    private readonly IPointsPromotionEvaluationService _promotionEvaluationService;
    private readonly IPointsPromotionDocumentService _promotionDocumentService;
    private readonly IPointsPromotionHistoryService _promotionHistoryService;
    private readonly ILocalDocumentRepository _localDocumentRepository;
    private readonly IGestionaleDocumentDeleteService _documentDeleteService;
    private readonly IBancoDocumentWorkflowService _bancoDocumentWorkflowService;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IPosPaymentService _posPaymentService;
    private readonly IPosProcessLogService _processLogService;
    private readonly IBancoPosPrintService _bancoPosPrintService;
    private readonly IWinEcrAutoRunService _winEcrAutoRunService;
    private readonly IReorderListRepository _reorderListRepository;
    private readonly RelayCommand _incrementaQuantitaRigaSelezionataCommand;
    private readonly RelayCommand _decrementaQuantitaRigaSelezionataCommand;
    private readonly Dictionary<string, bool> _columnVisibility = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GestionaleArticlePricingDetail> _articlePricingDetails = new();
    private readonly Task _layoutInitializationTask;
    private DocumentoLocale? _documentoLocale;
    private GestionaleDocumentDetail? _documentoGestionaleOrigine;
    private BancoDocumentoAccessResolution _documentAccessResolution = BancoDocumentoAccessResolver.Resolve(
        localMetadata: null,
        legacyDocumentOid: null,
        legacyDocumentLabel: null,
        legacyHasScontrinatoPayments: false);
    private bool _isScontrinato;
    private string _statoDocumento = "Nessun documento aperto";
    private string _searchArticoloText = string.Empty;
    private string _searchClienteText = string.Empty;
    private GestionaleArticleSearchResult? _articoloSelezionato;
    private GestionaleCustomerSummary? _clienteSelezionato;
    private bool _isClienteConfermato;
    private GestionaleOperatorSummary? _operatoreSelezionato;
    private GestionalePriceListSummary? _listinoSelezionato;
    private bool _isApplyingSelectedPriceList;
    private GestionalePointsCampaignSummary? _promoCampaignSummary;
    private IReadOnlyList<PointsRewardRule> _promoRewardRules = [];
    private PromotionEvaluationResult _promoEligibility = new()
    {
        EventType = PromotionEventType.NotEligible,
        Title = "Nessuna promo configurata",
        Message = "Completa la regola premio nel modulo Punti per usarla in vendita."
    };
    private GestionaleArticlePurchaseQuickInfo? _ultimoAcquistoArticolo;
    private RigaDocumentoLocaleViewModel? _rigaSelezionata;
    private int? _lastSelectedArticleOid;
    private string _statusMessage = "Apri un documento dal gestionale oppure crea una nuova scheda Banco.";
    private bool _isArticlePopupOpen;
    private bool _isCustomerPopupOpen;
    private CancellationTokenSource? _articleSearchCts;
    private CancellationTokenSource? _customerSearchCts;
    private CancellationTokenSource? _purchaseQuickInfoCts;
    private CancellationTokenSource? _promoRefreshCts;
    private CancellationTokenSource? _posPaymentCts;
    private double _rigaColumnWidth = 70;
    private double _codiceColumnWidth = 120;
    private double _descrizioneColumnWidth = 420;
    private double _quantitaColumnWidth = 100;
    private double _prezzoColumnWidth = 100;
    private double _ivaColumnWidth = 70;
    private double _tipoColumnWidth = 90;
    private double _tipoRigaColumnWidth = 80;
    private double _scontoColumnWidth = 90;
    private double _importoColumnWidth = 110;
    private double _azioniColumnWidth = 80;
    private int _rigaDisplayIndex;
    private int _codiceDisplayIndex = 1;
    private int _descrizioneDisplayIndex = 2;
    private int _quantitaDisplayIndex = 3;
    private int _prezzoDisplayIndex = 4;
    private int _ivaDisplayIndex = 5;
    private int _tipoDisplayIndex = 6;
    private int _scontoDisplayIndex = 7;
    private int _importoDisplayIndex = 8;
    private int _tipoRigaDisplayIndex = 9;
    private int _azioniDisplayIndex = 10;
    private bool _isUpdatingSearchClienteText;
    private bool _isUpdatingPaymentInputs;
    private bool _paymentInputsDirty;
    private string? _lastAutoAssignedPaymentType;
    private string _scontoPagamentoText = string.Empty;
    private string _contantiPagamentoText = string.Empty;
    private string _bancomatPagamentoText = string.Empty;
    private string _sospesoPagamentoText = string.Empty;
    private string _buoniPagamentoText = string.Empty;
    private int? _previewNumeroDocumentoGestionale;
    private int? _previewAnnoDocumentoGestionale;
    private bool _isPaymentOperationInProgress;
    private bool _hasPendingLocalChanges;
    private bool _consultationEditOverrideEnabled;
    private bool _isOperationPopupVisible;
    private string _operationPopupTitle = "Operazione in corso";
    private string _operationPopupMessage = string.Empty;
    private string _operationPopupFooterText = "L'operazione viene completata automaticamente.";
    private bool _operationPopupIsBusy = true;
    private bool _operationPopupIsSuccess;
    private bool _operationPopupCanCancel;
    private bool _operationPopupCanRetry;
    private TaskCompletionSource<bool>? _operationPopupRetryTcs;
    private int? _lastAutoAddedArticleOid;
    private string _lastAutoAddedSearchText = string.Empty;
    private DateTimeOffset _lastAutoAddedAt;
    private int _articleSearchRequestVersion;
    private int _customerSearchRequestVersion;
    private int _purchaseQuickInfoRequestVersion;
    private int _promoRefreshRequestVersion;

    public event Action? FocusArticleSearchRequested;
    public event Action<StoricoAcquistiViewModel>? OpenStoricoAcquistiRequested;
    public event Action<PurchaseHistoryViewModel>? OpenPurchaseHistoryRequested;
    public event Action? ShowDocumentListRequested;
    public event Action? OpenSettingsRequested;
    public event Action? OpenCashRegisterRequested;
    public event Action<string>? Pos80PreviewRequested;
    public event Action<BancoLegacyPublishNotification>? OfficialDocumentPublished;
    public event Action<int>? OfficialDocumentDeleted;
    public event Action<int>? OfficialDocumentMissing;
    public event Action<string>? DirectArticleMissing;
    public Func<string, string?, Task<string?>>? Pos80PrintRequested;
    public Func<PosPaymentResult, Task<PosManualWarningChoice>>? PosManualWarningRequested;
    public Func<PromotionEvaluationResult, Task<PointsRewardRule?>>? PromotionConfirmationRequested;
    public Func<GestionaleArticleSearchResult, GestionaleArticlePricingDetail, decimal, Task<decimal?>>? ArticleQuantitySelectionRequested;
    public Func<GestionaleArticleSearchResult, decimal, Task<NegativeAvailabilityDecision>>? NegativeAvailabilityDecisionRequested;

    public BancoViewModel(
        IGestionaleDocumentReadService documentReadService,
        IGestionaleArticleReadService articleReadService,
        IGestionaleCustomerReadService customerReadService,
        IGestionaleOperatorReadService operatorReadService,
        IGestionalePriceListReadService priceListReadService,
        IGestionalePointsReadService pointsReadService,
        IPointsRewardRuleService rewardRuleService,
        IPointsPromotionEvaluationService promotionEvaluationService,
        IPointsPromotionDocumentService promotionDocumentService,
        IPointsPromotionHistoryService promotionHistoryService,
        ILocalDocumentRepository localDocumentRepository,
        IGestionaleDocumentDeleteService documentDeleteService,
        IBancoDocumentWorkflowService bancoDocumentWorkflowService,
        IApplicationConfigurationService configurationService,
        IPosPaymentService posPaymentService,
        IPosProcessLogService processLogService,
        IBancoPosPrintService bancoPosPrintService,
        IWinEcrAutoRunService winEcrAutoRunService,
        IReorderListRepository reorderListRepository)
    {
        _documentReadService = documentReadService;
        _articleReadService = articleReadService;
        _customerReadService = customerReadService;
        _operatorReadService = operatorReadService;
        _priceListReadService = priceListReadService;
        _pointsReadService = pointsReadService;
        _rewardRuleService = rewardRuleService;
        _promotionEvaluationService = promotionEvaluationService;
        _promotionDocumentService = promotionDocumentService;
        _promotionHistoryService = promotionHistoryService;
        _localDocumentRepository = localDocumentRepository;
        _documentDeleteService = documentDeleteService;
        _bancoDocumentWorkflowService = bancoDocumentWorkflowService;
        _configurationService = configurationService;
        _posPaymentService = posPaymentService;
        _processLogService = processLogService;
        _bancoPosPrintService = bancoPosPrintService;
        _winEcrAutoRunService = winEcrAutoRunService;
        _reorderListRepository = reorderListRepository;
        _configurationService.SettingsChanged += OnSettingsChanged;
        _reorderListRepository.CurrentListChanged += OnReorderListChanged;

        Righe = [];
        RisultatiRicercaArticoli = [];
        RisultatiRicercaClienti = [];
        OperatoriDisponibili = [];
        ListiniDisponibili = [];

        CercaArticoliCommand = new RelayCommand(() => _ = CercaArticoliAsync(), () => !IsReadOnly && !string.IsNullOrWhiteSpace(SearchArticoloText));
        AggiungiArticoloCommand = new RelayCommand(() => _ = AggiungiArticoloAsync(), () => CanModifyDocument && ArticoloSelezionato is not null);
        RimuoviRigaCommand = new RelayCommand<RigaDocumentoLocaleViewModel>(RimuoviRiga, riga => CanModifyDocument && riga is not null);
        CercaClientiCommand = new RelayCommand(() => _ = CercaClientiAsync(), () => !IsReadOnly && !string.IsNullOrWhiteSpace(SearchClienteText));
        SelezionaClienteCommand = new RelayCommand(SelezionaCliente, () => CanModifyDocument && ClienteSelezionato is not null);
        NuovoDocumentoCommand = new RelayCommand(() => _ = NuovoDocumentoAsync());
        AzzeraDocumentoCommand = new RelayCommand(() => _ = AzzeraDocumentoAsync(), () => CanAzzeraContenuto);
        SalvaDocumentoCommand = new RelayCommand(() => _ = SalvaDocumentoAsync(), () => CanSaveDocumentoLocale);
        AbilitaModificaDocumentoFiscalizzatoCommand = new RelayCommand(AbilitaModificaDocumentoFiscalizzato, () => CanEnableConsultationEdit);
        CancellaSchedaCommand = new RelayCommand(() => _ = CancellaSchedaAsync(), () => CanCancellaScheda);
        AnnullaOperazionePosCommand = new RelayCommand(AnnullaOperazionePos, () => OperationPopupCanCancel);
        RiprovaOperazionePosCommand = new RelayCommand(RiprovaOperazionePos, () => OperationPopupCanRetry);
        ApplicaImportoPagamentoCommand = new RelayCommand<string>(ApplicaImportoPagamentoDiretto, tipo => CanModifyDocument && !string.IsNullOrWhiteSpace(tipo) && !_isPaymentOperationInProgress);
        ScontrinoCommand = new RelayCommand(() => _ = EmettiScontrinoAsync(), () => CanEmettiScontrino);
        _incrementaQuantitaRigaSelezionataCommand = new RelayCommand(() => ModificaQuantitaRigaSelezionata(1), () => RigaSelezionata is not null);
        _decrementaQuantitaRigaSelezionataCommand = new RelayCommand(() => ModificaQuantitaRigaSelezionata(-1), () => RigaSelezionata is not null);
        ApriStoricoAcquistiCommand = new RelayCommand(ApriStoricoAcquisti, () => CanOpenCustomerHistory);
        ApriStoricoAcquistiArticoloCommand = new RelayCommand(ApriStoricoAcquistiArticoloDaPulsante);
        MostraListaDocumentiCommand = new RelayCommand(() => ShowDocumentListRequested?.Invoke());
        ApriImpostazioniBancoCommand = new RelayCommand(() => OpenSettingsRequested?.Invoke());
        ApriRegistratoreCassaCommand = new RelayCommand(() => OpenCashRegisterRequested?.Invoke());
        StampaPos80Command = new RelayCommand(() => _ = StampaPos80Async());

        _layoutInitializationTask = InitializeLayoutAsync();
        _ = LoadOperatorsAsync();
        _ = LoadPromoCampaignAsync();
        _ = NuovoDocumentoAsync();
    }

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        if (!e.GestionaleDatabaseChanged)
        {
            return;
        }

        _ = HandleGestionaleConfigurationChangedAsync(e.Settings);
    }

    public async Task HandleGestionaleConfigurationChangedAsync(AppSettings settings)
    {
        _articlePricingDetails.Clear();
        _processLogService.Info(nameof(BancoViewModel), $"Cambio configurazione DB rilevato. Host={settings.GestionaleDatabase.Host}, Database={settings.GestionaleDatabase.Database}.");
        await LoadOperatorsAsync();
        await LoadPriceListsAsync();
        await LoadPromoCampaignAsync();
        await RefreshPreviewDocumentNumberAsync();

        if (!HasPendingLocalChanges)
        {
            StatusMessage = $"Configurazione DB aggiornata in tempo reale. Host attivo: {settings.GestionaleDatabase.Host}.";
        }
    }

    public string Titolo
    {
        get
        {
            var riferimento = DocumentoRiferimentoLabel;
            return string.IsNullOrWhiteSpace(riferimento)
                ? "Banco"
                : $"Banco - {riferimento}";
        }
    }

    public string DocumentoRiferimentoLabel
    {
        get
        {
            if (_documentoGestionaleOrigine is not null)
            {
                return NormalizeDocumentoLabel(_documentoGestionaleOrigine.DocumentoLabel) ?? "Documento";
            }

            var legacyLabel = FormatDocumentoLocaleLabel(DocumentoLocaleCorrente);
            if (!string.IsNullOrWhiteSpace(legacyLabel))
            {
                return legacyLabel;
            }

            if (_previewNumeroDocumentoGestionale.HasValue && _previewAnnoDocumentoGestionale.HasValue)
            {
                return $"{_previewNumeroDocumentoGestionale}/{_previewAnnoDocumentoGestionale} (anteprima)";
            }

            return string.Empty;
        }
    }

    public string StatoDocumento
    {
        get => _statoDocumento;
        private set => SetProperty(ref _statoDocumento, value);
    }

    public bool IsScontrinato
    {
        get => _isScontrinato;
        private set
        {
            if (SetProperty(ref _isScontrinato, value))
            {
                NotifyPropertyChanged(nameof(IsReadOnly));
                NotifyPropertyChanged(nameof(CanModifyDocument));
                NotifyPropertyChanged(nameof(EmptyDocumentStateTitle));
                NotifyPropertyChanged(nameof(EmptyDocumentStateMessage));
                RaiseCommandStateChanged();
            }
        }
    }

    public BancoDocumentoAccessMode DocumentoAccessMode => _documentAccessResolution.Mode;

    public bool IsReadOnly => HasDocumentoAperto && _documentAccessResolution.IsReadOnly && !_consultationEditOverrideEnabled;

    public bool HasDocumentoAperto => DocumentoLocaleCorrente is not null;

    public bool CanModifyDocument => HasDocumentoAperto && !IsReadOnly;

    public bool IsOfficialRecoverableDocument => HasDocumentoAperto && _documentAccessResolution.IsRecoverableOfficialDocument;

    public bool IsOfficialConsultationDocument => HasDocumentoAperto && DocumentoAccessMode == BancoDocumentoAccessMode.UfficialeConsultazione;

    public bool IsConsultationEditOverrideEnabled => HasDocumentoAperto && IsOfficialConsultationDocument && _consultationEditOverrideEnabled;

    public bool CanEnableConsultationEdit => HasDocumentoAperto &&
                                             IsOfficialConsultationDocument &&
                                             !_consultationEditOverrideEnabled &&
                                             !_isPaymentOperationInProgress;

    public bool ShowDocumentoAccessBanner => HasDocumentoAperto && _documentAccessResolution.ShowBanner;

    public string DocumentoAccessBannerText => ShowDocumentoAccessBanner ? GetDocumentoAccessBannerText() : string.Empty;

    public string DocumentoAccessBannerInlineText => ShowDocumentoAccessBanner ? GetDocumentoAccessBannerText() : string.Empty;

    public bool HasHeaderNotification => !string.IsNullOrWhiteSpace(HeaderNotificationText);

    public string HeaderNotificationText
    {
        get
        {
            var parti = new List<string>();

            if (ShowDocumentoAccessBanner && !string.IsNullOrWhiteSpace(DocumentoAccessBannerInlineText))
            {
                parti.Add(DocumentoAccessBannerInlineText.Trim());
            }

            if (!string.IsNullOrWhiteSpace(StatusMessage))
            {
                var messaggio = StatusMessage.Trim();
                if (!parti.Contains(messaggio, StringComparer.OrdinalIgnoreCase))
                {
                    parti.Add(messaggio);
                }
            }

            return string.Join(" | ", parti);
        }
    }

    public DocumentoLocale? DocumentoLocaleCorrente
    {
        get => _documentoLocale;
        private set
        {
        if (SetProperty(ref _documentoLocale, value))
        {
            if (_consultationEditOverrideEnabled)
            {
                _consultationEditOverrideEnabled = false;
            }

            NotifyPropertyChanged(nameof(HasDocumentoAperto));
            NotifyPropertyChanged(nameof(DocumentoAccessMode));
            NotifyPropertyChanged(nameof(CanModifyDocument));
            NotifyPropertyChanged(nameof(CanSelectListino));
            NotifyPropertyChanged(nameof(IsOfficialRecoverableDocument));
            NotifyPropertyChanged(nameof(IsOfficialConsultationDocument));
            NotifyPropertyChanged(nameof(IsConsultationEditOverrideEnabled));
            NotifyPropertyChanged(nameof(CanEnableConsultationEdit));
            NotifyPropertyChanged(nameof(ShowDocumentoAccessBanner));
            NotifyPropertyChanged(nameof(DocumentoAccessBannerText));
            NotifyPropertyChanged(nameof(DocumentoAccessBannerInlineText));
            NotifyPropertyChanged(nameof(HeaderNotificationText));
            NotifyPropertyChanged(nameof(HasHeaderNotification));
            NotifyPropertyChanged(nameof(DocumentoRiferimentoLabel));
            NotifyPropertyChanged(nameof(OperatoreCorrente));
            NotifyPropertyChanged(nameof(ListinoCorrente));
            NotifyPropertyChanged(nameof(ClienteCorrente));
            NotifyPropertyChanged(nameof(ClienteCorrenteDisplay));
            NotifyPropertyChanged(nameof(PuntiClienteCorrente));
            NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
            NotifyPropertyChanged(nameof(PuntiMaturandiDocumento));
            NotifyPropertyChanged(nameof(HasPuntiMaturandiDocumento));
            NotifyPropertyChanged(nameof(HasSospesoDocumento));
            NotifyPropertyChanged(nameof(SospesoDocumentoLabel));
            NotifyPropertyChanged(nameof(CustomerInfoLineText));
            NotifyPropertyChanged(nameof(TotaleDocumento));
            NotifyPropertyChanged(nameof(TotalePagato));
            NotifyPropertyChanged(nameof(TotaleSconto));
            NotifyPropertyChanged(nameof(TotaleSospeso));
                NotifyPropertyChanged(nameof(Residuo));
                NotifyPropertyChanged(nameof(Resto));
            NotifyPropertyChanged(nameof(TotaleDaIncassare));
            NotifyPropertyChanged(nameof(Titolo));
            NotifyPropertyChanged(nameof(CanEmettiScontrino));
            NotifyPropertyChanged(nameof(CanEmettiCortesia));
            NotifyPropertyChanged(nameof(CanCancellaScheda));
            CancellaSchedaCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanSaveDocumentoLocale));
            NotifyPropertyChanged(nameof(CanAzzeraContenuto));
            NotifyPropertyChanged(nameof(CanCancellaScheda));
            NotifyPropertyChanged(nameof(HasRiferimentiUfficialiDocumentoCorrente));
            NotifyPropertyChanged(nameof(RichiedeConfermaRistampa));
            NotifyPropertyChanged(nameof(EmptyDocumentStateTitle));
            NotifyPropertyChanged(nameof(EmptyDocumentStateMessage));
            HasPendingLocalChanges = false;
            RaiseCommandStateChanged();
        }
    }
    }

    public string OperatoreCorrente => DocumentoLocaleCorrente?.Operatore ?? "Admin Locale";

    public string ListinoCorrente => ListinoSelezionato?.DisplayLabel ?? "Web";

    public string ClienteCorrente => NormalizeClienteLabel(DocumentoLocaleCorrente?.Cliente) ?? "Cliente generico";

    public string ClienteCorrenteDisplay => ClienteCorrente;

    public string CartaFedeltaClienteCorrente => ClienteSelezionato?.CodiceCartaFedelta ?? string.Empty;

    public string CartaFedeltaClienteCorrenteLabel => HasClienteRaccoltaPunti && !string.IsNullOrWhiteSpace(CartaFedeltaClienteCorrente)
        ? $"Carta fedeltà {CartaFedeltaClienteCorrente}"
        : string.Empty;

    public string PuntiClienteCorrente => ClienteSelezionato?.PuntiLabel ?? string.Empty;

    public bool HasClienteRaccoltaPunti => IsClienteConfermato && ClienteSelezionato?.HaRaccoltaPunti == true;

    public bool HasPuntiClienteCorrente => HasClienteRaccoltaPunti;

    public string PuntiMaturandiDocumento => HasClienteRaccoltaPunti
        ? PromoEligibility.Summary.CurrentDocumentPoints.ToString("N0")
        : string.Empty;

    public bool HasPuntiMaturandiDocumento => HasClienteRaccoltaPunti &&
                                              CanModifyDocument &&
                                              HasPendingLocalChanges &&
                                              PromoEligibility.Summary.CurrentDocumentPoints > 0;

    public bool ShowPromoApplicabilityCard => HasClienteRaccoltaPunti;

    public bool HasDocumentRows => Righe.Count > 0;

    public string EmptyDocumentStateTitle => CanModifyDocument
        ? "Documento pronto per la vendita"
        : "Documento in consultazione";

    public string EmptyDocumentStateMessage => CanModifyDocument
        ? "Cerca un articolo, conferma un cliente oppure premi INS per inserire una riga manuale."
        : "Il documento e` aperto in sola lettura e non contiene righe da mostrare.";

    public string PromoApplicabilityTitle => ShowPromoApplicabilityCard ? PromoEligibility.Title : string.Empty;

    public string PromoApplicabilityMessage => ShowPromoApplicabilityCard ? PromoEligibility.Message : string.Empty;

    public string CustomerInfoLineText
    {
        get
        {
            var parti = new List<string>();

            if (HasClienteRaccoltaPunti && !string.IsNullOrWhiteSpace(PuntiClienteCorrente))
            {
                parti.Add($"Punti {PuntiClienteCorrente}");
            }

            if (HasPuntiMaturandiDocumento && !string.IsNullOrWhiteSpace(PuntiMaturandiDocumento))
            {
                parti.Add($"Matura {PuntiMaturandiDocumento}");
            }

            if (ShowPromoApplicabilityCard && !string.IsNullOrWhiteSpace(PromoApplicabilityTitle))
            {
                parti.Add($"Promo {PromoApplicabilityTitle}");
            }

            if (HasSospesoDocumento && !string.IsNullOrWhiteSpace(SospesoDocumentoLabel))
            {
                parti.Add(SospesoDocumentoLabel);
            }

            if (parti.Count == 0)
            {
                return IsClienteConfermato
                    ? "Nessuna segnalazione attiva per il cliente corrente."
                    : "Conferma il cliente per punti, promo e storico.";
            }

            return string.Join(" | ", parti);
        }
    }

    public bool CanOpenCustomerHistory => IsClienteConfermato && ClienteSelezionato is not null && !ClienteSelezionato.IsClienteGenerico;

    public bool CanApplyPromo => PromoEligibility.EventType is PromotionEventType.Eligible or PromotionEventType.Applied;

    public bool CustomerIsGeneric => PromoEligibility.IsGenericCustomer;

    public bool HasPendingLocalChanges
    {
        get => _hasPendingLocalChanges;
        private set
        {
            if (SetProperty(ref _hasPendingLocalChanges, value))
            {
                NotifyPropertyChanged(nameof(CanSaveDocumentoLocale));
                NotifyPropertyChanged(nameof(HasPuntiMaturandiDocumento));
                NotifyPropertyChanged(nameof(CustomerInfoLineText));
                SalvaDocumentoCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public decimal TotaleDocumento => DocumentoLocaleCorrente?.TotaleDocumento ?? 0;

    public decimal TotalePagato => ContantiPagamento + CartaPagamento + BuoniPagamento;

    public decimal TotaleSconto => ScontoPagamento;

    public decimal TotaleSospeso => SospesoPagamento;

    public decimal Residuo => Math.Max(0, TotaleDaIncassare - TotalePagato - TotaleSospeso);

    public decimal Resto => Math.Max(0, TotalePagato - TotaleDaIncassare);

    public decimal TotaleDaIncassare => Math.Max(0, TotaleDocumento - TotaleSconto);

    public bool HasSospesoDocumento => TotaleSospeso > 0;

    public string SospesoDocumentoLabel => HasSospesoDocumento ? $"Sospeso {TotaleSospeso:N2} €" : string.Empty;

    public bool CanEmettiScontrino => CanModifyDocument &&
                                      !IsOfficialConsultationDocument &&
                                      HasDocumentoAperto &&
                                      DocumentoLocaleCorrente?.Righe.Count > 0 &&
                                      !_isPaymentOperationInProgress;

    public bool CanEmettiCortesia => CanModifyDocument
                                     && !IsOfficialConsultationDocument
                                     && DocumentoLocaleCorrente?.Righe.Count > 0
                                     && !_isPaymentOperationInProgress;

    public bool HasRiferimentiUfficialiDocumentoCorrente => HasAnyOfficialLegacyReference(DocumentoLocaleCorrente);

    public bool CanSaveDocumentoLocale => CanModifyDocument &&
                                          DocumentoLocaleCorrente is not null &&
                                          HasPendingLocalChanges &&
                                          !_isPaymentOperationInProgress;

    public bool IsPubblicazioneBancoNeutraCorrente => IsOfficialBancoPublication(DocumentoLocaleCorrente);

    public bool CanAzzeraContenuto => CanModifyDocument &&
                                      DocumentoLocaleCorrente is not null &&
                                      !IsOfficialConsultationDocument;

    public bool RichiedeConfermaContantiPredefiniti =>
        CanModifyDocument &&
        DocumentoLocaleCorrente is not null &&
        DocumentoLocaleCorrente.Righe.Count > 0 &&
        TotaleDaIncassare > 0 &&
        ScontoPagamento <= 0 &&
        ContantiPagamento <= 0 &&
        CartaPagamento <= 0 &&
        SospesoPagamento <= 0 &&
        BuoniPagamento <= 0;

    public bool CanCancellaScheda => HasDocumentoAperto &&
                                     !_isPaymentOperationInProgress &&
                                     DocumentoAccessMode != BancoDocumentoAccessMode.UfficialeConsultazione;

    public RelayCommand AbilitaModificaDocumentoFiscalizzatoCommand { get; }

    public bool RichiedeConfermaRistampa =>
        DocumentoLocaleCorrente?.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata;

    private string GetDocumentoAccessBannerText()
    {
        if (IsConsultationEditOverrideEnabled)
        {
            return "Documento fiscalizzato: modifica manuale abilitata. Salva con F4/F10 senza nuova stampa.";
        }

        return _documentAccessResolution.BannerScheda;
    }

    private void AbilitaModificaDocumentoFiscalizzato()
    {
        if (!CanEnableConsultationEdit)
        {
            return;
        }

        _consultationEditOverrideEnabled = true;
        StatoDocumento = "Documento fiscalizzato - modifica manuale";
        StatusMessage = "Modifica manuale abilitata sulla scheda fiscalizzata. F4/F10 salva sul legacy senza ristampa.";
        NotifyConsultationEditOverrideChanged();
    }

    private void NotifyConsultationEditOverrideChanged()
    {
        NotifyPropertyChanged(nameof(IsReadOnly));
        NotifyPropertyChanged(nameof(CanModifyDocument));
        NotifyPropertyChanged(nameof(IsConsultationEditOverrideEnabled));
        NotifyPropertyChanged(nameof(CanEnableConsultationEdit));
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanSaveDocumentoLocale));
        NotifyPropertyChanged(nameof(CanAzzeraContenuto));
        NotifyPropertyChanged(nameof(ShowDocumentoAccessBanner));
        NotifyPropertyChanged(nameof(DocumentoAccessBannerText));
        NotifyPropertyChanged(nameof(DocumentoAccessBannerInlineText));
        NotifyPropertyChanged(nameof(HeaderNotificationText));
        NotifyPropertyChanged(nameof(HasHeaderNotification));
        NotifyPropertyChanged(nameof(EmptyDocumentStateTitle));
        NotifyPropertyChanged(nameof(EmptyDocumentStateMessage));
        RaiseCommandStateChanged();
    }

    public string ScontoPagamentoText
    {
        get => _scontoPagamentoText;
        set
        {
            if (SetProperty(ref _scontoPagamentoText, value))
            {
                OnPaymentInputChanged();
            }
        }
    }

    public string ContantiPagamentoText
    {
        get => _contantiPagamentoText;
        set
        {
            if (SetProperty(ref _contantiPagamentoText, value))
            {
                OnPaymentInputChanged();
            }
        }
    }

    public string CartaPagamentoText
    {
        get => _bancomatPagamentoText;
        set
        {
            if (SetProperty(ref _bancomatPagamentoText, value))
            {
                OnPaymentInputChanged();
            }
        }
    }

    public string SospesoPagamentoText
    {
        get => _sospesoPagamentoText;
        set
        {
            if (SetProperty(ref _sospesoPagamentoText, value))
            {
                OnPaymentInputChanged();
            }
        }
    }

    public string BuoniPagamentoText
    {
        get => _buoniPagamentoText;
        set
        {
            if (SetProperty(ref _buoniPagamentoText, value))
            {
                OnPaymentInputChanged();
            }
        }
    }

    public string SearchArticoloText
    {
        get => _searchArticoloText;
        set
        {
            if (SetProperty(ref _searchArticoloText, value))
            {
                CercaArticoliCommand.RaiseCanExecuteChanged();
                if (string.IsNullOrWhiteSpace(value))
                {
                    IsArticlePopupOpen = false;
                }

                ScheduleArticleSearch();
            }
        }
    }

    public string SearchClienteText
    {
        get => _searchClienteText;
        set
        {
            if (SetProperty(ref _searchClienteText, value))
            {
                CercaClientiCommand.RaiseCanExecuteChanged();
                if (string.IsNullOrWhiteSpace(value))
                {
                    IsCustomerPopupOpen = false;
                }

                if (!_isUpdatingSearchClienteText)
                {
                    IsClienteConfermato = false;
                    ScheduleCustomerSearch();
                }
            }
        }
    }

    public GestionaleArticleSearchResult? ArticoloSelezionato
    {
        get => _articoloSelezionato;
        set
        {
            if (SetProperty(ref _articoloSelezionato, value))
            {
                NotifyPropertyChanged(nameof(QuantitaArticoloLabel));
                NotifyPropertyChanged(nameof(QuantitaArticoloValue));
                NotifyPropertyChanged(nameof(PrezzoArticoloSelezionato));
                NotifyPropertyChanged(nameof(VarianteArticoloSelezionata));
                AggiungiArticoloCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GestionaleCustomerSummary? ClienteSelezionato
    {
        get => _clienteSelezionato;
        set
        {
            if (SetProperty(ref _clienteSelezionato, value))
            {
                IsClienteConfermato = false;
                NotifyPropertyChanged(nameof(PuntiClienteCorrente));
                NotifyPropertyChanged(nameof(CartaFedeltaClienteCorrente));
                NotifyPropertyChanged(nameof(CartaFedeltaClienteCorrenteLabel));
                NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
                NotifyPropertyChanged(nameof(HasClienteRaccoltaPunti));
                NotifyPropertyChanged(nameof(ShowPromoApplicabilityCard));
                NotifyPropertyChanged(nameof(CustomerInfoLineText));
                NotifyPropertyChanged(nameof(HasClienteSelezionato));
                SelezionaClienteCommand.RaiseCanExecuteChanged();
                ApriStoricoAcquistiCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsClienteConfermato
    {
        get => _isClienteConfermato;
        private set
        {
            if (SetProperty(ref _isClienteConfermato, value))
            {
                NotifyPropertyChanged(nameof(HasClienteRaccoltaPunti));
                NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
                NotifyPropertyChanged(nameof(PuntiMaturandiDocumento));
                NotifyPropertyChanged(nameof(HasPuntiMaturandiDocumento));
                NotifyPropertyChanged(nameof(CartaFedeltaClienteCorrente));
                NotifyPropertyChanged(nameof(CartaFedeltaClienteCorrenteLabel));
                NotifyPropertyChanged(nameof(ShowPromoApplicabilityCard));
                NotifyPropertyChanged(nameof(PromoApplicabilityTitle));
                NotifyPropertyChanged(nameof(PromoApplicabilityMessage));
                NotifyPropertyChanged(nameof(CustomerInfoLineText));
                RaiseCommandStateChanged();
            }
        }
    }

    public GestionaleOperatorSummary? OperatoreSelezionato
    {
        get => _operatoreSelezionato;
        set
        {
            if (SetProperty(ref _operatoreSelezionato, value))
            {
                if (DocumentoLocaleCorrente is not null && value is not null)
                {
                    DocumentoLocaleCorrente.Operatore = value.Nome;
                    NotifyPropertyChanged(nameof(OperatoreCorrente));
                }
            }
        }
    }

    public GestionalePriceListSummary? ListinoSelezionato
    {
        get => _listinoSelezionato;
        set
        {
            if (SetProperty(ref _listinoSelezionato, value))
            {
                NotifyPropertyChanged(nameof(ListinoCorrente));
                NotifyPropertyChanged(nameof(CanSelectListino));

                if (!_isApplyingSelectedPriceList)
                {
                    _ = HandleSelectedPriceListChangedAsync(value, markDocumentDirty: true, updateStatusMessage: true);
                }
            }
        }
    }

    public RigaDocumentoLocaleViewModel? RigaSelezionata
    {
        get => _rigaSelezionata;
        set
        {
            if (SetProperty(ref _rigaSelezionata, value))
            {
                NotifySelectedRowStateChanged();
                RimuoviRigaCommand.RaiseCanExecuteChanged();
                _incrementaQuantitaRigaSelezionataCommand.RaiseCanExecuteChanged();
                _decrementaQuantitaRigaSelezionataCommand.RaiseCanExecuteChanged();
                _ = AggiornaUltimoAcquistoArticoloSelezionatoAsync();
            }
        }
    }

    public bool HasRigaSelezionata => RigaSelezionata is not null;

    public bool HasQuickArticlePurchaseInfo => UltimoAcquistoArticolo is not null;

    public GestionaleArticlePurchaseQuickInfo? UltimoAcquistoArticolo
    {
        get => _ultimoAcquistoArticolo;
        private set
        {
            if (SetProperty(ref _ultimoAcquistoArticolo, value))
            {
                NotifyPropertyChanged(nameof(HasQuickArticlePurchaseInfo));
                NotifyPropertyChanged(nameof(UltimoAcquistoDataLabel));
                NotifyPropertyChanged(nameof(UltimoAcquistoFornitoreLabel));
                NotifyPropertyChanged(nameof(UltimoAcquistoFtLabel));
                NotifyPropertyChanged(nameof(UltimoAcquistoPrezzoLabel));
                NotifyPropertyChanged(nameof(UltimoAcquistoEmptyLabel));
            }
        }
    }

    public string UltimoAcquistoDataLabel => UltimoAcquistoArticolo is null
        ? string.Empty
        : UltimoAcquistoArticolo.DataUltimoAcquisto.ToString("dd/MM/yyyy");

    public string UltimoAcquistoFornitoreLabel => UltimoAcquistoArticolo?.FornitoreNominativo ?? string.Empty;

    public string UltimoAcquistoFtLabel => UltimoAcquistoArticolo?.RiferimentoFattura ?? string.Empty;

    public string UltimoAcquistoPrezzoLabel => UltimoAcquistoArticolo is null
        ? string.Empty
        : $"{UltimoAcquistoArticolo.PrezzoUnitario:N2} €";

    public string UltimoAcquistoEmptyLabel => HasQuickArticlePurchaseInfo
        ? string.Empty
        : "Nessun dato acquisto per l'articolo selezionato.";

    public PromotionEvaluationResult PromoEligibility
    {
        get => _promoEligibility;
        private set
        {
            if (SetProperty(ref _promoEligibility, value))
            {
                NotifyPropertyChanged(nameof(PromoApplicabilityTitle));
                NotifyPropertyChanged(nameof(PromoApplicabilityMessage));
                NotifyPropertyChanged(nameof(CanApplyPromo));
                NotifyPropertyChanged(nameof(CustomerIsGeneric));
                NotifyPropertyChanged(nameof(PuntiMaturandiDocumento));
                NotifyPropertyChanged(nameof(HasPuntiMaturandiDocumento));
                NotifyPropertyChanged(nameof(CustomerInfoLineText));
            }
        }
    }

    public decimal QuantitaRigaSelezionata
    {
        get => RigaSelezionata?.Quantita ?? 0;
        set
        {
            if (RigaSelezionata is null)
            {
                return;
            }

            RigaSelezionata.Quantita = value;
            NotifySelectedRowStateChanged();
        }
    }

    public decimal DisponibilitaResiduaRigaSelezionata => RigaSelezionata?.DisponibilitaRiferimento ?? 0;

    public bool IsDisponibilitaResiduaCritica => DisponibilitaResiduaRigaSelezionata <= 0;

    public string QuantitaArticoloLabel => ArticoloSelezionato?.TipoDisponibilitaLabel ?? "Disponibilita`";

    public decimal QuantitaArticoloValue => ArticoloSelezionato?.Giacenza ?? 0;

    public decimal PrezzoArticoloSelezionato => ArticoloSelezionato?.PrezzoVendita ?? 0;

    public string VarianteArticoloSelezionata => ArticoloSelezionato?.VarianteLabel ?? string.Empty;

    public bool IsArticlePopupOpen
    {
        get => _isArticlePopupOpen;
        set => SetProperty(ref _isArticlePopupOpen, value);
    }

    public bool IsCustomerPopupOpen
    {
        get => _isCustomerPopupOpen;
        set => SetProperty(ref _isCustomerPopupOpen, value);
    }

    public double RigaColumnWidth
    {
        get => _rigaColumnWidth;
        private set => SetProperty(ref _rigaColumnWidth, value);
    }

    public double CodiceColumnWidth
    {
        get => _codiceColumnWidth;
        private set => SetProperty(ref _codiceColumnWidth, value);
    }

    public double DescrizioneColumnWidth
    {
        get => _descrizioneColumnWidth;
        private set => SetProperty(ref _descrizioneColumnWidth, value);
    }

    public double QuantitaColumnWidth
    {
        get => _quantitaColumnWidth;
        private set => SetProperty(ref _quantitaColumnWidth, value);
    }

    public double PrezzoColumnWidth
    {
        get => _prezzoColumnWidth;
        private set => SetProperty(ref _prezzoColumnWidth, value);
    }

    public double IvaColumnWidth
    {
        get => _ivaColumnWidth;
        private set => SetProperty(ref _ivaColumnWidth, value);
    }

    public double TipoColumnWidth
    {
        get => _tipoColumnWidth;
        private set => SetProperty(ref _tipoColumnWidth, value);
    }

    public double TipoRigaColumnWidth
    {
        get => _tipoRigaColumnWidth;
        private set => SetProperty(ref _tipoRigaColumnWidth, value);
    }

    public double ScontoColumnWidth
    {
        get => _scontoColumnWidth;
        private set => SetProperty(ref _scontoColumnWidth, value);
    }

    public double ImportoColumnWidth
    {
        get => _importoColumnWidth;
        private set => SetProperty(ref _importoColumnWidth, value);
    }

    public double AzioniColumnWidth
    {
        get => _azioniColumnWidth;
        private set => SetProperty(ref _azioniColumnWidth, value);
    }

    public bool ShowRigaColumn => GetColumnVisibility("Riga");

    public bool ShowCodiceColumn => GetColumnVisibility("Codice");

    public bool ShowDescrizioneColumn => GetColumnVisibility("Descrizione");

    public bool ShowQuantitaColumn => GetColumnVisibility("Quantita");

    public bool ShowDisponibilitaColumn => GetColumnVisibility("Disponibilita");

    public bool ShowPrezzoColumn => GetColumnVisibility("Prezzo");

    public bool ShowScontoColumn => GetColumnVisibility("Sconto");

    public bool ShowImportoColumn => GetColumnVisibility("Importo");

    public bool ShowIvaColumn => GetColumnVisibility("Iva");

    public bool ShowUnitaMisuraColumn => GetColumnVisibility("UnitaMisura");

    public bool ShowTipoRigaColumn => GetColumnVisibility("TipoRiga");

    public bool ShowAzioniColumn => GetColumnVisibility("Azioni");

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                NotifyPropertyChanged(nameof(HeaderNotificationText));
                NotifyPropertyChanged(nameof(HasHeaderNotification));
            }
        }
    }

    public bool IsOperationPopupVisible
    {
        get => _isOperationPopupVisible;
        private set => SetProperty(ref _isOperationPopupVisible, value);
    }

    public string OperationPopupTitle
    {
        get => _operationPopupTitle;
        private set => SetProperty(ref _operationPopupTitle, value);
    }

    public string OperationPopupMessage
    {
        get => _operationPopupMessage;
        private set => SetProperty(ref _operationPopupMessage, value);
    }

    public string OperationPopupFooterText
    {
        get => _operationPopupFooterText;
        private set => SetProperty(ref _operationPopupFooterText, value);
    }

    public bool OperationPopupIsBusy
    {
        get => _operationPopupIsBusy;
        private set => SetProperty(ref _operationPopupIsBusy, value);
    }

    public bool OperationPopupIsSuccess
    {
        get => _operationPopupIsSuccess;
        private set => SetProperty(ref _operationPopupIsSuccess, value);
    }

    public bool OperationPopupCanCancel
    {
        get => _operationPopupCanCancel;
        private set => SetProperty(ref _operationPopupCanCancel, value);
    }

    public bool OperationPopupCanRetry
    {
        get => _operationPopupCanRetry;
        private set => SetProperty(ref _operationPopupCanRetry, value);
    }

    public ObservableCollection<RigaDocumentoLocaleViewModel> Righe { get; }

    public ObservableCollection<GestionaleArticleSearchResult> RisultatiRicercaArticoli { get; }

    public ObservableCollection<GestionaleCustomerSummary> RisultatiRicercaClienti { get; }

    public ObservableCollection<GestionaleOperatorSummary> OperatoriDisponibili { get; }

    public ObservableCollection<GestionalePriceListSummary> ListiniDisponibili { get; }

    public bool CanSelectListino => CanModifyDocument && ListiniDisponibili.Count > 0;

    public RelayCommand CercaArticoliCommand { get; }

    public RelayCommand AggiungiArticoloCommand { get; }

    public RelayCommand<RigaDocumentoLocaleViewModel> RimuoviRigaCommand { get; }

    public RelayCommand CercaClientiCommand { get; }

    public RelayCommand SelezionaClienteCommand { get; }

    public RelayCommand NuovoDocumentoCommand { get; }

    public RelayCommand AzzeraDocumentoCommand { get; }

    public RelayCommand SalvaDocumentoCommand { get; }

    public RelayCommand CancellaSchedaCommand { get; }

    public RelayCommand AnnullaOperazionePosCommand { get; }

    public RelayCommand RiprovaOperazionePosCommand { get; }

    public RelayCommand<string> ApplicaImportoPagamentoCommand { get; }

    public RelayCommand ScontrinoCommand { get; }

    public RelayCommand IncrementaQuantitaRigaSelezionataCommand => _incrementaQuantitaRigaSelezionataCommand;

    public RelayCommand DecrementaQuantitaRigaSelezionataCommand => _decrementaQuantitaRigaSelezionataCommand;

    public RelayCommand ApriStoricoAcquistiCommand { get; }

    public RelayCommand ApriStoricoAcquistiArticoloCommand { get; }

    public RelayCommand MostraListaDocumentiCommand { get; }

    public RelayCommand ApriImpostazioniBancoCommand { get; }

    public RelayCommand ApriRegistratoreCassaCommand { get; }

    public RelayCommand StampaPos80Command { get; }

    public async Task RegistraOperazioneCassaAsync(CashRegisterOptionSelection selection)
    {
        var descrizioneOperazione = selection.Action switch
        {
            CashRegisterOptionAction.DailyJournal => $"Stampa giornale giornaliero ({selection.JournalModeLabel}).",
            CashRegisterOptionAction.CloseCashAndTransmit => $"Stampa e chiusura cassa con invio AdE ({selection.JournalModeLabel}).",
            CashRegisterOptionAction.ReceiptReprint => $"Ristampa scontrino {selection.ReceiptReferenceLabel}.",
            CashRegisterOptionAction.ReceiptCancellation => $"Annullo scontrino {selection.ReceiptReferenceLabel}.",
            _ => "Operazione cassa non riconosciuta."
        };

        _isPaymentOperationInProgress = true;
        RaiseCommandStateChanged();
        ShowOperationPopup(
            "Opzioni cassa",
            selection.Action switch
            {
                CashRegisterOptionAction.DailyJournal => $"Preparazione stampa giornale {selection.JournalModeLabel} in corso...",
                CashRegisterOptionAction.CloseCashAndTransmit => $"Preparazione chiusura cassa {selection.JournalModeLabel} in corso...",
                CashRegisterOptionAction.ReceiptReprint => $"Preparazione ristampa scontrino {selection.ReceiptReferenceLabel} in corso...",
                CashRegisterOptionAction.ReceiptCancellation => $"Preparazione annullo scontrino {selection.ReceiptReferenceLabel} in corso...",
                _ => "Preparazione operazione cassa in corso..."
            },
            true,
            false);

        try
        {
            _processLogService.Info(
                nameof(BancoViewModel),
                $"Opzione cassa selezionata dal Banco. Azione={selection.Action}, Giornale={selection.JournalMode}, Riferimento={selection.ReceiptReferenceLabel}, Registratore=Ditron Elsi Retail R1.");
            var result = await _winEcrAutoRunService.ExecuteCashRegisterOperationAsync(selection, DocumentoLocaleCorrente);
            StatoDocumento = result.IsSuccess
                ? "Operazione cassa completata"
                : "Operazione cassa non completata";
            StatusMessage = $"{descrizioneOperazione} {result.Message}";
            if (!string.IsNullOrWhiteSpace(result.ErrorDetails))
            {
                StatusMessage = $"{StatusMessage} Dettaglio driver: {result.ErrorDetails}";
            }

            await ShowFinalOperationPopupAsync(
                result.IsSuccess ? "Operazione completata" : "Operazione non completata",
                StatusMessage,
                result.IsSuccess);
        }
        catch (Exception ex)
        {
            StatoDocumento = "Errore opzioni cassa";
            StatusMessage = $"Errore durante l'operazione cassa: {ex.Message}";
            _processLogService.Error(
                nameof(BancoViewModel),
                $"Errore durante l'operazione cassa {selection.Action} ({selection.JournalMode}, {selection.ReceiptReferenceLabel}) sul registratore Ditron Elsi Retail R1.",
                ex);
            await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
        }
        finally
        {
            _isPaymentOperationInProgress = false;
            RaiseCommandStateChanged();
        }
    }

    public async Task<(string DeviceSerialNumber, string ReceiptPrefix)> LoadCashRegisterDialogDefaultsAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var deviceSerialNumber = string.IsNullOrWhiteSpace(settings.WinEcrIntegration.DeviceSerialNumber)
            ? "ND"
            : settings.WinEcrIntegration.DeviceSerialNumber.Trim();
        return (deviceSerialNumber, "1959");
    }

    public bool CanPrintPos80 => DocumentoLocaleCorrente is not null
                                 && DocumentoLocaleCorrente.Righe.Count > 0
                                 && !_isPaymentOperationInProgress;

    public async Task<bool> AddSelectedRowToReorderListAsync()
    {
        var riga = RigaSelezionata;
        if (!CanModifyDocument || riga?.Model.ArticoloOid is not int articoloOid || articoloOid <= 0)
        {
            return false;
        }

        var supplierSuggestion = await ResolveSuggestedSupplierAsync(articoloOid);
        var item = new ReorderListItem
        {
            ArticoloOid = articoloOid,
            CodiceArticolo = riga.CodiceArticolo,
            Descrizione = riga.Descrizione,
            Quantita = riga.Quantita <= 0 ? 1 : riga.Quantita,
            UnitaMisura = riga.UnitaMisura,
            FornitoreSuggeritoOid = supplierSuggestion?.Oid,
            FornitoreSuggeritoNome = supplierSuggestion?.Nome ?? string.Empty,
            FornitoreSelezionatoOid = supplierSuggestion?.Oid,
            FornitoreSelezionatoNome = supplierSuggestion?.Nome ?? string.Empty,
            PrezzoSuggerito = supplierSuggestion?.PrezzoRiferimento,
            IvaOid = riga.IvaOid,
            Motivo = riga.DisponibilitaRiferimento <= 0 ? ReorderReason.GiacenzaZero : ReorderReason.Manuale,
            Stato = ReorderItemStatus.DaOrdinare,
            Operatore = OperatoreCorrente
        };

        await _reorderListRepository.AddOrIncrementItemAsync(item);
        await RefreshReorderMarkersAsync();

        StatoDocumento = "Riga segnata per riordino";
        StatusMessage = $"Articolo {riga.CodiceArticolo} aggiunto alla lista riordino.";
        await ShowReorderOperationPopupAsync(StatusMessage);
        return true;
    }

    public async Task<bool> RemoveSelectedRowFromReorderListAsync()
    {
        var riga = RigaSelezionata;
        if (riga?.Model.ArticoloOid is not int articoloOid || articoloOid <= 0)
        {
            return false;
        }

        var snapshot = await _reorderListRepository.GetCurrentListAsync();
        var item = snapshot.Items.FirstOrDefault(entry => IsSameReorderIdentity(entry, riga));
        if (item is null)
        {
            return false;
        }

        await _reorderListRepository.RemoveItemAsync(item.Id);
        await RefreshReorderMarkersAsync();

        StatoDocumento = "Riga rimossa dal riordino";
        StatusMessage = $"Articolo {riga.CodiceArticolo} rimosso dalla lista riordino.";
        await ShowReorderOperationPopupAsync(StatusMessage);
        return true;
    }

    public bool HasClienteSelezionato => _clienteSelezionato is not null && _clienteSelezionato.Oid > 0;

    private bool HasBuoniPagamentoLocale => DocumentoLocaleCorrente?.Pagamenti.Any(pagamento =>
        string.Equals(pagamento.TipoPagamento?.Trim(), "buoni", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(pagamento.TipoPagamento?.Trim(), "buonipasto", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(pagamento.TipoPagamento?.Trim(), "ticket", StringComparison.OrdinalIgnoreCase)) == true;

    public Task ExecuteArticleSearchAsync() => CercaArticoliAsync(explicitUserRequest: true);

    public async Task ExecuteCustomerSearchAsync()
    {
        await CercaClientiAsync();

        if (!CanModifyDocument || IsReadOnly)
        {
            return;
        }

        var bestMatch = ResolveBestCustomerSelection(SearchClienteText, RisultatiRicercaClienti);
        if (bestMatch is null)
        {
            return;
        }

        ClienteSelezionato = bestMatch;
        SelezionaCliente();
    }

    public bool InserisciRigaManuale()
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null)
        {
            return false;
        }

        var nuovaRiga = new RigaDocumentoLocale
        {
            OrdineRiga = DocumentoLocaleCorrente.Righe.Count + 1,
            TipoRiga = TipoRigaDocumento.Manuale,
            ArticoloOid = null,
            CodiceArticolo = null,
            Descrizione = "Articolo manuale",
            UnitaMisura = "PZ",
            Quantita = 1,
            DisponibilitaRiferimento = 0,
            PrezzoUnitario = 0,
            ScontoPercentuale = 0,
            IvaOid = 1,
            AliquotaIva = 0,
            FlagManuale = true
        };

        DocumentoLocaleCorrente.AggiungiRiga(nuovaRiga);
        RefreshRows();
        RigaSelezionata = Righe.LastOrDefault(item => item.Id == nuovaRiga.Id);
        StatoDocumento = "Riga manuale inserita";
        StatusMessage = "Riga manuale aggiunta al documento.";
        HasPendingLocalChanges = true;
        return true;
    }

    public bool ConvertiRigaSelezionataInManuale()
    {
        if (!CanModifyDocument || RigaSelezionata is null)
        {
            return false;
        }

        var model = RigaSelezionata.Model;
        model.TipoRiga = TipoRigaDocumento.Manuale;
        model.ArticoloOid = null;
        model.CodiceArticolo = null;
        model.FlagManuale = true;

        if (string.IsNullOrWhiteSpace(model.Descrizione) || model.Descrizione == "-")
        {
            model.Descrizione = "Articolo manuale";
        }

        RigaSelezionata.NotifyMetadataChanged();
        StatoDocumento = "Riga convertita in manuale";
        StatusMessage = "Il codice articolo della riga selezionata e` stato rimosso.";
        HasPendingLocalChanges = true;
        return true;
    }

    public async Task LoadGestionaleDocumentAsync(int oid)
    {
        StatusMessage = $"Caricamento documento gestionale {oid} in corso...";

        var detail = await _documentReadService.GetDocumentDetailAsync(oid);
        if (detail is null)
        {
            StatusMessage = $"Documento gestionale {oid} non trovato.";
            OfficialDocumentMissing?.Invoke(oid);
            return;
        }

        ResetState();
        StatusMessage = $"Caricamento documento gestionale {oid} in corso...";

        _documentoGestionaleOrigine = detail;
        ClearPreviewDocumentNumber();
        NotifyPropertyChanged(nameof(DocumentoRiferimentoLabel));
        NotifyPropertyChanged(nameof(Titolo));

        var localMetadata = await _localDocumentRepository.GetByDocumentoGestionaleOidAsync(detail.Oid);
        var legacyHasScontrinatoPayments = HasScontrinatoPayments(detail.PagatoContanti, detail.PagatoCarta, detail.PagatoWeb);
        var accessResolution = BancoDocumentoAccessResolver.Resolve(
            localMetadata,
            detail.Oid,
            detail.DocumentoLabel,
            legacyHasScontrinatoPayments,
            detail.HasLegacyFiscalSignal);
        var documento = localMetadata ?? CreateLegacyBackedDocument(detail, accessResolution);

        DocumentoLocaleCorrente = documento;
        ApplyDocumentAccessResolution(accessResolution);
        IsScontrinato = legacyHasScontrinatoPayments;
        SyncOperatoreSelezionato(detail.Operatore);
        LoadPaymentInputsFromDocument();

        RefreshRows();
        await LoadClienteCorrenteAsync(detail.SoggettoOid);
        await LoadPriceListsAsync(detail.ListinoOid);

        StatoDocumento = accessResolution.StatoScheda;
        StatusMessage = accessResolution.MessaggioScheda;
    }

    public async Task LoadLocalDocumentAsync(Guid documentId)
    {
        ResetState();
        StatusMessage = "Caricamento scheda Banco in corso...";

        var documento = await _localDocumentRepository.GetByIdAsync(documentId);
        if (documento is null)
        {
            StatusMessage = "Scheda Banco non trovata.";
            return;
        }

        DocumentoLocaleCorrente = documento;
        if (HasAnyOfficialLegacyReference(documento))
        {
            ClearPreviewDocumentNumber();
        }
        else
        {
            await RefreshPreviewDocumentNumberAsync();
        }
        ApplyDocumentAccessResolution(BancoDocumentoAccessResolver.Resolve(
            documento,
            documento.DocumentoGestionaleOid,
            FormatDocumentoLocaleLabel(documento),
            HasScontrinatoPayments(
                DocumentGridRowViewModel.NormalizePagamenti(documento.Pagamenti).Contanti,
                DocumentGridRowViewModel.NormalizePagamenti(documento.Pagamenti).Carta,
                DocumentGridRowViewModel.NormalizePagamenti(documento.Pagamenti).Web),
            documento.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata));
        var pagamentiLocali = DocumentGridRowViewModel.NormalizePagamenti(documento.Pagamenti);
        IsScontrinato = HasScontrinatoPayments(pagamentiLocali.Contanti, pagamentiLocali.Carta, pagamentiLocali.Web) ||
                        documento.StatoFiscaleBanco == StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata;
        SyncOperatoreSelezionato(documento.Operatore);
        LoadPaymentInputsFromDocument();
        RefreshRows();
        if (documento.ClienteOid.HasValue)
        {
            await LoadClienteCorrenteAsync(documento.ClienteOid.Value);
        }
        else
        {
            await LoadClienteCorrenteFromLabelAsync(documento.Cliente);
        }
        await LoadPriceListsAsync(documento.ListinoOid);

        StatoDocumento = _documentAccessResolution.StatoScheda;
        StatusMessage = documento.DocumentoGestionaleOid.HasValue
            ? _documentAccessResolution.MessaggioScheda
            : "Documento Banco operativo caricato dal supporto tecnico.";
    }

    private async Task NuovoDocumentoAsync()
    {
        ResetState();

        DocumentoLocaleCorrente = DocumentoLocale.Reidrata(
            Guid.NewGuid(),
            StatoDocumentoLocale.BozzaLocale,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            OperatoreSelezionato?.Nome ?? "Admin Banco",
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
        await RefreshPreviewDocumentNumberAsync();
        ApplyDocumentAccessResolution(BancoDocumentoAccessResolver.Resolve(
            DocumentoLocaleCorrente,
            DocumentoLocaleCorrente?.DocumentoGestionaleOid,
            FormatDocumentoLocaleLabel(DocumentoLocaleCorrente),
            false,
            false));
        var nuovoDocumentoId = DocumentoLocaleCorrente!.Id;
        _processLogService.Info(nameof(BancoViewModel), $"Nuova scheda Banco creata: {nuovoDocumentoId:N}.");
        LoadPaymentInputsFromDocument();

        await PrepareDefaultCustomerLookupAsync();
        await LoadPriceListsAsync();
        StatoDocumento = "Nuova scheda Banco";
        StatusMessage = string.IsNullOrWhiteSpace(DocumentoRiferimentoLabel)
            ? "Scheda Banco pronta. Inserisci cliente e articoli."
            : $"Scheda Banco pronta. Progressivo {DocumentoRiferimentoLabel}. Inserisci cliente e articoli.";
        FocusArticleSearchRequested?.Invoke();
    }

    private async Task AzzeraDocumentoAsync()
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null)
        {
            return;
        }

        DocumentoLocaleCorrente.AzzeraContenuto();
        SearchArticoloText = string.Empty;
        RisultatiRicercaArticoli.Clear();
        ArticoloSelezionato = null;
        IsArticlePopupOpen = false;
        await PrepareDefaultCustomerLookupAsync();
        await LoadPriceListsAsync();
        RefreshRows();
        ResetPaymentInputs();
        await RefreshPreviewDocumentNumberAsync();
        NotifyPropertyChanged(nameof(ClienteCorrente));
        NotifyPropertyChanged(nameof(ClienteCorrenteDisplay));
        NotifyPropertyChanged(nameof(PuntiClienteCorrente));
        NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
        NotifyPropertyChanged(nameof(CustomerInfoLineText));
        StatoDocumento = "Documento azzerato";
        StatusMessage = "Contenuto documento azzerato. Inserisci cliente e articoli.";
        // Dopo l'azzeramento la scheda deve risultare pulita, senza prompt di uscita immediato.
        HasPendingLocalChanges = false;
        SchedulePromoRefresh(triggerPopupIfNeeded: false);
        FocusArticleSearchRequested?.Invoke();
    }

    public void FocusArticleSearch()
    {
        FocusArticleSearchRequested?.Invoke();
    }

    public void PrepareForArticleInsertionAfterAnnullaPrompt()
    {
        StatusMessage = "Scheda lasciata invariata. Inserisci o cerca un nuovo articolo.";
        FocusArticleSearchRequested?.Invoke();
    }

    private Task CercaArticoliAsync(bool explicitUserRequest = false)
    {
        var requestVersion = ++_articleSearchRequestVersion;
        return CercaArticoliAsync(requestVersion, SearchArticoloText, explicitUserRequest);
    }

    private async Task CercaArticoliAsync(int requestVersion, string searchText, bool explicitUserRequest = false)
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var results = await _articleReadService.SearchArticlesAsync(searchText, ListinoSelezionato?.Oid);
        if (!IsArticleSearchRequestCurrent(requestVersion, searchText))
        {
            return;
        }

        RisultatiRicercaArticoli.Clear();
        ArticoloSelezionato = null;
        foreach (var result in results)
        {
            RisultatiRicercaArticoli.Add(result);
        }

        IsArticlePopupOpen = results.Count > 0;
        ArticoloSelezionato = RisultatiRicercaArticoli.FirstOrDefault();

        var barcodeMatch = CanModifyDocument && IsBarcodeScan(searchText)
            ? FindBestBarcodeMatch(results, searchText)
            : null;

        if (barcodeMatch is not null)
        {
            var barcodeToken = NormalizeArticleSearchToken(searchText);
            InvalidatePendingArticleSearch(clearSearchText: true);
            ArticoloSelezionato = barcodeMatch;
            IsArticlePopupOpen = false;
            await AggiungiArticoloAsync(fromAutoSearch: true, capturedSearchText: barcodeToken);
            StatusMessage = "Barcode riconosciuto e articolo aggiunto automaticamente al documento.";
            return;
        }

        if (results.Count == 1 && CanModifyDocument)
        {
            await AggiungiArticoloAsync(fromAutoSearch: true);
            StatusMessage = "Articolo trovato e aggiunto automaticamente al documento.";
            return;
        }

        StatusMessage = results.Count == 0
            ? "Nessun articolo trovato con i criteri indicati."
            : $"Articoli trovati: {results.Count}. Seleziona la riga corretta e aggiungila al documento.";

        if (results.Count == 0 && explicitUserRequest && IsDirectArticleEntry(searchText))
        {
            DirectArticleMissing?.Invoke(searchText.Trim());
        }
    }

    private async Task AggiungiArticoloAsync(bool fromAutoSearch = false, string? capturedSearchText = null)
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null || ArticoloSelezionato is null)
        {
            return;
        }

        if (fromAutoSearch && IsDuplicateAutoSearchInsert(capturedSearchText, ArticoloSelezionato))
        {
            return;
        }

        var dettaglioPrezzi = await GetArticlePricingDetailAsync(ArticoloSelezionato);
        var quantitaDaInserire = 1m;

        if (dettaglioPrezzi is not null)
        {
            quantitaDaInserire = CalcolaQuantitaPredefinitaArticolo(dettaglioPrezzi);
            if (dettaglioPrezzi.RichiedeSceltaQuantita && ArticleQuantitySelectionRequested is not null)
            {
                var quantitaSelezionata = await ArticleQuantitySelectionRequested(ArticoloSelezionato, dettaglioPrezzi, quantitaDaInserire);
                if (!quantitaSelezionata.HasValue)
                {
                    ClearPendingArticleSelection();
                    StatoDocumento = "Inserimento articolo annullato";
                    StatusMessage = "Inserimento articolo annullato. Ricerca articolo pronta per un nuovo codice.";
                    FocusArticleSearchRequested?.Invoke();
                    return;
                }

                quantitaDaInserire = NormalizeArticleQuantity(quantitaSelezionata.Value, dettaglioPrezzi);
            }
        }

        var decisioneDisponibilitaNegativa = await ResolveNegativeAvailabilityDecisionAsync(quantitaDaInserire);
        if (decisioneDisponibilitaNegativa == NegativeAvailabilityDecision.Annulla)
        {
            ClearPendingArticleSelection();
            StatoDocumento = "Inserimento articolo annullato";
            StatusMessage = "Inserimento articolo annullato per giacenza non disponibile.";
            FocusArticleSearchRequested?.Invoke();
            return;
        }

        var rigaCreataComeManuale = decisioneDisponibilitaNegativa == NegativeAvailabilityDecision.ConvertiInManuale;
        var aggiungiARiordino = decisioneDisponibilitaNegativa == NegativeAvailabilityDecision.VendiEAggiungiALista;
        RigaDocumentoLocale? rigaAccorpata = null;
        RigaDocumentoLocale? rigaInserita = null;
        if (!rigaCreataComeManuale)
        {
            rigaAccorpata = TryAccorpaArticoloSelezionato(quantitaDaInserire, dettaglioPrezzi);
        }

        if (rigaAccorpata is null)
        {
            var prezzoUnitario = ResolveArticleUnitPrice(dettaglioPrezzi, quantitaDaInserire, ArticoloSelezionato.PrezzoVendita);
            rigaInserita = CreateArticleRowFromSelection(
                prezzoUnitario,
                quantitaDaInserire,
                dettaglioPrezzi,
                asManualRow: rigaCreataComeManuale);
            DocumentoLocaleCorrente.AggiungiRiga(rigaInserita);
        }

        if (fromAutoSearch)
        {
            RegisterAutoSearchInsert(capturedSearchText, ArticoloSelezionato);
        }

        RefreshRows();
        var rigaTargetId = rigaAccorpata?.Id ?? rigaInserita?.Id;
        RigaSelezionata = rigaTargetId.HasValue
            ? Righe.LastOrDefault(item => item.Id == rigaTargetId.Value)
            : Righe.LastOrDefault();

        if (aggiungiARiordino && RigaSelezionata is not null)
        {
            await AddSelectedRowToReorderListAsync();
        }

        SearchArticoloText = string.Empty;
        RisultatiRicercaArticoli.Clear();
        ArticoloSelezionato = null;
        IsArticlePopupOpen = false;
        StatoDocumento = "Scheda Banco aggiornata";
        StatusMessage = BuildArticleInsertStatusMessage(
            rigaAccorpata,
            dettaglioPrezzi,
            quantitaDaInserire,
            rigaCreataComeManuale,
            aggiungiARiordino);
        HasPendingLocalChanges = true;

        if (fromAutoSearch)
        {
            FocusArticleSearchRequested?.Invoke();
        }
    }

    private async Task<NegativeAvailabilityDecision> ResolveNegativeAvailabilityDecisionAsync(decimal quantitaDaInserire)
    {
        if (ArticoloSelezionato is null || ArticoloSelezionato.Giacenza > 0)
        {
            return NegativeAvailabilityDecision.ScaricaComunque;
        }

        if (NegativeAvailabilityDecisionRequested is null)
        {
            return NegativeAvailabilityDecision.ScaricaComunque;
        }

        return await NegativeAvailabilityDecisionRequested(ArticoloSelezionato, quantitaDaInserire);
    }

    private void ClearPendingArticleSelection()
    {
        SearchArticoloText = string.Empty;
        RisultatiRicercaArticoli.Clear();
        ArticoloSelezionato = null;
        IsArticlePopupOpen = false;
    }

    private RigaDocumentoLocale? TryAccorpaArticoloSelezionato(decimal quantitaDaAggiungere, GestionaleArticlePricingDetail? dettaglioPrezzi)
    {
        if (DocumentoLocaleCorrente is null || ArticoloSelezionato is null)
        {
            return null;
        }

        var barcodeSelezionato = NormalizeBarcodeIdentity(ArticoloSelezionato.BarcodeAlternativo);
        var rigaEsistente = DocumentoLocaleCorrente.Righe.FirstOrDefault(riga =>
            riga.TipoRiga == TipoRigaDocumento.Articolo &&
            !riga.FlagManuale &&
            !riga.IsPromoRow &&
            IsMatchingArticleRowForMerge(riga, barcodeSelezionato));

        if (rigaEsistente is null)
        {
            return null;
        }

        rigaEsistente.Quantita = NormalizeArticleQuantity(rigaEsistente.Quantita + quantitaDaAggiungere, dettaglioPrezzi);
        rigaEsistente.DisponibilitaRiferimento = ArticoloSelezionato.Giacenza;
        if (dettaglioPrezzi is not null)
        {
            ApplicaPrezzoQuantitaArticolo(rigaEsistente, dettaglioPrezzi, fallbackPrezzoUnitario: ArticoloSelezionato.PrezzoVendita);
        }

        return rigaEsistente;
    }

    private RigaDocumentoLocale CreateArticleRowFromSelection(
        decimal prezzoUnitario,
        decimal quantitaDaInserire,
        GestionaleArticlePricingDetail? dettaglioPrezzi,
        bool asManualRow)
    {
        return new RigaDocumentoLocale
        {
            OrdineRiga = DocumentoLocaleCorrente!.Righe.Count + 1,
            TipoRiga = asManualRow ? TipoRigaDocumento.Manuale : TipoRigaDocumento.Articolo,
            ArticoloOid = asManualRow ? null : ArticoloSelezionato!.Oid,
            CodiceArticolo = asManualRow ? null : ArticoloSelezionato!.CodiceArticolo,
            BarcodeArticolo = asManualRow ? null : NormalizeBarcodeIdentity(ArticoloSelezionato!.BarcodeAlternativo),
            VarianteDettaglioOid1 = asManualRow ? null : ArticoloSelezionato!.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = asManualRow ? null : ArticoloSelezionato!.VarianteDettaglioOid2,
            Descrizione = BuildDocumentRowDescription(ArticoloSelezionato!),
            UnitaMisura = dettaglioPrezzi?.UnitaMisuraPrincipale ?? "PZ",
            Quantita = quantitaDaInserire,
            DisponibilitaRiferimento = ArticoloSelezionato!.Giacenza,
            PrezzoUnitario = prezzoUnitario,
            ScontoPercentuale = 0,
            IvaOid = ArticoloSelezionato.IvaOid,
            AliquotaIva = ArticoloSelezionato.AliquotaIva,
            FlagManuale = asManualRow
        };
    }

    private string BuildArticleInsertStatusMessage(
        RigaDocumentoLocale? rigaAccorpata,
        GestionaleArticlePricingDetail? dettaglioPrezzi,
        decimal quantitaDaInserire,
        bool createdAsManualRow,
        bool addedToReorderList)
    {
        if (createdAsManualRow)
        {
            return "Giacenza non disponibile: articolo inserito come riga manuale senza codice.";
        }

        var messaggio = rigaAccorpata is null
            ? BuildArticleAddedStatusMessage(dettaglioPrezzi, quantitaDaInserire)
            : BuildArticleMergedStatusMessage(rigaAccorpata, dettaglioPrezzi);

        return addedToReorderList
            ? $"{messaggio} Riga aggiunta anche alla lista riordino."
            : messaggio;
    }

    private bool IsMatchingArticleRowForMerge(RigaDocumentoLocale riga, string? barcodeSelezionato)
    {
        if (ArticoloSelezionato is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(barcodeSelezionato))
        {
            return string.Equals(
                NormalizeBarcodeIdentity(riga.BarcodeArticolo),
                barcodeSelezionato,
                StringComparison.OrdinalIgnoreCase);
        }

        if (ArticoloSelezionato.IsVariante)
        {
            return false;
        }

        return riga.ArticoloOid.HasValue && riga.ArticoloOid.Value == ArticoloSelezionato.Oid;
    }

    private static string? NormalizeBarcodeIdentity(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        return barcode.Trim().ToUpperInvariant();
    }

    private async Task<GestionaleArticlePricingDetail?> GetArticlePricingDetailAsync(GestionaleArticleSearchResult articolo)
    {
        var pricingCacheKey = BuildPricingDetailCacheKey(articolo);
        if (_articlePricingDetails.TryGetValue(pricingCacheKey, out var dettaglioInCache))
        {
            return dettaglioInCache;
        }

        var dettaglio = await _articleReadService.GetArticlePricingDetailAsync(articolo, ListinoSelezionato?.Oid);
        if (dettaglio is null)
        {
            return null;
        }

        _articlePricingDetails[pricingCacheKey] = dettaglio;
        return dettaglio;
    }

    private async Task EnsureArticlePricingAppliedAsync(RigaDocumentoLocale riga, RigaDocumentoLocaleViewModel? rigaViewModel = null)
    {
        if (!riga.ArticoloOid.HasValue || riga.FlagManuale || riga.IsPromoRow)
        {
            return;
        }

        var pricingCacheKey = BuildPricingDetailCacheKey(riga);
        if (!_articlePricingDetails.TryGetValue(pricingCacheKey, out var dettaglioPrezzi))
        {
            dettaglioPrezzi = await _articleReadService.GetArticlePricingDetailAsync(BuildSearchResultFromRow(riga), ListinoSelezionato?.Oid);
            if (dettaglioPrezzi is null)
            {
                return;
            }

            _articlePricingDetails[pricingCacheKey] = dettaglioPrezzi;
        }

        var prezzoPrecedente = riga.PrezzoUnitario;
        var quantitaPrecedente = riga.Quantita;
        var prezzoIniziale = rigaViewModel?.PrezzoUnitario ?? riga.PrezzoUnitario;
        ApplicaPrezzoQuantitaArticolo(riga, dettaglioPrezzi, prezzoIniziale);
        rigaViewModel?.NotifyPricingChanged();

        if (prezzoPrecedente != riga.PrezzoUnitario || quantitaPrecedente != riga.Quantita)
        {
            NotifyTotalsChanged();
            NotifySelectedRowStateChanged();
            HasPendingLocalChanges = true;
        }
    }

    private string BuildPricingDetailCacheKey(GestionaleArticleSearchResult articolo)
    {
        var listinoKey = ListinoSelezionato?.Oid ?? 0;
        var barcode = NormalizeBarcodeIdentity(articolo.BarcodeAlternativo);
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            return $"LISTINO:{listinoKey}|BARCODE:{barcode}";
        }

        return $"LISTINO:{listinoKey}|ARTICOLO:{articolo.Oid}";
    }

    private string BuildPricingDetailCacheKey(RigaDocumentoLocale riga)
    {
        var listinoKey = ListinoSelezionato?.Oid ?? 0;
        var barcode = NormalizeBarcodeIdentity(riga.BarcodeArticolo);
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            return $"LISTINO:{listinoKey}|BARCODE:{barcode}";
        }

        return $"LISTINO:{listinoKey}|ARTICOLO:{riga.ArticoloOid.GetValueOrDefault()}";
    }

    private static decimal CalcolaQuantitaPredefinitaArticolo(GestionaleArticlePricingDetail dettaglioPrezzi)
    {
        return NormalizeArticleQuantity(1, dettaglioPrezzi);
    }

    private static decimal NormalizeArticleQuantity(decimal requestedQuantity, GestionaleArticlePricingDetail? dettaglioPrezzi)
    {
        var normalized = requestedQuantity <= 0 ? 1 : requestedQuantity;
        if (dettaglioPrezzi is null)
        {
            return normalized;
        }

        var quantitaMinima = dettaglioPrezzi.QuantitaMinimaVendita <= 0 ? 1 : dettaglioPrezzi.QuantitaMinimaVendita;
        normalized = Math.Max(normalized, quantitaMinima);

        var quantitaMultipla = dettaglioPrezzi.QuantitaMultiplaVendita <= 0 ? 1 : dettaglioPrezzi.QuantitaMultiplaVendita;
        if (quantitaMultipla > 1)
        {
            normalized = Math.Ceiling(normalized / quantitaMultipla) * quantitaMultipla;
        }

        return normalized;
    }

    private static decimal ResolveArticleUnitPrice(
        GestionaleArticlePricingDetail? dettaglioPrezzi,
        decimal quantita,
        decimal fallbackPrezzoUnitario)
    {
        if (dettaglioPrezzi is null || dettaglioPrezzi.FascePrezzoQuantita.Count == 0)
        {
            return fallbackPrezzoUnitario;
        }

        var fasciaApplicata = dettaglioPrezzi.FascePrezzoQuantita
            .Where(fascia => quantita >= fascia.QuantitaMinima)
            .OrderByDescending(fascia => fascia.QuantitaMinima)
            .FirstOrDefault();

        if (fasciaApplicata is not null)
        {
            return fasciaApplicata.PrezzoUnitario;
        }

        return fallbackPrezzoUnitario > 0
            ? fallbackPrezzoUnitario
            : dettaglioPrezzi.FascePrezzoQuantita
                .OrderBy(fascia => fascia.QuantitaMinima)
                .First()
                .PrezzoUnitario;
    }

    private static void ApplicaPrezzoQuantitaArticolo(
        RigaDocumentoLocale riga,
        GestionaleArticlePricingDetail dettaglioPrezzi,
        decimal fallbackPrezzoUnitario)
    {
        riga.UnitaMisura = string.IsNullOrWhiteSpace(dettaglioPrezzi.UnitaMisuraPrincipale)
            ? "PZ"
            : dettaglioPrezzi.UnitaMisuraPrincipale;
        riga.Quantita = NormalizeArticleQuantity(riga.Quantita, dettaglioPrezzi);
        riga.PrezzoUnitario = ResolveArticleUnitPrice(dettaglioPrezzi, riga.Quantita, fallbackPrezzoUnitario);
    }

    private string BuildArticleAddedStatusMessage(GestionaleArticlePricingDetail? dettaglioPrezzi, decimal quantitaInserita)
    {
        if (dettaglioPrezzi is null || dettaglioPrezzi.FascePrezzoQuantita.Count <= 1)
        {
            return "Articolo aggiunto al documento corrente.";
        }

        return $"Articolo aggiunto. Quantita` {quantitaInserita:N2} {dettaglioPrezzi.UnitaMisuraPrincipale}. {BuildArticleTierSummary(dettaglioPrezzi)}";
    }

    private string BuildArticleMergedStatusMessage(RigaDocumentoLocale riga, GestionaleArticlePricingDetail? dettaglioPrezzi)
    {
        if (dettaglioPrezzi is null || dettaglioPrezzi.FascePrezzoQuantita.Count <= 1)
        {
            return "Articolo gia` presente: quantita` aggiornata sulla riga esistente.";
        }

        return $"Articolo gia` presente: quantita` aggiornata a {riga.Quantita:N2} {dettaglioPrezzi.UnitaMisuraPrincipale}. {BuildArticleTierSummary(dettaglioPrezzi)}";
    }

    private static string BuildArticleTierSummary(GestionaleArticlePricingDetail dettaglioPrezzi)
    {
        var fasce = dettaglioPrezzi.FascePrezzoQuantita
            .OrderBy(fascia => fascia.QuantitaMinima)
            .Select(fascia => $"da {fascia.QuantitaMinima:N0} {dettaglioPrezzi.UnitaMisuraPrincipale} = {fascia.PrezzoUnitario:N2}")
            .ToList();

        return fasce.Count == 0
            ? string.Empty
            : $"Prezzi q.ta`: {string.Join(" | ", fasce)}.";
    }

    private Task CercaClientiAsync()
    {
        var requestVersion = ++_customerSearchRequestVersion;
        return CercaClientiAsync(requestVersion, SearchClienteText);
    }

    private async Task CercaClientiAsync(int requestVersion, string searchText)
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var results = await _customerReadService.SearchCustomersAsync(searchText);
        if (!IsCustomerSearchRequestCurrent(requestVersion, searchText))
        {
            return;
        }

        RisultatiRicercaClienti.Clear();
        ClienteSelezionato = null;
        foreach (var result in results)
        {
            RisultatiRicercaClienti.Add(result);
        }

        IsCustomerPopupOpen = results.Count > 0;
        ClienteSelezionato = results.Count == 1 ? RisultatiRicercaClienti.FirstOrDefault() : null;
        StatusMessage = results.Count == 0
            ? "Nessun cliente trovato con i criteri indicati."
            : $"Clienti trovati: {results.Count}. Seleziona il cliente da associare al documento.";
    }

    private static GestionaleCustomerSummary? ResolveBestCustomerSelection(
        string? searchText,
        IEnumerable<GestionaleCustomerSummary> results)
    {
        var items = results.ToList();
        if (items.Count == 0)
        {
            return null;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        var normalizedSearch = NormalizeCustomerSelectionToken(searchText);
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return null;
        }

        return items.FirstOrDefault(cliente =>
                   NormalizeCustomerSelectionToken(cliente.DisplayLabel) == normalizedSearch ||
                   NormalizeCustomerSelectionToken(cliente.DisplayName) == normalizedSearch ||
                   NormalizeCustomerSelectionToken(cliente.CodiceCartaFedelta) == normalizedSearch)
               ?? items.FirstOrDefault(cliente =>
                   NormalizeCustomerSelectionToken(cliente.DisplayLabel).Contains(normalizedSearch, StringComparison.Ordinal) ||
                   NormalizeCustomerSelectionToken(cliente.DisplayName).Contains(normalizedSearch, StringComparison.Ordinal) ||
                   NormalizeCustomerSelectionToken(cliente.CodiceCartaFedelta).Contains(normalizedSearch, StringComparison.Ordinal));
    }

    private void SelezionaCliente()
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null || ClienteSelezionato is null)
        {
            return;
        }

        DocumentoLocaleCorrente.ImpostaCliente(ClienteSelezionato.Oid, ClienteSelezionato.DisplayLabel);
        SetCustomerSearchText(BuildCustomerSearchLabel(ClienteSelezionato));
        NotifyPropertyChanged(nameof(ClienteCorrente));
        NotifyPropertyChanged(nameof(ClienteCorrenteDisplay));
        NotifyPropertyChanged(nameof(PuntiClienteCorrente));
        NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
        NotifyPropertyChanged(nameof(CustomerInfoLineText));
        IsClienteConfermato = true;
        IsCustomerPopupOpen = false;
        StatoDocumento = "Cliente aggiornato";
        StatusMessage = $"Cliente documento impostato su {ClienteSelezionato.DisplayName}.";
        HasPendingLocalChanges = true;
        SchedulePromoRefresh();
        FocusArticleSearchRequested?.Invoke();
    }

    public void SelezionaArticoloDaPopup(GestionaleArticleSearchResult? articolo)
    {
        if (articolo is null)
        {
            return;
        }

        ArticoloSelezionato = articolo;
    }

    public void AggiungiArticoloDaPopup(GestionaleArticleSearchResult? articolo)
    {
        if (articolo is null)
        {
            return;
        }

        ArticoloSelezionato = articolo;
        _ = AggiungiArticoloAsync();
    }

    public void SelezionaClienteDaPopup(GestionaleCustomerSummary? cliente, bool applyImmediately)
    {
        if (cliente is null)
        {
            return;
        }

        ClienteSelezionato = cliente;
        if (applyImmediately)
        {
            SelezionaCliente();
        }
    }

    public void RipristinaTestoClienteCorrente()
    {
        if (ClienteSelezionato is not null)
        {
            SetCustomerSearchText(BuildCustomerSearchLabel(ClienteSelezionato));
        }
        else
        {
            SetCustomerSearchText(ClienteCorrenteDisplay);
        }

        IsCustomerPopupOpen = false;
    }

    private async void RimuoviRiga(RigaDocumentoLocaleViewModel? riga)
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null || riga is null)
        {
            return;
        }

        if (riga.Model.IsPromoRow)
        {
            await _promotionHistoryService.RecordAsync(new PromotionEventRecord
            {
                CampaignOid = riga.Model.PromoCampaignOid.GetValueOrDefault(),
                RuleId = Guid.TryParse(riga.Model.PromoRuleId, out var ruleId) ? ruleId : null,
                CustomerOid = ClienteSelezionato?.Oid,
                LocalDocumentId = DocumentoLocaleCorrente.Id,
                EventType = PromotionEventType.Reversed,
                RewardType = riga.Model.TipoRiga == TipoRigaDocumento.PremioArticolo
                    ? PointsRewardType.ArticoloPremio
                    : PointsRewardType.ScontoFisso,
                AvailablePoints = PromoEligibility.Summary.TotalAvailablePoints,
                RequiredPoints = PromoEligibility.Summary.RequiredPoints,
                AppliedRowId = riga.Id,
                Title = "Premio reversato",
                Message = "La riga premio e` stata rimossa manualmente dal documento."
            });
        }

        DocumentoLocaleCorrente.RimuoviRiga(riga.Id);
        RinominaOrdiniRiga();
        RefreshRows();
        StatusMessage = "Riga rimossa dal documento.";
        HasPendingLocalChanges = true;
    }

    public async Task SalvaDocumentoAsync()
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null)
        {
            return;
        }

        if (!HasPendingLocalChanges)
        {
            StatoDocumento = "Scheda Banco gia` allineata";
            StatusMessage = "Nessuna modifica locale da salvare.";
            return;
        }

        _isPaymentOperationInProgress = true;
        RaiseCommandStateChanged();
        ShowOperationPopup("Salva", "Pubblicazione documento Banco sul legacy in corso...", true, false);

        try
        {
            CommitPaymentInputsToDocument();
            await RefreshPreviewDocumentNumberAsync();
            DocumentoLocaleCorrente.SegnaInChiusura();
            var documentoSalvatoLabel = GetOperationalDocumentLabel(DocumentoLocaleCorrente);

            var workflowResult = await _bancoDocumentWorkflowService.PublishAsync(
                DocumentoLocaleCorrente,
                CategoriaDocumentoBanco.Indeterminata);

            ApplyPublishSuccessUiState(workflowResult, CategoriaDocumentoBanco.Indeterminata, "Salva");
            _processLogService.Info(
                nameof(BancoViewModel),
                $"Documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)} pubblicato su db_diltech con OID {workflowResult.DocumentoGestionaleOid} tramite Salva.");
            StatusMessage = $"Documento {documentoSalvatoLabel} pubblicato su db_diltech.";
            await ResetCurrentTabToNewDocumentAsync("Operazione completata", StatusMessage, true);
        }
        catch (Exception ex)
        {
            if (DocumentoLocaleCorrente is not null)
            {
                DocumentoLocaleCorrente.Riapri();
            }

            ClearTransientPublishedUiStateAfterFailure();
            StatoDocumento = "Pubblicazione Banco non completata";
            StatusMessage = $"Il documento non e` stato pubblicato su db_diltech: {ex.Message}";
            _processLogService.Error(nameof(BancoViewModel), $"Errore durante la pubblicazione Banco tramite Salva per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.", ex);
            RefreshPublishedDocumentUiState();
            await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
        }
        finally
        {
            _isPaymentOperationInProgress = false;
            RaiseCommandStateChanged();
        }
    }

    public async Task CancellaSchedaAsync()
    {
        if (DocumentoLocaleCorrente is null)
        {
            return;
        }

        var documentoGestionaleOid = DocumentoLocaleCorrente.DocumentoGestionaleOid;
        var documentoLocaleId = DocumentoLocaleCorrente.Id;

        try
        {
            if (documentoGestionaleOid.HasValue)
            {
                await _documentDeleteService.DeleteNonFiscalizedDocumentAsync(documentoGestionaleOid.Value);
                _processLogService.Info(nameof(BancoViewModel), $"Documento Banco legacy {documentoGestionaleOid.Value} cancellato da db_diltech.");

                var metadataCollegato = await _localDocumentRepository.GetByDocumentoGestionaleOidAsync(documentoGestionaleOid.Value);
                if (metadataCollegato is not null)
                {
                    await _localDocumentRepository.DeleteAsync(metadataCollegato.Id);
                    _processLogService.Info(nameof(BancoViewModel), $"Supporto tecnico collegato al documento legacy {documentoGestionaleOid.Value} rimosso da SQLite.");
                }

                OfficialDocumentDeleted?.Invoke(documentoGestionaleOid.Value);
            }
            else
            {
                await _localDocumentRepository.DeleteAsync(documentoLocaleId);
                _processLogService.Info(nameof(BancoViewModel), $"Scheda Banco {documentoLocaleId:N} cancellata da SQLite.");
            }

            await NuovoDocumentoAsync();
            StatoDocumento = "Scheda cancellata";
            StatusMessage = documentoGestionaleOid.HasValue
                ? "Vendita non scontrinata cancellata dal DB legacy. Nuova scheda Banco pronta."
                : "Scheda eliminata. Nuova scheda Banco pronta.";
        }
        catch (Exception ex)
        {
            StatoDocumento = "Cancellazione scheda non completata";
            StatusMessage = $"Non sono riuscito a cancellare la vendita corrente: {ex.Message}";
            _processLogService.Error(nameof(BancoViewModel), $"Errore durante la cancellazione della scheda Banco {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.", ex);
        }
    }

    private void ShowOperationPopup(string title, string message, bool isBusy, bool isSuccess, bool canCancel = false, bool canRetry = false)
    {
        OperationPopupTitle = title;
        OperationPopupMessage = message;
        OperationPopupFooterText = canRetry
            ? "Puoi ritentare subito senza chiudere la scheda."
            : "L'operazione viene completata automaticamente.";
        OperationPopupIsBusy = isBusy;
        OperationPopupIsSuccess = isSuccess;
        OperationPopupCanCancel = canCancel;
        OperationPopupCanRetry = canRetry;
        IsOperationPopupVisible = true;
        AnnullaOperazionePosCommand.RaiseCanExecuteChanged();
        RiprovaOperazionePosCommand.RaiseCanExecuteChanged();
    }

    private void HideOperationPopup()
    {
        _operationPopupRetryTcs?.TrySetResult(false);
        _operationPopupRetryTcs = null;
        IsOperationPopupVisible = false;
        OperationPopupIsBusy = false;
        OperationPopupCanCancel = false;
        OperationPopupCanRetry = false;
        AnnullaOperazionePosCommand.RaiseCanExecuteChanged();
        RiprovaOperazionePosCommand.RaiseCanExecuteChanged();
    }

    private async Task ShowFinalOperationPopupAsync(string title, string message, bool isSuccess)
    {
        ShowOperationPopup(title, message, false, isSuccess, false, false);
        var visibleDuration = isSuccess
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(6);
        await Task.Delay(visibleDuration);
        HideOperationPopup();
    }

    private async Task<bool> ShowRetryableOperationPopupAsync(string title, string message)
    {
        _operationPopupRetryTcs?.TrySetResult(false);
        _operationPopupRetryTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ShowOperationPopup(title, message, false, false, false, true);
        return await _operationPopupRetryTcs.Task;
    }

    private async Task ShowReorderOperationPopupAsync(string message)
    {
        ShowOperationPopup("Lista riordino", message, false, true, false, false);
        await Task.Delay(TimeSpan.FromMilliseconds(900));
        HideOperationPopup();
    }

    private void AnnullaOperazionePos()
    {
        if (!OperationPopupCanCancel || _posPaymentCts is null || _posPaymentCts.IsCancellationRequested)
        {
            return;
        }

        _processLogService.Info(nameof(BancoViewModel), $"Annullamento richiesto dall'operatore per il pagamento POS del documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
        ShowOperationPopup("Pagamento POS", "Richiesta di annullamento al terminale in corso...", true, false, false);
        _posPaymentCts.Cancel();
    }

    private void RiprovaOperazionePos()
    {
        if (!OperationPopupCanRetry || _operationPopupRetryTcs is null)
        {
            return;
        }

        _processLogService.Info(nameof(BancoViewModel), $"Ritento immediato richiesto dall'operatore per il pagamento POS del documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
        var retryTcs = _operationPopupRetryTcs;
        _operationPopupRetryTcs = null;
        HideOperationPopup();
        retryTcs.TrySetResult(true);
    }

    private static bool CanRetryPosPaymentImmediately(PosPaymentResult result)
    {
        return result.FailureKind is PosPaymentFailureKind.RejectedByTerminal
            or PosPaymentFailureKind.ConnectionUnavailable
            or PosPaymentFailureKind.TechnicalError;
    }

    public void ApplicaImportoPagamentoDiretto(string? tipo)
    {
        if (!CanModifyDocument || DocumentoLocaleCorrente is null || string.IsNullOrWhiteSpace(tipo))
        {
            return;
        }

        var tipoNormalizzato = tipo.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(_lastAutoAssignedPaymentType) &&
            !_lastAutoAssignedPaymentType.Equals(tipoNormalizzato, StringComparison.OrdinalIgnoreCase))
        {
            ClearPagamentoText(_lastAutoAssignedPaymentType);
        }

        var importo = tipoNormalizzato switch
        {
            "sconto" => CalcolaImportoResiduoPerSconto(),
            "contanti" => CalcolaImportoResiduoPerPagamento("contanti"),
            "carta" => CalcolaImportoResiduoPerPagamento("carta"),
            "bancomat" => CalcolaImportoResiduoPerPagamento("carta"),
            "sospeso" => CalcolaImportoResiduoPerPagamento("sospeso"),
            "buoni" => CalcolaImportoResiduoPerPagamento("buoni"),
            _ => 0
        };

        var importoFormattato = FormatCurrencyInput(importo);
        if (string.Equals(_lastAutoAssignedPaymentType, tipoNormalizzato, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetPagamentoText(tipoNormalizzato), importoFormattato, StringComparison.Ordinal))
        {
            return;
        }

        SetPagamentoText(tipoNormalizzato, importo);
        _lastAutoAssignedPaymentType = tipoNormalizzato;
        _processLogService.Info(nameof(BancoViewModel), $"Importo pagamento calcolato. Tipo={tipoNormalizzato}, Importo={importo:N2}, Residuo={Residuo:N2}, Documento={GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
    }

    private void ResetState()
    {
        _documentoGestionaleOrigine = null;
        ClearPreviewDocumentNumber();
        DocumentoLocaleCorrente = null;
        ApplyDocumentAccessResolution(BancoDocumentoAccessResolver.Resolve(
            localMetadata: null,
            legacyDocumentOid: null,
            legacyDocumentLabel: null,
            legacyHasScontrinatoPayments: false));
        IsScontrinato = false;
        StatoDocumento = "Nessun documento aperto";
        SearchArticoloText = string.Empty;
        SetCustomerSearchText(string.Empty);
        RisultatiRicercaArticoli.Clear();
        RisultatiRicercaClienti.Clear();
        ListiniDisponibili.Clear();
        ArticoloSelezionato = null;
        ClienteSelezionato = null;
        _isApplyingSelectedPriceList = true;
        try
        {
            ListinoSelezionato = null;
        }
        finally
        {
            _isApplyingSelectedPriceList = false;
        }
        IsClienteConfermato = false;
        NotifyPropertyChanged(nameof(PuntiClienteCorrente));
        IsArticlePopupOpen = false;
        IsCustomerPopupOpen = false;
        RigaSelezionata = null;
        Righe.Clear();
        _lastAutoAssignedPaymentType = null;
        ResetPaymentInputs();
        StatusMessage = string.Empty;
    }

    private void RefreshRows()
    {
        Righe.Clear();

        if (DocumentoLocaleCorrente is null)
        {
            NotifyPropertyChanged(nameof(HasDocumentRows));
            NotifyTotalsChanged();
            return;
        }

        foreach (var riga in DocumentoLocaleCorrente.Righe.OrderBy(r => r.OrdineRiga))
        {
            Righe.Add(new RigaDocumentoLocaleViewModel(riga, OnRowValuesChanged));
        }

        NotifyPropertyChanged(nameof(HasDocumentRows));
        NotifyPropertyChanged(nameof(EmptyDocumentStateTitle));
        NotifyPropertyChanged(nameof(EmptyDocumentStateMessage));
        NotifyTotalsChanged();
        SchedulePromoRefresh();
        _ = RefreshReorderMarkersAsync();
    }

    private void OnRowValuesChanged(RigaDocumentoLocaleViewModel riga, string propertyName)
    {
        if (string.Equals(propertyName, nameof(RigaDocumentoLocaleViewModel.Quantita), StringComparison.Ordinal) &&
            riga.Model.ArticoloOid.HasValue &&
            !riga.Model.FlagManuale &&
            !riga.Model.IsPromoRow)
        {
            _ = EnsureArticlePricingAppliedAsync(riga.Model, riga);
        }

        NotifyTotalsChanged();
        NotifySelectedRowStateChanged();
        HasPendingLocalChanges = true;
        SchedulePromoRefresh();
    }

    private void NotifySelectedRowStateChanged()
    {
        NotifyPropertyChanged(nameof(HasRigaSelezionata));
        NotifyPropertyChanged(nameof(QuantitaRigaSelezionata));
        NotifyPropertyChanged(nameof(DisponibilitaResiduaRigaSelezionata));
        NotifyPropertyChanged(nameof(IsDisponibilitaResiduaCritica));
    }

    private void ModificaQuantitaRigaSelezionata(decimal delta)
    {
        if (RigaSelezionata is null)
        {
            return;
        }

        RigaSelezionata.Quantita = Math.Max(1, RigaSelezionata.Quantita + delta);
        NotifySelectedRowStateChanged();
        HasPendingLocalChanges = true;
    }

    private void NotifyTotalsChanged()
    {
        NotifyPropertyChanged(nameof(TotaleDocumento));
        NotifyPropertyChanged(nameof(TotalePagato));
        NotifyPropertyChanged(nameof(TotaleSconto));
        NotifyPropertyChanged(nameof(TotaleSospeso));
        NotifyPropertyChanged(nameof(Residuo));
        NotifyPropertyChanged(nameof(Resto));
        NotifyPropertyChanged(nameof(TotaleDaIncassare));
        NotifyPropertyChanged(nameof(HasSospesoDocumento));
        NotifyPropertyChanged(nameof(SospesoDocumentoLabel));
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
    }

    private async Task RefreshReorderMarkersAsync()
    {
        try
        {
            var snapshot = await _reorderListRepository.GetCurrentListAsync();
            var articleOids = snapshot.Items
                .ToList();

            foreach (var row in Righe)
            {
                row.IsInReorderList = articleOids.Any(item => IsSameReorderIdentity(item, row));
            }
        }
        catch (Exception ex)
        {
            _processLogService.Warning(nameof(BancoViewModel), $"Impossibile aggiornare i marker lista riordino: {ex.Message}");
        }
    }

    private static bool IsSameReorderIdentity(ReorderListItem item, RigaDocumentoLocaleViewModel row)
    {
        if (!row.Model.ArticoloOid.HasValue || row.Model.ArticoloOid.Value <= 0)
        {
            return false;
        }

        if (item.ArticoloOid != row.Model.ArticoloOid.Value)
        {
            return false;
        }

        return string.Equals(NormalizeReorderText(item.CodiceArticolo), NormalizeReorderText(row.CodiceArticolo), StringComparison.Ordinal)
            && string.Equals(NormalizeReorderText(item.Descrizione), NormalizeReorderText(row.Descrizione), StringComparison.Ordinal);
    }

    private static string NormalizeReorderText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
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

    private void OnReorderListChanged()
    {
        _ = RefreshReorderMarkersAsync();
    }

    private static string? NormalizeDocumentoLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var normalized = label.Trim();
        var match = Regex.Match(normalized, @"(\d+)(?:/\d+)?");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return normalized;
    }

    private async Task RefreshPreviewDocumentNumberAsync()
    {
        if (DocumentoLocaleCorrente is null || HasAnyOfficialLegacyReference(DocumentoLocaleCorrente))
        {
            ClearPreviewDocumentNumber();
            return;
        }

        try
        {
            var nextNumber = await _documentReadService.GetNextBancoDocumentNumberAsync();
            _previewNumeroDocumentoGestionale = nextNumber.Numero;
            _previewAnnoDocumentoGestionale = nextNumber.Anno;
        }
        catch
        {
            _previewNumeroDocumentoGestionale = null;
            _previewAnnoDocumentoGestionale = null;
        }

        NotifyPropertyChanged(nameof(DocumentoRiferimentoLabel));
        NotifyPropertyChanged(nameof(Titolo));
    }

    private void ClearPreviewDocumentNumber()
    {
        if (!_previewNumeroDocumentoGestionale.HasValue && !_previewAnnoDocumentoGestionale.HasValue)
        {
            return;
        }

        _previewNumeroDocumentoGestionale = null;
        _previewAnnoDocumentoGestionale = null;
        NotifyPropertyChanged(nameof(DocumentoRiferimentoLabel));
        NotifyPropertyChanged(nameof(Titolo));
    }

    private static string? FormatDocumentoLocaleLabel(DocumentoLocale? documentoLocale)
    {
        if (documentoLocale is null)
        {
            return null;
        }

        if (documentoLocale.NumeroDocumentoGestionale.HasValue && documentoLocale.AnnoDocumentoGestionale.HasValue)
        {
            return $"{documentoLocale.NumeroDocumentoGestionale}/{documentoLocale.AnnoDocumentoGestionale}";
        }

        return null;
    }

    private static string GetOperationalDocumentLabel(DocumentoLocale? documentoLocale)
    {
        var legacyLabel = FormatDocumentoLocaleLabel(documentoLocale);
        if (!string.IsNullOrWhiteSpace(legacyLabel))
        {
            return legacyLabel;
        }

        return "scheda corrente";
    }

    private static string? NormalizeClienteLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var normalized = label.Trim();
        var match = Regex.Match(normalized, @"^\s*\d+\s*-\s*(.+?)(?:\s*\(.+\))?\s*$");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        var parenIndex = normalized.IndexOf(" (", StringComparison.Ordinal);
        if (parenIndex > 0)
        {
            normalized = normalized[..parenIndex].Trim();
        }

        match = Regex.Match(normalized, @"^\s*\d+\s*-\s*(.+)$");
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return normalized;
    }

    private void OnPaymentInputChanged()
    {
        if (_isUpdatingPaymentInputs || DocumentoLocaleCorrente is null)
        {
            return;
        }

        _paymentInputsDirty = true;
        NotifyTotalsChanged();
        HasPendingLocalChanges = true;
    }

    public void CommitPaymentInputsToDocument()
    {
        if (_isUpdatingPaymentInputs || DocumentoLocaleCorrente is null)
        {
            return;
        }

        if (!_paymentInputsDirty && DocumentoLocaleCorrente.ScontoDocumento == ScontoPagamento)
        {
            return;
        }

        DocumentoLocaleCorrente.ImpostaScontoDocumento(ScontoPagamento);
        var pagamenti = BuildPagamentiLocali().ToList();
        DocumentoLocaleCorrente.SostituisciPagamenti(pagamenti);
        _paymentInputsDirty = false;
        NotifyTotalsChanged();
        HasPendingLocalChanges = true;
    }

    private IEnumerable<PagamentoLocale> BuildPagamentiLocali()
    {
        var now = DateTimeOffset.Now;
        var offset = 0;

        if (ContantiPagamento > 0)
        {
            yield return CreatePagamento("contanti", ContantiPagamento, now, ref offset);
        }

        if (CartaPagamento > 0)
        {
            yield return CreatePagamento("carta", CartaPagamento, now, ref offset);
        }

        if (SospesoPagamento > 0)
        {
            yield return CreatePagamento("sospeso", SospesoPagamento, now, ref offset);
        }

        if (BuoniPagamento > 0)
        {
            yield return CreatePagamento("buoni", BuoniPagamento, now, ref offset);
        }

    }

    public void ApplicaContantiPredefinitiPerChiusura()
    {
        if (DocumentoLocaleCorrente is null)
        {
            return;
        }

        var importo = Math.Max(0, TotaleDaIncassare);
        if (importo <= 0)
        {
            return;
        }

        ContantiPagamentoText = FormatCurrencyInput(importo);
        _lastAutoAssignedPaymentType = "contanti";
        _processLogService.Info(
            nameof(BancoViewModel),
            $"Pagamento contanti predefinito applicato. Importo={importo:N2}, Documento={GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
    }

    public async Task EmettiCortesiaAsync()
    {
        if (DocumentoLocaleCorrente is null || !CanEmettiCortesia)
        {
            return;
        }

        _isPaymentOperationInProgress = true;
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
        ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
        ScontrinoCommand.RaiseCanExecuteChanged();
        ShowOperationPopup("Cortesia", "Pubblicazione documento Banco in corso...", true, false);

        try
        {
            CommitPaymentInputsToDocument();
            await RefreshPreviewDocumentNumberAsync();
            DocumentoLocaleCorrente.SegnaInChiusura();

            var workflowResult = await _bancoDocumentWorkflowService.PublishAsync(
                DocumentoLocaleCorrente,
                CategoriaDocumentoBanco.Cortesia);

            ApplyPublishSuccessUiState(workflowResult, CategoriaDocumentoBanco.Cortesia, "Cortesia");
            ShowOperationPopup("Cortesia", "Stampa POS80 in corso...", true, false);

            var printResult = await _bancoPosPrintService.PrintCortesiaAsync(
                DocumentoLocaleCorrente,
                customer: ClienteSelezionato);

            if (!printResult.Succeeded)
            {
                StatoDocumento = "Documento cortesia pubblicato - stampa non completata";
                StatusMessage = $"{StatusMessage} {printResult.Message}";
                _processLogService.Warning(
                    nameof(BancoViewModel),
                    $"Cortesia pubblicata ma stampa POS80 non completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {printResult.Message}");
                await ShowFinalOperationPopupAsync("Pubblicazione completata con avvisi", StatusMessage, false);
                return;
            }

            var printError = await ExecutePreparedPos80PrintAsync(printResult);
            if (!string.IsNullOrWhiteSpace(printError))
            {
                StatoDocumento = "Documento cortesia pubblicato - stampa non completata";
                StatusMessage = $"{StatusMessage} {printError}";
                _processLogService.Warning(
                    nameof(BancoViewModel),
                    $"Cortesia pubblicata ma stampa POS80 non completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {printError}");
                await ShowFinalOperationPopupAsync("Pubblicazione completata con avvisi", StatusMessage, false);
                return;
            }

            StatoDocumento = "Documento cortesia stampato";
            StatusMessage = $"{StatusMessage} {printResult.Message}";
            _processLogService.Info(
                nameof(BancoViewModel),
                $"Cortesia POS80 completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {printResult.Message}");
            await ResetCurrentTabToNewDocumentAsync("Operazione completata", StatusMessage, true);
            return;
        }
        catch (Exception ex)
        {
            if (DocumentoLocaleCorrente is not null)
            {
                DocumentoLocaleCorrente.Riapri();
            }

            ClearTransientPublishedUiStateAfterFailure();
            StatoDocumento = "Errore pubblicazione cortesia";
            StatusMessage = $"Errore durante la pubblicazione cortesia: {ex.Message}";
            _processLogService.Error(nameof(BancoViewModel), $"Errore durante la pubblicazione cortesia per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.", ex);
            RefreshPublishedDocumentUiState();
            await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
        }
        finally
        {
            _isPaymentOperationInProgress = false;
            ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
            ScontrinoCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanEmettiScontrino));
        }
    }

    public async Task AnteprimaPos80BuoniAsync()
    {
        if (DocumentoLocaleCorrente is null || !CanEmettiCortesia)
        {
            return;
        }

        _isPaymentOperationInProgress = true;
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
        ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
        ScontrinoCommand.RaiseCanExecuteChanged();
        ShowOperationPopup("Anteprima Banco", "Preparazione anteprima in corso...", true, false);

        try
        {
            CommitPaymentInputsToDocument();
            var importoBuoni = SommaPagamenti(DocumentoLocaleCorrente.Pagamenti, "buoni", "buonipasto", "ticket");
            if (importoBuoni <= 0)
            {
                importoBuoni = Residuo;
            }

            if (importoBuoni <= 0)
            {
                StatusMessage = "Nessun importo residuo disponibile per l'anteprima Banco.";
                await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
                return;
            }

            BuoniPagamentoText = FormatCurrencyInput(importoBuoni);
            CommitPaymentInputsToDocument();
            var previewResult = await _bancoPosPrintService.PreviewCortesiaAsync(DocumentoLocaleCorrente, customer: ClienteSelezionato);
            if (!previewResult.Succeeded)
            {
                StatoDocumento = "Anteprima Banco non completata";
                StatusMessage = previewResult.Message;
                _processLogService.Warning(nameof(BancoViewModel), $"Anteprima Banco non completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {previewResult.Message}");
                await ShowFinalOperationPopupAsync("Anteprima non completata", previewResult.Message, false);
                return;
            }

            _processLogService.Info(nameof(BancoViewModel), $"Anteprima Banco aperta per il documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
            StatoDocumento = "Anteprima Banco aperta";
            StatusMessage = previewResult.Message;
            if (!string.IsNullOrWhiteSpace(previewResult.OutputPath))
            {
                Pos80PreviewRequested?.Invoke(previewResult.OutputPath);
            }

            await ShowFinalOperationPopupAsync("Operazione completata", StatusMessage, true);
        }
        catch (Exception ex)
        {
            StatoDocumento = "Errore anteprima Banco";
            StatusMessage = $"Errore durante l'anteprima Banco: {ex.Message}";
            _processLogService.Error(nameof(BancoViewModel), $"Errore durante l'anteprima Banco per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.", ex);
            await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
        }
        finally
        {
            _isPaymentOperationInProgress = false;
            ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
            ScontrinoCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanEmettiScontrino));
        }
    }

    public async Task StampaPos80Async()
    {
        if (DocumentoLocaleCorrente is null)
        {
            return;
        }

        if (DocumentoLocaleCorrente.Righe.Count == 0)
        {
            StatusMessage = "Inserire almeno una riga prima di stampare il POS80.";
            return;
        }

        _isPaymentOperationInProgress = true;
        NotifyPropertyChanged(nameof(CanPrintPos80));
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
        ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
        ScontrinoCommand.RaiseCanExecuteChanged();
        ShowOperationPopup("Stampa POS80", "Preparazione stampa POS80 in corso...", true, false);

        try
        {
            CommitPaymentInputsToDocument();
            var printResult = await _bancoPosPrintService.PrintCortesiaAsync(
                DocumentoLocaleCorrente,
                customer: ClienteSelezionato);

            if (!printResult.Succeeded)
            {
                StatoDocumento = "Stampa POS80 non completata";
                StatusMessage = printResult.Message;
                _processLogService.Warning(
                    nameof(BancoViewModel),
                    $"Stampa POS80 non completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {printResult.Message}");
                await ShowFinalOperationPopupAsync("Stampa non completata", printResult.Message, false);
                return;
            }

            var printError = await ExecutePreparedPos80PrintAsync(printResult);
            if (!string.IsNullOrWhiteSpace(printError))
            {
                StatoDocumento = "Stampa POS80 non completata";
                StatusMessage = printError;
                _processLogService.Warning(
                    nameof(BancoViewModel),
                    $"Stampa POS80 non completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {printError}");
                await ShowFinalOperationPopupAsync("Stampa non completata", printError, false);
                return;
            }

            StatoDocumento = "Stampa POS80 completata";
            StatusMessage = printResult.Message;
            _processLogService.Info(
                nameof(BancoViewModel),
                $"Stampa POS80 completata per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {printResult.Message}");
            await ShowFinalOperationPopupAsync("Operazione completata", printResult.Message, true);
        }
        catch (Exception ex)
        {
            StatoDocumento = "Errore stampa POS80";
            StatusMessage = $"Errore durante la stampa POS80: {ex.Message}";
            _processLogService.Error(
                nameof(BancoViewModel),
                $"Errore durante la stampa POS80 per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.",
                ex);
            await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
        }
        finally
        {
            _isPaymentOperationInProgress = false;
            NotifyPropertyChanged(nameof(CanPrintPos80));
            NotifyPropertyChanged(nameof(CanEmettiCortesia));
            NotifyPropertyChanged(nameof(CanCancellaScheda));
            NotifyPropertyChanged(nameof(CanEmettiScontrino));
            CancellaSchedaCommand.RaiseCanExecuteChanged();
            ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
            ScontrinoCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task<string?> ExecutePreparedPos80PrintAsync(FastReportRuntimeActionResult printResult)
    {
        if (string.IsNullOrWhiteSpace(printResult.OutputPath))
        {
            return "Il file di stampa POS80 non e` stato generato.";
        }

        if (Pos80PrintRequested is null)
        {
            return "La stampa POS80 non e` disponibile nella schermata Banco.";
        }

        return await Pos80PrintRequested.Invoke(printResult.OutputPath, printResult.AssignedPrinterName);
    }

    private static PagamentoLocale CreatePagamento(string tipoPagamento, decimal importo, DateTimeOffset baseTime, ref int offset)
    {
        var pagamento = new PagamentoLocale
        {
            TipoPagamento = tipoPagamento,
            Importo = importo,
            StatoPagamentoLocale = "Registrato",
            DataOra = baseTime.AddSeconds(offset)
        };

        offset++;
        return pagamento;
    }

    public async Task EmettiScontrinoAsync(bool confermaRistampa = false)
    {
        if (DocumentoLocaleCorrente is null)
        {
            return;
        }

        CommitPaymentInputsToDocument();

        _processLogService.Info(
            nameof(BancoViewModel),
            $"Avvio emissione scontrino. Documento={GetOperationalDocumentLabel(DocumentoLocaleCorrente)}, Stato={DocumentoLocaleCorrente.Stato}, Righe={DocumentoLocaleCorrente.Righe.Count}, Totale={TotaleDocumento:N2}, Sconto={TotaleSconto:N2}, Pagato={TotalePagato:N2}, Residuo={Residuo:N2}, Carta={CartaPagamento:N2}, Contanti={ContantiPagamento:N2}, Sospeso={SospesoPagamento:N2}, Ristampa={confermaRistampa}.");

        if (RichiedeConfermaRistampa && !confermaRistampa)
        {
            StatusMessage = "Ristampa richiesta: confermare prima di proseguire.";
            _processLogService.Warning(nameof(BancoViewModel), $"Ristampa richiesta per il documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
            return;
        }

        if (RichiedeConfermaRistampa && confermaRistampa)
        {
            StatusMessage = "Ristampa fiscale confermata, ma il comando di ristampa sicura di WinEcr non e` ancora implementato.";
            _processLogService.Warning(nameof(BancoViewModel), $"Ristampa fiscale confermata per il documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}, ma il comando sicuro non e` ancora implementato.");
            return;
        }

        if (DocumentoLocaleCorrente.Righe.Count == 0)
        {
            StatusMessage = "Inserire almeno una riga prima di emettere lo scontrino.";
            _processLogService.Warning(nameof(BancoViewModel), $"Emissione scontrino bloccata: nessuna riga sul documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
            return;
        }

        if (Residuo > 0)
        {
            StatusMessage = $"Pagamenti incompleti. Residuo da coprire: {Residuo:N2} €.";
            _processLogService.Warning(nameof(BancoViewModel), $"Emissione scontrino bloccata per residuo {Residuo:N2} sul documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.");
            return;
        }

        _isPaymentOperationInProgress = true;
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
        ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
        ScontrinoCommand.RaiseCanExecuteChanged();
        ShowOperationPopup("Esecuzione scontrino", "Preparazione operazione in corso...", true, false);

        try
        {
            await RefreshPreviewDocumentNumberAsync();
            var posOutcomeMessage = string.Empty;

            while (CartaPagamento > 0)
            {
                StatoDocumento = "Pagamento POS in corso";
                StatusMessage = $"Invio di {CartaPagamento:N2} € al POS Nexi in corso...";
                using var posPaymentCts = new CancellationTokenSource();
                _posPaymentCts = posPaymentCts;
                ShowOperationPopup("Pagamento POS", $"Invio di {CartaPagamento:N2} € al terminale...", true, false, true);
                _processLogService.Info(nameof(BancoViewModel), $"Invio pagamento POS per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: importo {CartaPagamento:N2} €.");

                var posResult = await _posPaymentService.ExecutePaymentAsync(CartaPagamento, posPaymentCts.Token);
                _posPaymentCts = null;
                if (!posResult.IsSuccess)
                {
                    StatusMessage = posResult.Message;
                    _processLogService.Warning(nameof(BancoViewModel), $"Pagamento POS non autorizzato per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {posResult.Message}");

                    if (posResult.IsCancelledByUser)
                    {
                        StatoDocumento = "Pagamento POS annullato";
                        await ShowFinalOperationPopupAsync("Pagamento annullato", posResult.Message, false);
                        return;
                    }

                    if (posResult.RequiresManualInterventionWarning)
                    {
                        HideOperationPopup();
                        var warningChoice = PosManualWarningRequested is null
                            ? PosManualWarningChoice.TornaScheda
                            : await PosManualWarningRequested(posResult);

                        StatoDocumento = "Pagamento POS da verificare";
                        StatusMessage = warningChoice == PosManualWarningChoice.StampaManuale
                            ? "Pagamento POS da verificare manualmente. Se il terminale mostra la transazione accettata, non ripetere il pagamento: procedi con stampa manuale del registratore."
                            : "Pagamento POS da verificare. Controlla il terminale prima di rilanciare l'incasso dalla scheda Banco.";
                        return;
                    }

                    if (CanRetryPosPaymentImmediately(posResult))
                    {
                        StatoDocumento = "Pagamento POS da riprovare";
                        var retryRequested = await ShowRetryableOperationPopupAsync("Pagamento non completato", posResult.Message);
                        if (retryRequested)
                        {
                            continue;
                        }

                        StatoDocumento = "Pagamento POS non autorizzato";
                        return;
                    }

                    StatoDocumento = "Pagamento POS non autorizzato";
                    await ShowFinalOperationPopupAsync("Pagamento non completato", posResult.Message, false);
                    return;
                }

                posOutcomeMessage = posResult.Message;
                StatusMessage = posResult.Message;
                ShowOperationPopup("Pagamento POS", posResult.Message, false, true, false);
                _processLogService.Info(nameof(BancoViewModel), $"Pagamento POS autorizzato per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}: {posResult.Message}");
                break;
            }

            DocumentoLocaleCorrente.SegnaInChiusura();

            ShowOperationPopup("Scontrino", "Pubblicazione documento ufficiale nel legacy in corso...", true, false);
            var fiscalizationResult = await _bancoDocumentWorkflowService.PublishAsync(
                DocumentoLocaleCorrente,
                CategoriaDocumentoBanco.Scontrino);

            ApplyPublishSuccessUiState(fiscalizationResult, CategoriaDocumentoBanco.Scontrino, "Scontrino", posOutcomeMessage);
            _processLogService.Info(
                nameof(BancoViewModel),
                fiscalizationResult.WinEcrExecuted
                    ? $"Documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)} fiscalizzato su db_diltech con OID {fiscalizationResult.DocumentoGestionaleOid}."
                    : $"Documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)} pubblicato su db_diltech con OID {fiscalizationResult.DocumentoGestionaleOid}, ma WinEcr non ha completato la fiscalizzazione.");
            if (fiscalizationResult.WinEcrExecuted)
            {
                await ResetCurrentTabToNewDocumentAsync(
                    GetPublishPopupTitle(fiscalizationResult),
                    StatusMessage,
                    IsPublishPopupSuccess(fiscalizationResult));
            }
            else
            {
                await ShowFinalOperationPopupAsync("Fiscalizzazione non completata", StatusMessage, false);
            }
        }
        catch (Exception ex)
        {
            if (DocumentoLocaleCorrente is not null)
            {
                DocumentoLocaleCorrente.Riapri();
            }

            ClearTransientPublishedUiStateAfterFailure();
            StatoDocumento = "Errore emissione scontrino";
            StatusMessage = $"Errore durante il flusso scontrino: {ex.Message}";
            _processLogService.Error(nameof(BancoViewModel), $"Errore durante il flusso scontrino per documento {GetOperationalDocumentLabel(DocumentoLocaleCorrente)}.", ex);
            RefreshPublishedDocumentUiState();
            await ShowFinalOperationPopupAsync("Operazione non completata", StatusMessage, false);
        }
        finally
        {
            _posPaymentCts?.Dispose();
            _posPaymentCts = null;
            _isPaymentOperationInProgress = false;
            ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
            ScontrinoCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanEmettiScontrino));
            NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(RichiedeConfermaRistampa));
        }
    }

    private void LoadPaymentInputsFromDocument()
    {
        if (DocumentoLocaleCorrente is null)
        {
            ResetPaymentInputs();
            return;
        }

        var pagamenti = DocumentoLocaleCorrente.Pagamenti;
        SetPaymentInputs(
            DocumentoLocaleCorrente.ScontoDocumento,
            SommaPagamenti(pagamenti, "contanti", "contante"),
            SommaPagamenti(pagamenti, "bancomat", "carta", "pos"),
            SommaPagamenti(pagamenti, "sospeso"),
            SommaPagamenti(pagamenti, "buoni", "buonipasto", "ticket"));
    }

    private void ResetPaymentInputs()
    {
        _lastAutoAssignedPaymentType = null;
        SetPaymentInputs(0, 0, 0, 0, 0);
    }

    private void SetPaymentInputs(decimal sconto, decimal contanti, decimal carta, decimal sospeso, decimal buoni)
    {
        _isUpdatingPaymentInputs = true;
        try
        {
            ScontoPagamentoText = FormatCurrencyInput(sconto);
            ContantiPagamentoText = FormatCurrencyInput(contanti);
            CartaPagamentoText = FormatCurrencyInput(carta);
            SospesoPagamentoText = FormatCurrencyInput(sospeso);
            BuoniPagamentoText = FormatCurrencyInput(buoni);
        }
        finally
        {
            _isUpdatingPaymentInputs = false;
        }

        _paymentInputsDirty = false;
        NotifyTotalsChanged();
    }

    private void SetPagamentoText(string tipoPagamento, decimal importo)
    {
        var formatted = FormatCurrencyInput(importo);
        switch (tipoPagamento)
        {
            case "sconto":
                ScontoPagamentoText = formatted;
                break;
            case "contanti":
                ContantiPagamentoText = formatted;
                break;
            case "carta":
            case "bancomat":
                CartaPagamentoText = formatted;
                break;
            case "sospeso":
                SospesoPagamentoText = formatted;
                break;
            case "buoni":
                BuoniPagamentoText = formatted;
                break;
        }
    }

    private string GetPagamentoText(string tipoPagamento)
    {
        return tipoPagamento switch
        {
            "sconto" => ScontoPagamentoText,
            "contanti" => ContantiPagamentoText,
            "carta" or "bancomat" => CartaPagamentoText,
            "sospeso" => SospesoPagamentoText,
            "buoni" => BuoniPagamentoText,
            _ => string.Empty
        };
    }

    private void ClearPagamentoText(string? tipoPagamento)
    {
        if (string.IsNullOrWhiteSpace(tipoPagamento))
        {
            return;
        }

        switch (tipoPagamento.Trim().ToLowerInvariant())
        {
            case "sconto":
                ScontoPagamentoText = FormatCurrencyInput(0);
                break;
            case "contanti":
                ContantiPagamentoText = FormatCurrencyInput(0);
                break;
            case "carta":
            case "bancomat":
                CartaPagamentoText = FormatCurrencyInput(0);
                break;
            case "sospeso":
                SospesoPagamentoText = FormatCurrencyInput(0);
                break;
            case "buoni":
                BuoniPagamentoText = FormatCurrencyInput(0);
                break;
        }
    }

    private decimal CalcolaImportoResiduoPerPagamento(string tipoPagamento)
    {
        var sconto = ScontoPagamento;
        var altriPagamenti = tipoPagamento switch
        {
            "contanti" => CartaPagamento + SospesoPagamento + BuoniPagamento,
            "carta" => ContantiPagamento + SospesoPagamento + BuoniPagamento,
            "bancomat" => ContantiPagamento + SospesoPagamento + BuoniPagamento,
            "sospeso" => ContantiPagamento + CartaPagamento + BuoniPagamento,
            "buoni" => ContantiPagamento + CartaPagamento + SospesoPagamento,
            _ => ContantiPagamento + CartaPagamento + SospesoPagamento + BuoniPagamento
        };

        return Math.Max(0, TotaleDocumento - sconto - altriPagamenti);
    }

    private decimal CalcolaImportoResiduoPerSconto()
    {
        var pagamentiNetti = ContantiPagamento + CartaPagamento + SospesoPagamento + BuoniPagamento;
        return Math.Max(0, TotaleDocumento - pagamentiNetti);
    }

    private decimal ScontoPagamento => ParseCurrencyInput(ScontoPagamentoText);

    private decimal ContantiPagamento => ParseCurrencyInput(ContantiPagamentoText);

    private decimal CartaPagamento => ParseCurrencyInput(CartaPagamentoText);

    private decimal SospesoPagamento => ParseCurrencyInput(SospesoPagamentoText);

    private decimal BuoniPagamento => ParseCurrencyInput(BuoniPagamentoText);

    private static decimal SommaPagamenti(IEnumerable<PagamentoLocale> pagamenti, params string[] tipi)
    {
        return pagamenti
            .Where(pagamento => tipi.Any(tipo =>
                string.Equals(pagamento.TipoPagamento?.Trim(), tipo, StringComparison.OrdinalIgnoreCase)))
            .Sum(pagamento => pagamento.Importo);
    }

    private static string FormatCurrencyInput(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static decimal ParseCurrencyInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var normalized = value.Replace("€", string.Empty).Trim();
        var invariantCandidate = NormalizeDecimalInput(normalized);

        if (!string.IsNullOrWhiteSpace(invariantCandidate) &&
            decimal.TryParse(invariantCandidate, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedInvariant))
        {
            return Math.Max(0, parsedInvariant);
        }

        return 0;
    }

    private static string NormalizeDecimalInput(string value)
    {
        var compact = value.Replace(" ", string.Empty);
        var hasComma = compact.Contains(',');
        var hasDot = compact.Contains('.');

        if (hasComma && hasDot)
        {
            var lastComma = compact.LastIndexOf(',');
            var lastDot = compact.LastIndexOf('.');

            if (lastComma > lastDot)
            {
                return compact.Replace(".", string.Empty).Replace(',', '.');
            }

            return compact.Replace(",", string.Empty);
        }

        if (hasComma)
        {
            return compact.Replace(',', '.');
        }

        return compact;
    }

    private Task PrepareDefaultCustomerLookupAsync()
    {
        ClienteSelezionato = null;
        IsClienteConfermato = false;
        IsCustomerPopupOpen = false;
        SetCustomerSearchText("Cliente generico");
        return Task.CompletedTask;
    }

    private void ApriStoricoAcquisti()
    {
        if (_clienteSelezionato is null ||
            _clienteSelezionato.Oid <= 0 ||
            !_isClienteConfermato ||
            _clienteSelezionato.IsClienteGenerico)
        {
            StatusMessage = "Seleziona e conferma un cliente reale prima di aprire lo storico acquisti.";
            return;
        }

        var vm = new StoricoAcquistiViewModel(_documentReadService)
        {
            SoggettoOid = _clienteSelezionato.Oid,
            ClienteNominativo = _clienteSelezionato.DisplayLabel
        };

        OpenStoricoAcquistiRequested?.Invoke(vm);
    }

    public void ApriStoricoAcquistiArticoloDaTastiera()
    {
        ApriStoricoAcquistiArticolo(PurchaseHistoryOpenMode.ArticleContext);
    }

    private void ApriStoricoAcquistiArticoloDaPulsante()
    {
        ApriStoricoAcquistiArticolo(PurchaseHistoryOpenMode.FreeSearch);
    }

    private void ApriStoricoAcquistiArticolo(PurchaseHistoryOpenMode modalita)
    {
        var vm = new PurchaseHistoryViewModel(_documentReadService, _articleReadService, _customerReadService);

        if (modalita == PurchaseHistoryOpenMode.ArticleContext)
        {
            var articolo = BuildSelectedRowArticleContext();
            if (articolo is null)
            {
                StatusMessage = "Seleziona una riga articolo reale prima di aprire lo storico acquisti con F1.";
                return;
            }

            vm.InitializeForArticleContext(articolo);
        }
        else
        {
            vm.InitializeForFreeSearch();
        }

        OpenPurchaseHistoryRequested?.Invoke(vm);
    }

    private async Task LoadClienteCorrenteAsync(int soggettoOid)
    {
        if (soggettoOid <= 0)
        {
            await PrepareDefaultCustomerLookupAsync();
            return;
        }

        try
        {
            var cliente = await _customerReadService.GetCustomerByOidAsync(soggettoOid);
            if (cliente is null)
            {
                await PrepareDefaultCustomerLookupAsync();
                return;
            }

            ApplySelectedCustomer(cliente, markDocumentDirty: false);
        }
        catch
        {
            await PrepareDefaultCustomerLookupAsync();
        }
    }

    private async Task LoadClienteCorrenteFromLabelAsync(string? clienteLabel)
    {
        if (string.IsNullOrWhiteSpace(clienteLabel))
        {
            await PrepareDefaultCustomerLookupAsync();
            return;
        }

        var match = System.Text.RegularExpressions.Regex.Match(clienteLabel, @"^\s*(\d+)\s*-");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var soggettoOid))
        {
            await LoadClienteCorrenteAsync(soggettoOid);
            return;
        }

        SetCustomerSearchText(clienteLabel);
        await CercaClientiAsync();

        var matchingCustomer = RisultatiRicercaClienti.FirstOrDefault(cliente =>
            cliente.DisplayLabel.Equals(clienteLabel, StringComparison.OrdinalIgnoreCase) ||
            cliente.DisplayLabel.Contains(clienteLabel, StringComparison.OrdinalIgnoreCase));

        if (matchingCustomer is not null)
        {
            ApplySelectedCustomer(matchingCustomer, markDocumentDirty: false);
            return;
        }

        ClienteSelezionato = null;
        IsClienteConfermato = false;
        NotifyPropertyChanged(nameof(PuntiClienteCorrente));
        NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
        NotifyPropertyChanged(nameof(CustomerInfoLineText));
        IsCustomerPopupOpen = false;
        SetCustomerSearchText(string.Empty);
    }

    private async Task LoadOperatorsAsync()
    {
        try
        {
            var operators = await _operatorReadService.GetOperatorsAsync();
            OperatoriDisponibili.Clear();

            foreach (var operatore in operators)
            {
                OperatoriDisponibili.Add(operatore);
            }

            if (OperatoriDisponibili.Count == 0)
            {
                var fallbackOperatore = CreateFallbackOperatore();
                OperatoriDisponibili.Add(fallbackOperatore);
                OperatoreSelezionato = fallbackOperatore;
            }
            else if (OperatoreSelezionato is null)
            {
                OperatoreSelezionato = OperatoriDisponibili.FirstOrDefault(item => item.Matches("Admin")) ??
                    OperatoriDisponibili.FirstOrDefault() ??
                    CreateFallbackOperatore();
            }
        }
        catch
        {
            OperatoriDisponibili.Clear();
            var fallbackOperatore = CreateFallbackOperatore();
            OperatoriDisponibili.Add(fallbackOperatore);
            OperatoreSelezionato = fallbackOperatore;
        }
    }

    private async Task LoadPriceListsAsync(int? preferredPriceListOid = null, bool repriceDocument = false, bool markDocumentDirty = false)
    {
        try
        {
            var priceLists = await _priceListReadService.GetSalesPriceListsAsync();
            ListiniDisponibili.Clear();

            foreach (var priceList in priceLists)
            {
                ListiniDisponibili.Add(priceList);
            }

            var selectedPriceList = ResolvePreferredPriceList(preferredPriceListOid);
            await SetSelectedPriceListAsync(selectedPriceList, repriceDocument, markDocumentDirty, updateStatusMessage: false);
            NotifyPropertyChanged(nameof(CanSelectListino));
        }
        catch
        {
            ListiniDisponibili.Clear();
            NotifyPropertyChanged(nameof(CanSelectListino));
        }
    }

    private GestionalePriceListSummary? ResolvePreferredPriceList(int? preferredPriceListOid = null)
    {
        if (preferredPriceListOid.HasValue)
        {
            var explicitMatch = ListiniDisponibili.FirstOrDefault(item => item.Oid == preferredPriceListOid.Value);
            if (explicitMatch is not null)
            {
                return explicitMatch;
            }
        }

        if (ClienteSelezionato?.ClienteListinoOid is int customerPriceListOid)
        {
            var customerMatch = ListiniDisponibili.FirstOrDefault(item => item.Oid == customerPriceListOid);
            if (customerMatch is not null)
            {
                return customerMatch;
            }
        }

        if (ListinoSelezionato is not null)
        {
            var currentMatch = ListiniDisponibili.FirstOrDefault(item => item.Oid == ListinoSelezionato.Oid);
            if (currentMatch is not null)
            {
                return currentMatch;
            }
        }

        return ListiniDisponibili.FirstOrDefault(item => item.IsWeb)
            ?? ListiniDisponibili.FirstOrDefault(item => item.IsDefault)
            ?? ListiniDisponibili.FirstOrDefault();
    }

    private async Task SetSelectedPriceListAsync(
        GestionalePriceListSummary? priceList,
        bool repriceDocument,
        bool markDocumentDirty,
        bool updateStatusMessage)
    {
        _isApplyingSelectedPriceList = true;
        try
        {
            ListinoSelezionato = priceList;
        }
        finally
        {
            _isApplyingSelectedPriceList = false;
        }

        if (DocumentoLocaleCorrente is not null)
        {
            DocumentoLocaleCorrente.ImpostaListino(priceList?.Oid, priceList?.DisplayLabel);
        }

        if (repriceDocument)
        {
            await RepriceDocumentRowsAsync(markDocumentDirty);
        }

        if (updateStatusMessage && priceList is not null)
        {
            StatoDocumento = "Listino aggiornato";
            StatusMessage = $"Vendita riallineata al listino {priceList.DisplayLabel}.";
        }
    }

    private async Task HandleSelectedPriceListChangedAsync(
        GestionalePriceListSummary? selectedPriceList,
        bool markDocumentDirty,
        bool updateStatusMessage)
    {
        if (_isApplyingSelectedPriceList)
        {
            return;
        }

        if (DocumentoLocaleCorrente is not null)
        {
            DocumentoLocaleCorrente.ImpostaListino(selectedPriceList?.Oid, selectedPriceList?.DisplayLabel);
        }

        await RepriceDocumentRowsAsync(markDocumentDirty);

        if (updateStatusMessage && selectedPriceList is not null)
        {
            StatoDocumento = "Listino aggiornato";
            StatusMessage = $"Vendita riallineata al listino {selectedPriceList.DisplayLabel}.";
        }
    }

    private async Task RepriceDocumentRowsAsync(bool markDocumentDirty)
    {
        if (DocumentoLocaleCorrente is null)
        {
            return;
        }

        _articlePricingDetails.Clear();
        var hasPricingChanges = false;

        foreach (var riga in DocumentoLocaleCorrente.Righe)
        {
            var prezzoPrecedente = riga.PrezzoUnitario;
            var quantitaPrecedente = riga.Quantita;
            await EnsureArticlePricingAppliedAsync(riga);
            if (prezzoPrecedente != riga.PrezzoUnitario || quantitaPrecedente != riga.Quantita)
            {
                hasPricingChanges = true;
            }
        }

        if (hasPricingChanges)
        {
            RefreshRows();
            NotifyTotalsChanged();
            NotifySelectedRowStateChanged();
            if (markDocumentDirty)
            {
                HasPendingLocalChanges = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchArticoloText))
        {
            await CercaArticoliAsync();
        }
    }

    private void ApplySelectedCustomer(GestionaleCustomerSummary cliente, bool markDocumentDirty, bool refreshPromoState = true)
    {
        ClienteSelezionato = cliente;
        SetCustomerSearchText(BuildCustomerSearchLabel(cliente));

        if (DocumentoLocaleCorrente is not null)
        {
            DocumentoLocaleCorrente.ImpostaCliente(cliente.Oid, cliente.DisplayLabel);
            NotifyPropertyChanged(nameof(ClienteCorrente));
            NotifyPropertyChanged(nameof(ClienteCorrenteDisplay));
        }

        NotifyPropertyChanged(nameof(PuntiClienteCorrente));
        NotifyPropertyChanged(nameof(HasPuntiClienteCorrente));
        NotifyPropertyChanged(nameof(CustomerInfoLineText));
        IsClienteConfermato = true;
        IsCustomerPopupOpen = false;
        HasPendingLocalChanges = markDocumentDirty;
        _ = AutoSelectCustomerPriceListAsync(cliente, markDocumentDirty);
        if (refreshPromoState)
        {
            SchedulePromoRefresh();
        }
    }

    private async Task AutoSelectCustomerPriceListAsync(GestionaleCustomerSummary cliente, bool markDocumentDirty)
    {
        if (ListiniDisponibili.Count == 0)
        {
            await LoadPriceListsAsync(cliente.ClienteListinoOid, repriceDocument: false, markDocumentDirty: false);
        }

        var selectedPriceList = ResolvePreferredPriceList(cliente.ClienteListinoOid);
        if (selectedPriceList is null ||
            (ListinoSelezionato is not null && ListinoSelezionato.Oid == selectedPriceList.Oid))
        {
            return;
        }

        await SetSelectedPriceListAsync(selectedPriceList, repriceDocument: true, markDocumentDirty, updateStatusMessage: false);
        StatoDocumento = "Cliente aggiornato";
        StatusMessage = cliente.ClienteListinoOid.HasValue
            ? $"Cliente documento impostato su {cliente.DisplayName}. Listino {selectedPriceList.DisplayLabel} selezionato automaticamente."
            : $"Cliente documento impostato su {cliente.DisplayName}.";
    }

    private void SetCustomerSearchText(string value)
    {
        _isUpdatingSearchClienteText = true;
        SearchClienteText = value;
        _isUpdatingSearchClienteText = false;
    }

    private static string BuildCustomerSearchLabel(GestionaleCustomerSummary cliente)
    {
        return string.IsNullOrWhiteSpace(cliente.DisplayName)
            ? cliente.DisplayLabel
            : cliente.DisplayName;
    }

    private static string NormalizeCustomerSelectionToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", " ");
        compact = compact.Replace("-", " ");
        return Regex.Replace(compact, @"\s+", " ").Trim();
    }

    public Task RefreshPromoConfigurationAsync(bool triggerPopupIfNeeded = false)
    {
        return LoadPromoConfigurationAsync(refreshSelectedCustomer: true, triggerPopupIfNeeded);
    }

    private Task LoadPromoCampaignAsync()
    {
        return LoadPromoConfigurationAsync(refreshSelectedCustomer: false, triggerPopupIfNeeded: false);
    }

    private async Task LoadPromoConfigurationAsync(bool refreshSelectedCustomer, bool triggerPopupIfNeeded)
    {
        try
        {
            var campaigns = await _pointsReadService.GetCampaignsAsync();
            _promoCampaignSummary = campaigns.FirstOrDefault(c => c.Attiva == true) ?? campaigns.FirstOrDefault();
            _promoRewardRules = _promoCampaignSummary is null
                ? []
                : await _rewardRuleService.GetAsync(_promoCampaignSummary.Oid);
        }
        catch
        {
            _promoCampaignSummary = null;
            _promoRewardRules = [];
        }

        if (refreshSelectedCustomer && ClienteSelezionato?.Oid > 0)
        {
            try
            {
                var refreshedCustomer = await _customerReadService.GetCustomerByOidAsync(ClienteSelezionato.Oid);
                if (refreshedCustomer is not null)
                {
                    ApplySelectedCustomer(refreshedCustomer, markDocumentDirty: false, refreshPromoState: false);
                }
            }
            catch
            {
                // Se il refresh del cliente fallisce manteniamo il dato corrente e continuiamo.
            }
        }

        await RefreshPromoStateAsync(triggerPopupIfNeeded);
    }

    private async Task RefreshPromoStateAsync(bool triggerPopupIfNeeded = true)
    {
        var document = DocumentoLocaleCorrente;
        var requestVersion = ++_promoRefreshRequestVersion;
        var expectedDocumentId = document?.Id;
        var expectedCustomerOid = ClienteSelezionato?.Oid;
        var rewardAlreadyApplied = document?.Righe.Any(riga =>
            riga.IsPromoRow &&
            riga.PromoCampaignOid == _promoCampaignSummary?.Oid) == true;

        if (document is null || ClienteSelezionato is null || !HasClienteRaccoltaPunti)
        {
            PromoEligibility = new PromotionEvaluationResult
            {
                EventType = PromotionEventType.NotEligible,
                Title = ClienteSelezionato is null
                    ? "Seleziona un cliente"
                    : "Raccolta punti non attiva",
                Message = ClienteSelezionato is null
                    ? "Conferma un cliente per valutare la regola premio."
                    : "Questo cliente non ha assegnazione alla raccolta punti."
            };
            return;
        }

        PromotionEventType? lastEventType = null;
        Guid? lastEventRuleId = null;
        decimal? lastEventAvailablePoints = null;
        decimal? lastEventRequiredPoints = null;
        if (document is not null)
        {
            var lastEvent = await _promotionHistoryService.GetLastDocumentEventAsync(document.Id);
            if (!IsPromoRefreshRequestCurrent(requestVersion, expectedDocumentId, expectedCustomerOid))
            {
                return;
            }

            lastEventType = lastEvent?.EventType;
            lastEventRuleId = lastEvent?.RuleId;
            lastEventAvailablePoints = lastEvent?.AvailablePoints;
            lastEventRequiredPoints = lastEvent?.RequiredPoints;
        }

        var evaluation = _promotionEvaluationService.Evaluate(new PromotionEvaluationContext
        {
            Customer = ClienteSelezionato,
            Campaign = _promoCampaignSummary,
            RewardRules = _promoRewardRules,
            Document = document,
            RewardAlreadyApplied = rewardAlreadyApplied,
            LastEventType = lastEventType,
            LastEventRuleId = lastEventRuleId,
            LastEventAvailablePoints = lastEventAvailablePoints,
            LastEventRequiredPoints = lastEventRequiredPoints
        });

        PromoEligibility = evaluation;

        if (document is null || ClienteSelezionato is null || _promoCampaignSummary is null)
        {
            return;
        }

        if (!IsPromoRefreshRequestCurrent(requestVersion, expectedDocumentId, expectedCustomerOid))
        {
            return;
        }

        if (evaluation.EventType == PromotionEventType.NotEligible &&
            lastEventType != PromotionEventType.NotEligible &&
            !rewardAlreadyApplied)
        {
            await _promotionHistoryService.RecordAsync(new PromotionEventRecord
            {
                CampaignOid = _promoCampaignSummary.Oid,
                RuleId = evaluation.RewardRule?.Id,
                CustomerOid = ClienteSelezionato.Oid,
                LocalDocumentId = document.Id,
                EventType = PromotionEventType.NotEligible,
                RewardType = evaluation.RewardRule?.RewardType,
                AvailablePoints = evaluation.Summary.TotalAvailablePoints,
                RequiredPoints = evaluation.Summary.RequiredPoints,
                Title = evaluation.Title,
                Message = evaluation.Message
            });
        }

        if (rewardAlreadyApplied && evaluation.EventType == PromotionEventType.NotEligible)
        {
            var reversed = _promotionDocumentService.ReverseReward(document, evaluation);
            if (reversed is not null)
            {
                await _promotionHistoryService.RecordAsync(reversed);
                RefreshRows();
            }

            return;
        }

        if (!triggerPopupIfNeeded || !evaluation.ShouldShowPopup || PromotionConfirmationRequested is null)
        {
            return;
        }

        var eligibleEvent = new PromotionEventRecord
        {
            CampaignOid = _promoCampaignSummary.Oid,
            RuleId = evaluation.RewardRule?.Id,
            CustomerOid = ClienteSelezionato.Oid,
            LocalDocumentId = document.Id,
            EventType = PromotionEventType.Eligible,
            RewardType = evaluation.RewardRule?.RewardType,
            AvailablePoints = evaluation.Summary.TotalAvailablePoints,
            RequiredPoints = evaluation.Summary.RequiredPoints,
            Title = evaluation.Title,
            Message = evaluation.Message
        };
        await _promotionHistoryService.RecordAsync(eligibleEvent);

        var selectedRewardRule = await PromotionConfirmationRequested.Invoke(evaluation);
        if (!IsPromoRefreshRequestCurrent(requestVersion, expectedDocumentId, expectedCustomerOid))
        {
            return;
        }

        if (selectedRewardRule is not null)
        {
            var applied = _promotionDocumentService.ApplyReward(document, ClienteSelezionato.Oid, _promoCampaignSummary, selectedRewardRule, evaluation);
            await _promotionHistoryService.RecordAsync(applied);
            RefreshRows();
            StatusMessage = applied.Message;
            return;
        }

        await _promotionHistoryService.RecordAsync(new PromotionEventRecord
        {
            CampaignOid = _promoCampaignSummary.Oid,
            RuleId = evaluation.RewardRule?.Id,
            CustomerOid = ClienteSelezionato.Oid,
            LocalDocumentId = document.Id,
            EventType = PromotionEventType.Rejected,
            RewardType = evaluation.RewardRule?.RewardType,
            AvailablePoints = evaluation.Summary.TotalAvailablePoints,
            RequiredPoints = evaluation.Summary.RequiredPoints,
            Title = "Premio non applicato",
            Message = "L'operatore ha scelto di non applicare il premio sul documento."
        });
    }

    private void SchedulePromoRefresh(bool triggerPopupIfNeeded = true)
    {
        _promoRefreshCts?.Cancel();

        var cts = new CancellationTokenSource();
        _promoRefreshCts = cts;
        var requestVersion = ++_promoRefreshRequestVersion;
        var expectedDocumentId = DocumentoLocaleCorrente?.Id;
        var expectedCustomerOid = ClienteSelezionato?.Oid;
        _ = SchedulePromoRefreshCoreAsync(cts.Token, requestVersion, expectedDocumentId, expectedCustomerOid, triggerPopupIfNeeded);
    }

    private async Task SchedulePromoRefreshCoreAsync(
        CancellationToken cancellationToken,
        int requestVersion,
        Guid? expectedDocumentId,
        int? expectedCustomerOid,
        bool triggerPopupIfNeeded)
    {
        try
        {
            await Task.Delay(150, cancellationToken);
            if (cancellationToken.IsCancellationRequested ||
                !IsPromoRefreshRequestCurrent(requestVersion, expectedDocumentId, expectedCustomerOid))
            {
                return;
            }

            await RefreshPromoStateAsync(triggerPopupIfNeeded);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private bool IsPromoRefreshRequestCurrent(int requestVersion, Guid? expectedDocumentId, int? expectedCustomerOid)
    {
        return requestVersion == _promoRefreshRequestVersion &&
               DocumentoLocaleCorrente?.Id == expectedDocumentId &&
               ClienteSelezionato?.Oid == expectedCustomerOid;
    }

    private void SyncOperatoreSelezionato(string? operatore)
    {
        var nomeOperatore = string.IsNullOrWhiteSpace(operatore)
            ? CreateFallbackOperatore().Nome
            : operatore.Trim();

        var existing = OperatoriDisponibili.FirstOrDefault(item => item.Matches(nomeOperatore));

        if (existing is null)
        {
            existing = new GestionaleOperatorSummary
            {
                Nome = nomeOperatore,
                MatchTokens = new[] { nomeOperatore }
            };
            OperatoriDisponibili.Insert(0, existing);
        }

        OperatoreSelezionato = existing;
    }

    private static GestionaleOperatorSummary CreateFallbackOperatore()
    {
        return new GestionaleOperatorSummary
        {
            Nome = "Admin Banco"
        };
    }

    public async Task SaveColumnWidthAsync(string columnKey, double width)
    {
        if (width <= 0)
        {
            return;
        }

        switch (columnKey)
        {
            case "Riga": RigaColumnWidth = width; break;
            case "Codice": CodiceColumnWidth = width; break;
            case "Descrizione": DescrizioneColumnWidth = width; break;
            case "Quantita": QuantitaColumnWidth = width; break;
            case "Disponibilita": break;
            case "Prezzo": PrezzoColumnWidth = width; break;
            case "Sconto": ScontoColumnWidth = width; break;
            case "Importo": ImportoColumnWidth = width; break;
            case "Iva": IvaColumnWidth = width; break;
            case "UnitaMisura": TipoColumnWidth = width; break;
            case "TipoRiga": TipoRigaColumnWidth = width; break;
            case "Azioni": AzioniColumnWidth = width; break;
            default: return;
        }

        await PersistGridLayoutAsync();
    }

    public Task EnsureLayoutInitializedAsync() => _layoutInitializationTask;

    public async Task SetColumnVisibilityAsync(string columnKey, bool visible)
    {
        _columnVisibility[columnKey] = visible;
        RaiseColumnVisibilityNotifications();
        await PersistGridLayoutAsync();
    }

    public async Task SaveColumnDisplayIndexAsync(string columnKey, int displayIndex)
    {
        switch (columnKey)
        {
            case "Riga": _rigaDisplayIndex = displayIndex; break;
            case "Codice": _codiceDisplayIndex = displayIndex; break;
            case "Descrizione": _descrizioneDisplayIndex = displayIndex; break;
            case "Quantita": _quantitaDisplayIndex = displayIndex; break;
            case "Prezzo": _prezzoDisplayIndex = displayIndex; break;
            case "Iva": _ivaDisplayIndex = displayIndex; break;
            case "UnitaMisura": _tipoDisplayIndex = displayIndex; break;
            case "Sconto": _scontoDisplayIndex = displayIndex; break;
            case "Importo": _importoDisplayIndex = displayIndex; break;
            case "TipoRiga": _tipoRigaDisplayIndex = displayIndex; break;
            case "Azioni": _azioniDisplayIndex = displayIndex; break;
            default: return;
        }

        await PersistGridLayoutAsync();
    }

    public int GetColumnDisplayIndex(string columnKey)
    {
        return columnKey switch
        {
            "Riga" => _rigaDisplayIndex,
            "Codice" => _codiceDisplayIndex,
            "Descrizione" => _descrizioneDisplayIndex,
            "Quantita" => _quantitaDisplayIndex,
            "Disponibilita" => 4,
            "Prezzo" => _prezzoDisplayIndex,
            "Iva" => _ivaDisplayIndex,
            "UnitaMisura" => _tipoDisplayIndex,
            "Sconto" => _scontoDisplayIndex,
            "Importo" => _importoDisplayIndex,
            "TipoRiga" => _tipoRigaDisplayIndex,
            "Azioni" => _azioniDisplayIndex,
            _ => 0
        };
    }

    private async Task InitializeLayoutAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = settings.BancoDocumentGridLayout;

        RigaColumnWidth = layout.RigaWidth;
        CodiceColumnWidth = layout.CodiceWidth;
        DescrizioneColumnWidth = layout.DescrizioneWidth;
        QuantitaColumnWidth = layout.QuantitaWidth;
        PrezzoColumnWidth = layout.PrezzoWidth;
        IvaColumnWidth = layout.IvaWidth;
        TipoColumnWidth = layout.UnitaMisuraWidth;
        TipoRigaColumnWidth = layout.TipoRigaWidth;
        ScontoColumnWidth = layout.ScontoWidth;
        ImportoColumnWidth = layout.ImportoWidth;
        AzioniColumnWidth = layout.AzioniWidth;

        _columnVisibility["Riga"] = layout.ShowRiga;
        _columnVisibility["Codice"] = layout.ShowCodice;
        _columnVisibility["Descrizione"] = layout.ShowDescrizione;
        _columnVisibility["Quantita"] = layout.ShowQuantita;
        _columnVisibility["Disponibilita"] = layout.ShowDisponibilita;
        _columnVisibility["Prezzo"] = layout.ShowPrezzo;
        _columnVisibility["Sconto"] = layout.ShowSconto;
        _columnVisibility["Importo"] = layout.ShowImporto;
        _columnVisibility["Iva"] = layout.ShowIva;
        _columnVisibility["UnitaMisura"] = layout.ShowUnitaMisura;
        _columnVisibility["TipoRiga"] = layout.ShowTipoRiga;
        _columnVisibility["Azioni"] = layout.ShowAzioni;
        _rigaDisplayIndex = layout.RigaDisplayIndex;
        _codiceDisplayIndex = layout.CodiceDisplayIndex;
        _descrizioneDisplayIndex = layout.DescrizioneDisplayIndex;
        _quantitaDisplayIndex = layout.QuantitaDisplayIndex;
        _prezzoDisplayIndex = layout.PrezzoDisplayIndex;
        _ivaDisplayIndex = layout.IvaDisplayIndex;
        _scontoDisplayIndex = layout.ScontoDisplayIndex;
        _importoDisplayIndex = layout.ImportoDisplayIndex;
        _tipoDisplayIndex = layout.UnitaMisuraDisplayIndex;
        _tipoRigaDisplayIndex = layout.TipoRigaDisplayIndex;
        _azioniDisplayIndex = layout.AzioniDisplayIndex;

        RaiseColumnVisibilityNotifications();
    }

    private async Task PersistGridLayoutAsync()
    {
        var settings = await _configurationService.LoadAsync();
        var layout = settings.BancoDocumentGridLayout;

        layout.RigaWidth = RigaColumnWidth;
        layout.CodiceWidth = CodiceColumnWidth;
        layout.DescrizioneWidth = DescrizioneColumnWidth;
        layout.QuantitaWidth = QuantitaColumnWidth;
        layout.DisponibilitaWidth = layout.DisponibilitaWidth <= 0 ? 70 : layout.DisponibilitaWidth;
        layout.PrezzoWidth = PrezzoColumnWidth;
        layout.ScontoWidth = ScontoColumnWidth;
        layout.ImportoWidth = ImportoColumnWidth;
        layout.IvaWidth = IvaColumnWidth;
        layout.UnitaMisuraWidth = TipoColumnWidth;
        layout.TipoRigaWidth = TipoRigaColumnWidth;
        layout.AzioniWidth = AzioniColumnWidth;
        layout.ShowRiga = ShowRigaColumn;
        layout.ShowCodice = ShowCodiceColumn;
        layout.ShowDescrizione = ShowDescrizioneColumn;
        layout.ShowQuantita = ShowQuantitaColumn;
        layout.ShowDisponibilita = ShowDisponibilitaColumn;
        layout.ShowPrezzo = ShowPrezzoColumn;
        layout.ShowSconto = ShowScontoColumn;
        layout.ShowImporto = ShowImportoColumn;
        layout.ShowIva = ShowIvaColumn;
        layout.ShowUnitaMisura = ShowUnitaMisuraColumn;
        layout.ShowTipoRiga = ShowTipoRigaColumn;
        layout.ShowAzioni = ShowAzioniColumn;
        layout.RigaDisplayIndex = _rigaDisplayIndex;
        layout.CodiceDisplayIndex = _codiceDisplayIndex;
        layout.DescrizioneDisplayIndex = _descrizioneDisplayIndex;
        layout.QuantitaDisplayIndex = _quantitaDisplayIndex;
        layout.PrezzoDisplayIndex = _prezzoDisplayIndex;
        layout.ScontoDisplayIndex = _scontoDisplayIndex;
        layout.ImportoDisplayIndex = _importoDisplayIndex;
        layout.IvaDisplayIndex = _ivaDisplayIndex;
        layout.UnitaMisuraDisplayIndex = _tipoDisplayIndex;
        layout.TipoRigaDisplayIndex = _tipoRigaDisplayIndex;
        layout.AzioniDisplayIndex = _azioniDisplayIndex;
        await _configurationService.SaveAsync(settings);
    }

    private void ScheduleArticleSearch()
    {
        _articleSearchCts?.Cancel();
        var searchText = SearchArticoloText;
        if (string.IsNullOrWhiteSpace(searchText) || IsReadOnly)
        {
            return;
        }

        var requestVersion = ++_articleSearchRequestVersion;
        var cts = new CancellationTokenSource();
        _articleSearchCts = cts;
        _ = ScheduleArticleSearchCoreAsync(cts.Token, requestVersion, searchText);
    }

    private async Task ScheduleArticleSearchCoreAsync(CancellationToken cancellationToken, int requestVersion, string searchText)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await CercaArticoliAsync(requestVersion, searchText);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void ScheduleCustomerSearch()
    {
        _customerSearchCts?.Cancel();
        var searchText = SearchClienteText;
        if (string.IsNullOrWhiteSpace(searchText) || IsReadOnly)
        {
            return;
        }

        var requestVersion = ++_customerSearchRequestVersion;
        var cts = new CancellationTokenSource();
        _customerSearchCts = cts;
        _ = ScheduleCustomerSearchCoreAsync(cts.Token, requestVersion, searchText);
    }

    private async Task ScheduleCustomerSearchCoreAsync(CancellationToken cancellationToken, int requestVersion, string searchText)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await CercaClientiAsync(requestVersion, searchText);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private bool IsArticleSearchRequestCurrent(int requestVersion, string searchText)
    {
        return requestVersion == _articleSearchRequestVersion &&
               string.Equals(SearchArticoloText, searchText, StringComparison.Ordinal);
    }

    private bool IsCustomerSearchRequestCurrent(int requestVersion, string searchText)
    {
        return requestVersion == _customerSearchRequestVersion &&
               string.Equals(SearchClienteText, searchText, StringComparison.Ordinal);
    }

    private bool GetColumnVisibility(string key) => !_columnVisibility.TryGetValue(key, out var value) || value;

    private void RaiseColumnVisibilityNotifications()
    {
        NotifyPropertyChanged(nameof(ShowRigaColumn));
        NotifyPropertyChanged(nameof(ShowCodiceColumn));
        NotifyPropertyChanged(nameof(ShowDescrizioneColumn));
        NotifyPropertyChanged(nameof(ShowQuantitaColumn));
        NotifyPropertyChanged(nameof(ShowDisponibilitaColumn));
        NotifyPropertyChanged(nameof(ShowPrezzoColumn));
        NotifyPropertyChanged(nameof(ShowScontoColumn));
        NotifyPropertyChanged(nameof(ShowImportoColumn));
        NotifyPropertyChanged(nameof(ShowIvaColumn));
        NotifyPropertyChanged(nameof(ShowUnitaMisuraColumn));
        NotifyPropertyChanged(nameof(ShowTipoRigaColumn));
        NotifyPropertyChanged(nameof(ShowAzioniColumn));
    }

    private void InvalidatePendingArticleSearch(bool clearSearchText)
    {
        _articleSearchCts?.Cancel();
        _articleSearchCts = null;
        _articleSearchRequestVersion++;
        IsArticlePopupOpen = false;

        if (clearSearchText)
        {
            SearchArticoloText = string.Empty;
        }
    }

    private static string NormalizeArticleSearchToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private bool IsDuplicateAutoSearchInsert(string? capturedSearchText, GestionaleArticleSearchResult articolo)
    {
        var normalizedSearch = NormalizeArticleSearchToken(capturedSearchText ?? SearchArticoloText);
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        return _lastAutoAddedArticleOid == articolo.Oid &&
               string.Equals(_lastAutoAddedSearchText, normalizedSearch, StringComparison.OrdinalIgnoreCase) &&
               DateTimeOffset.Now - _lastAutoAddedAt <= TimeSpan.FromMilliseconds(700);
    }

    private void RegisterAutoSearchInsert(string? capturedSearchText, GestionaleArticleSearchResult articolo)
    {
        _lastAutoAddedArticleOid = articolo.Oid;
        _lastAutoAddedSearchText = NormalizeArticleSearchToken(capturedSearchText ?? SearchArticoloText);
        _lastAutoAddedAt = DateTimeOffset.Now;
    }

    private async Task ResetCurrentTabToNewDocumentAsync(string popupTitle, string popupMessage, bool popupSuccess)
    {
        await ShowFinalOperationPopupAsync(popupTitle, popupMessage, popupSuccess);
        await NuovoDocumentoAsync();
    }

    private void RaiseCommandStateChanged()
    {
        NotifyPropertyChanged(nameof(CanPrintPos80));
        CercaArticoliCommand.RaiseCanExecuteChanged();
        AggiungiArticoloCommand.RaiseCanExecuteChanged();
        RimuoviRigaCommand.RaiseCanExecuteChanged();
        CercaClientiCommand.RaiseCanExecuteChanged();
        SelezionaClienteCommand.RaiseCanExecuteChanged();
        AzzeraDocumentoCommand.RaiseCanExecuteChanged();
        SalvaDocumentoCommand.RaiseCanExecuteChanged();
        AbilitaModificaDocumentoFiscalizzatoCommand.RaiseCanExecuteChanged();
        CancellaSchedaCommand.RaiseCanExecuteChanged();
        ApplicaImportoPagamentoCommand.RaiseCanExecuteChanged();
        ScontrinoCommand.RaiseCanExecuteChanged();
        ApriStoricoAcquistiCommand.RaiseCanExecuteChanged();
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanEnableConsultationEdit));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        CancellaSchedaCommand.RaiseCanExecuteChanged();
        NotifyPropertyChanged(nameof(CanSaveDocumentoLocale));
        NotifyPropertyChanged(nameof(CanAzzeraContenuto));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        NotifyPropertyChanged(nameof(HasRiferimentiUfficialiDocumentoCorrente));
    }

    private async Task AggiornaUltimoAcquistoArticoloSelezionatoAsync()
    {
        var articoloOid = RigaSelezionata?.Model.ArticoloOid;
        if (!articoloOid.HasValue || articoloOid.Value <= 0)
        {
            _purchaseQuickInfoRequestVersion++;
            _purchaseQuickInfoCts?.Cancel();
            _lastSelectedArticleOid = null;
            UltimoAcquistoArticolo = null;
            return;
        }

        if (_lastSelectedArticleOid == articoloOid.Value)
        {
            return;
        }

        _lastSelectedArticleOid = articoloOid.Value;
        _purchaseQuickInfoCts?.Cancel();
        _purchaseQuickInfoCts?.Dispose();

        var requestVersion = ++_purchaseQuickInfoRequestVersion;
        var cts = new CancellationTokenSource();
        _purchaseQuickInfoCts = cts;

        try
        {
            var quickInfo = await _documentReadService.GetLatestArticlePurchaseAsync(articoloOid.Value, cts.Token);
            if (cts.IsCancellationRequested ||
                requestVersion != _purchaseQuickInfoRequestVersion ||
                RigaSelezionata?.Model.ArticoloOid != articoloOid.Value)
            {
                return;
            }

            UltimoAcquistoArticolo = quickInfo;
        }
        catch (TaskCanceledException)
        {
        }
        catch
        {
            UltimoAcquistoArticolo = null;
        }
    }

    private GestionaleArticleSearchResult? BuildSelectedRowArticleContext()
    {
        var riga = RigaSelezionata?.Model;
        return riga is null ? null : BuildSearchResultFromRow(riga);
    }

    private static GestionaleArticleSearchResult BuildSearchResultFromRow(RigaDocumentoLocale riga)
    {
        if (riga.ArticoloOid is not int articoloOid || articoloOid <= 0)
        {
            return new GestionaleArticleSearchResult();
        }

        return new GestionaleArticleSearchResult
        {
            Oid = articoloOid,
            CodiceArticolo = riga.CodiceArticolo ?? string.Empty,
            Descrizione = riga.Descrizione ?? string.Empty,
            PrezzoVendita = riga.PrezzoUnitario,
            Giacenza = riga.DisponibilitaRiferimento,
            IvaOid = riga.IvaOid,
            AliquotaIva = riga.AliquotaIva,
            BarcodeAlternativo = NormalizeBarcodeIdentity(riga.BarcodeArticolo),
            VarianteDettaglioOid1 = riga.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = riga.VarianteDettaglioOid2
        };
    }

    private void RinominaOrdiniRiga()
    {
        if (DocumentoLocaleCorrente is null)
        {
            return;
        }

        var righeOrdinate = DocumentoLocaleCorrente.Righe.OrderBy(riga => riga.OrdineRiga).ToList();
        for (var index = 0; index < righeOrdinate.Count; index++)
        {
            righeOrdinate[index].OrdineRiga = index + 1;
        }
    }

    private static string BuildDocumentRowDescription(GestionaleArticleSearchResult articolo)
    {
        if (string.IsNullOrWhiteSpace(articolo.VarianteLabel))
        {
            return articolo.Descrizione;
        }

        return $"{articolo.Descrizione} [{articolo.VarianteLabel}]";
    }

    private static bool IsBarcodeScan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length >= 8 &&
               trimmed.Length <= 18 &&
               trimmed.All(char.IsDigit);
    }

    private static bool IsDirectArticleEntry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains(' '))
        {
            return false;
        }

        if (IsBarcodeScan(trimmed))
        {
            return true;
        }

        var terms = SplitDirectArticleTerms(trimmed);
        if (terms.Count != 1)
        {
            return false;
        }

        return trimmed.Any(char.IsDigit) || trimmed.Contains('_') || trimmed.Contains('-') || trimmed.Contains('/');
    }

    private static IReadOnlyList<string> SplitDirectArticleTerms(string value)
    {
        return Regex
            .Split(value, @"[^0-9A-Za-z]+")
            .Select(term => term.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasAnyOfficialLegacyReference(DocumentoLocale? documento)
    {
        if (documento is null)
        {
            return false;
        }

        return documento.DocumentoGestionaleOid.HasValue ||
               documento.NumeroDocumentoGestionale.HasValue ||
               documento.AnnoDocumentoGestionale.HasValue ||
               documento.DataDocumentoGestionale.HasValue;
    }

    private static bool IsOfficialBancoPublication(DocumentoLocale? documento)
    {
        return documento is not null &&
               documento.ModalitaChiusura == ModalitaChiusuraDocumento.PubblicazioneUfficiale &&
               documento.DocumentoGestionaleOid.HasValue &&
               documento.CategoriaDocumentoBanco == CategoriaDocumentoBanco.Indeterminata &&
               documento.StatoFiscaleBanco == StatoFiscaleBanco.PubblicatoLegacyNonFiscalizzato;
    }

    private void ApplyDocumentAccessResolution(BancoDocumentoAccessResolution resolution)
    {
        _documentAccessResolution = resolution;
        NotifyPropertyChanged(nameof(DocumentoAccessMode));
        NotifyPropertyChanged(nameof(IsReadOnly));
        NotifyPropertyChanged(nameof(CanModifyDocument));
        NotifyPropertyChanged(nameof(CanSelectListino));
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanSaveDocumentoLocale));
        NotifyPropertyChanged(nameof(CanAzzeraContenuto));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        NotifyPropertyChanged(nameof(IsOfficialRecoverableDocument));
        NotifyPropertyChanged(nameof(IsOfficialConsultationDocument));
        NotifyPropertyChanged(nameof(IsConsultationEditOverrideEnabled));
        NotifyPropertyChanged(nameof(CanEnableConsultationEdit));
        NotifyPropertyChanged(nameof(ShowDocumentoAccessBanner));
        NotifyPropertyChanged(nameof(DocumentoAccessBannerText));
        NotifyPropertyChanged(nameof(DocumentoAccessBannerInlineText));
        NotifyPropertyChanged(nameof(HeaderNotificationText));
        NotifyPropertyChanged(nameof(HasHeaderNotification));
        NotifyPropertyChanged(nameof(EmptyDocumentStateTitle));
        NotifyPropertyChanged(nameof(EmptyDocumentStateMessage));
        RaiseCommandStateChanged();
    }

    private void RefreshPublishedDocumentUiState()
    {
        ApplyDocumentAccessResolution(BancoDocumentoAccessResolver.Resolve(
            DocumentoLocaleCorrente,
            DocumentoLocaleCorrente?.DocumentoGestionaleOid,
            FormatDocumentoLocaleLabel(DocumentoLocaleCorrente),
            HasScontrinatoPayments(
                _documentoGestionaleOrigine?.PagatoContanti ?? 0,
                _documentoGestionaleOrigine?.PagatoCarta ?? 0,
                _documentoGestionaleOrigine?.PagatoWeb ?? 0),
            _documentoGestionaleOrigine?.HasLegacyFiscalSignal == true));
        NotifyPropertyChanged(nameof(DocumentoRiferimentoLabel));
        NotifyPropertyChanged(nameof(Titolo));
        NotifyPropertyChanged(nameof(CanSaveDocumentoLocale));
        NotifyPropertyChanged(nameof(CanEmettiCortesia));
        NotifyPropertyChanged(nameof(CanEmettiScontrino));
        NotifyPropertyChanged(nameof(CanCancellaScheda));
        NotifyPropertyChanged(nameof(HasRiferimentiUfficialiDocumentoCorrente));
        NotifyPropertyChanged(nameof(IsPubblicazioneBancoNeutraCorrente));
        NotifyPropertyChanged(nameof(RichiedeConfermaRistampa));
        RaiseCommandStateChanged();
    }

    private void ApplyPublishSuccessUiState(
        FiscalizationResult workflowResult,
        CategoriaDocumentoBanco categoriaDocumentoBanco,
        string origineComando,
        string? prefixMessage = null)
    {
        ClearPreviewDocumentNumber();
        IsScontrinato = workflowResult.WinEcrExecuted;

        var messaggi = new List<string>();
        if (!string.IsNullOrWhiteSpace(prefixMessage))
        {
            messaggi.Add(prefixMessage);
        }

        messaggi.Add(workflowResult.Message);
        if (!string.IsNullOrWhiteSpace(workflowResult.WinEcrMessage))
        {
            messaggi.Add(workflowResult.WinEcrMessage);
        }

        if (!string.IsNullOrWhiteSpace(workflowResult.WinEcrErrorDetails))
        {
            messaggi.Add($"Dettaglio driver: {workflowResult.WinEcrErrorDetails}");
        }

        if (!string.IsNullOrWhiteSpace(workflowResult.TechnicalWarningMessage))
        {
            messaggi.Add(workflowResult.TechnicalWarningMessage);
        }

        StatusMessage = string.Join(" ", messaggi);
        StatoDocumento = workflowResult.OutcomeKind switch
        {
            Banco.Vendita.Fiscal.LegacyPublishOutcomeKind.LegacyPublishedWinEcrIncomplete => "Documento legacy pubblicato - fiscalizzazione WinEcr non completata",
            Banco.Vendita.Fiscal.LegacyPublishOutcomeKind.LegacyPublishedWithTechnicalWarning => "Documento legacy pubblicato con warning tecnico",
            _ => categoriaDocumentoBanco switch
            {
                CategoriaDocumentoBanco.Cortesia => "Documento cortesia pubblicato",
                CategoriaDocumentoBanco.Scontrino when workflowResult.WinEcrExecuted => "Documento fiscalizzato",
                _ => "Documento Banco pubblicato"
            }
        };

        // Dopo un publish riuscito la scheda e` allineata al documento ufficiale.
        HasPendingLocalChanges = false;
        RefreshPublishedDocumentUiState();
        OfficialDocumentPublished?.Invoke(new BancoLegacyPublishNotification
        {
            DocumentoGestionaleOid = workflowResult.DocumentoGestionaleOid,
            CategoriaDocumentoBanco = categoriaDocumentoBanco,
            OutcomeKind = workflowResult.OutcomeKind
        });
        _processLogService.Info(
            nameof(BancoViewModel),
            $"Esito publish {origineComando}: OID={workflowResult.DocumentoGestionaleOid}, Outcome={workflowResult.OutcomeKind}, WarningTecnico={(string.IsNullOrWhiteSpace(workflowResult.TechnicalWarningMessage) ? "no" : "si")}.");

        if (workflowResult.OutcomeKind == Banco.Vendita.Fiscal.LegacyPublishOutcomeKind.LegacyPublishedWinEcrIncomplete)
        {
            _processLogService.Warning(
                nameof(BancoViewModel),
                $"Fiscalizzazione WinEcr non completata per OID={workflowResult.DocumentoGestionaleOid}. CodiceEcr={(workflowResult.WinEcrErrorCode?.ToString() ?? "n.d.")}. Messaggio={workflowResult.WinEcrMessage}. Dettaglio={workflowResult.WinEcrErrorDetails}");
        }
    }

    private void ClearTransientPublishedUiStateAfterFailure()
    {
        if (DocumentoLocaleCorrente is null || HasRiferimentiUfficialiDocumentoCorrente)
        {
            return;
        }

        IsScontrinato = false;
    }

    private static string GetPublishPopupTitle(FiscalizationResult workflowResult)
    {
        return workflowResult.OutcomeKind == Banco.Vendita.Fiscal.LegacyPublishOutcomeKind.LegacyPublishedWithTechnicalWarning
            ? "Operazione completata con avvisi"
            : "Operazione completata";
    }

    private static bool IsPublishPopupSuccess(FiscalizationResult workflowResult)
    {
        return workflowResult.OutcomeKind != Banco.Vendita.Fiscal.LegacyPublishOutcomeKind.LegacyPublishedWinEcrIncomplete;
    }

    private static GestionaleArticleSearchResult? FindBestBarcodeMatch(
        IReadOnlyList<GestionaleArticleSearchResult> results,
        string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText) || results.Count == 0)
        {
            return null;
        }

        var normalizedSearch = searchText.Trim();

        var exactVariantMatch = results.FirstOrDefault(result =>
            result.IsVariante &&
            !string.IsNullOrWhiteSpace(result.BarcodeAlternativo) &&
            result.BarcodeAlternativo.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase));

        if (exactVariantMatch is not null)
        {
            return exactVariantMatch;
        }

        var exactAnyMatch = results.FirstOrDefault(result =>
            !string.IsNullOrWhiteSpace(result.BarcodeAlternativo) &&
            result.BarcodeAlternativo.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase));

        if (exactAnyMatch is not null)
        {
            return exactAnyMatch;
        }

        return results.Count == 1 ? results[0] : null;
    }

    private static DocumentoLocale CreateLegacyBackedDocument(
        GestionaleDocumentDetail detail,
        BancoDocumentoAccessResolution accessResolution)
    {
        var isConsultazioneFiscale = accessResolution.Mode == BancoDocumentoAccessMode.UfficialeConsultazione;
        var modalitaChiusura = isConsultazioneFiscale
            ? ModalitaChiusuraDocumento.Scontrino
            : ModalitaChiusuraDocumento.PubblicazioneUfficiale;
        var categoria = isConsultazioneFiscale
            ? CategoriaDocumentoBanco.Scontrino
            : CategoriaDocumentoBanco.Indeterminata;
        var statoFiscale = isConsultazioneFiscale
            ? StatoFiscaleBanco.FiscalizzazioneWinEcrCompletata
            : StatoFiscaleBanco.Nessuno;
        var statoDocumento = isConsultazioneFiscale
            ? StatoDocumentoLocale.Fiscalizzato
            : StatoDocumentoLocale.ParzialmenteFiscalizzato;

        return DocumentoLocale.Reidrata(
            Guid.NewGuid(),
            statoDocumento,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            string.IsNullOrWhiteSpace(detail.Operatore) ? "Admin Banco" : detail.Operatore,
            string.IsNullOrWhiteSpace(detail.SoggettoNominativo) ? "Cliente generico" : detail.SoggettoNominativo,
            detail.SoggettoOid,
            detail.ListinoOid,
            detail.ListinoNome,
            $"Riaperto dal documento Banco legacy {detail.DocumentoLabel}.",
            modalitaChiusura,
            categoria,
            detail.PagatoSospeso > 0,
            statoFiscale,
            0,
            detail.Oid,
            detail.Numero,
            detail.Anno,
            detail.Data,
            null,
            null,
            detail.Righe.Select(MapRigaGestionale),
            BuildPagamentiFromGestionaleDetail(detail));
    }

    private static IEnumerable<PagamentoLocale> BuildPagamentiFromGestionaleDetail(GestionaleDocumentDetail detail)
    {
        var baseTime = new DateTimeOffset(detail.Data.Date.AddHours(12));
        var offset = 0;

        if (detail.PagatoContanti > 0)
        {
            yield return CreatePagamento("contanti", detail.PagatoContanti, baseTime, ref offset);
        }

        if (detail.PagatoCarta > 0)
        {
            yield return CreatePagamento("carta", detail.PagatoCarta, baseTime, ref offset);
        }

        if (detail.PagatoWeb > 0)
        {
            yield return CreatePagamento("web", detail.PagatoWeb, baseTime, ref offset);
        }

        if (detail.PagatoBuoni > 0)
        {
            yield return CreatePagamento("buoni", detail.PagatoBuoni, baseTime, ref offset);
        }

        if (detail.PagatoSospeso > 0)
        {
            yield return CreatePagamento("sospeso", detail.PagatoSospeso, baseTime, ref offset);
        }
    }

    private static bool HasScontrinatoPayments(decimal contanti, decimal carta, decimal web)
    {
        return contanti > 0 || carta > 0 || web > 0;
    }

    private static bool IsPaymentType(string? rawType, params string[] acceptedTypes)
    {
        var normalized = rawType?.Trim().ToLowerInvariant() ?? string.Empty;
        return acceptedTypes.Any(type => normalized == type);
    }

    private static RigaDocumentoLocale MapRigaGestionale(GestionaleDocumentRowDetail riga)
    {
        return new RigaDocumentoLocale
        {
            OrdineRiga = riga.OrdineRiga,
            TipoRiga = riga.ArticoloOid.HasValue ? TipoRigaDocumento.Articolo : TipoRigaDocumento.Manuale,
            ArticoloOid = riga.ArticoloOid,
            CodiceArticolo = riga.CodiceArticolo,
            BarcodeArticolo = NormalizeBarcodeIdentity(riga.BarcodeArticolo),
            VarianteDettaglioOid1 = riga.VarianteDettaglioOid1,
            VarianteDettaglioOid2 = riga.VarianteDettaglioOid2,
            Descrizione = riga.Descrizione,
            UnitaMisura = string.IsNullOrWhiteSpace(riga.UnitaMisura) ? "PZ" : riga.UnitaMisura,
            Quantita = riga.Quantita,
            DisponibilitaRiferimento = 0,
            PrezzoUnitario = riga.PrezzoUnitario,
            Sconto1 = riga.Sconto1,
            Sconto2 = riga.Sconto2,
            Sconto3 = riga.Sconto3,
            Sconto4 = riga.Sconto4,
            IvaOid = riga.IvaOid,
            AliquotaIva = 0,
            FlagManuale = !riga.ArticoloOid.HasValue
        };
    }

    private sealed record ReorderSupplierSuggestion(
        int Oid,
        string Nome,
        decimal PrezzoRiferimento,
        DateTime DataUltimoAcquisto);
}

