using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using Banco.Core.Contracts.Navigation;
using Banco.Riordino;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Banco.Vendita.Documents;

namespace Banco.UI.Wpf.DashboardModule;

public sealed class DashboardViewModel : ViewModelBase
{
    private const string BancoDestinationKey = "banco.vendita";
    private const string DocumentsDestinationKey = "documenti.lista";
    private const string ReorderDestinationKey = "magazzino.riordino";

    private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");

    private readonly IGestionaleDocumentReadService _documentReadService;
    private readonly IReorderListRepository _reorderListRepository;
    private readonly INavigationRegistry _navigationRegistry;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly DispatcherTimer _clockTimer;
    private AppSettings? _currentSettings;
    private bool _isRefreshing;
    private bool _showSummaryWidget = true;
    private bool _showPaymentsWidget = true;
    private bool _showAlertsWidget = true;
    private bool _showRecentDocumentsWidget = true;
    private bool _showShortcutsWidget = true;
    private bool _showCalendarWidget = true;
    private bool _showClockWidget = true;
    private bool _showNotesWidget = true;
    private decimal _salesToday;
    private decimal _salesYesterday;
    private decimal _salesWeek;
    private decimal _paymentsWeek;
    private int _documentsTodayCount;
    private int _reorderItemsCount;
    private DateTime _lastUpdated = DateTime.Now;
    private DateTime _currentDateTime = DateTime.Now;
    private DateTime _calendarDate = DateTime.Today;
    private string _statusText = "Dashboard pronta.";
    private string _notesText = string.Empty;

    public DashboardViewModel(
        IGestionaleDocumentReadService documentReadService,
        IReorderListRepository reorderListRepository,
        INavigationRegistry navigationRegistry,
        IApplicationConfigurationService configurationService)
    {
        _documentReadService = documentReadService;
        _reorderListRepository = reorderListRepository;
        _navigationRegistry = navigationRegistry;
        _configurationService = configurationService;

        RefreshCommand = new RelayCommand(QueueRefresh, () => !IsRefreshing);
        SaveNotesCommand = new RelayCommand(SaveNotes);
        HideSummaryWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowSummaryWidget), false));
        HidePaymentsWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowPaymentsWidget), false));
        HideAlertsWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowAlertsWidget), false));
        HideRecentDocumentsWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowRecentDocumentsWidget), false));
        HideShortcutsWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowShortcutsWidget), false));
        HideCalendarWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowCalendarWidget), false));
        HideClockWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowClockWidget), false));
        HideNotesWidgetCommand = new RelayCommand(() => SetWidgetVisibility(nameof(ShowNotesWidget), false));
        RestoreWidgetsCommand = new RelayCommand(RestoreWidgets, () => AnyWidgetHidden);
        OpenBancoCommand = new RelayCommand(() => OpenDestinationRequested?.Invoke(BancoDestinationKey));
        OpenDocumentsCommand = new RelayCommand(() => OpenDestinationRequested?.Invoke(DocumentsDestinationKey));
        OpenReorderCommand = new RelayCommand(() => OpenDestinationRequested?.Invoke(ReorderDestinationKey));

        BuildShortcuts();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock(DateTime.Now);
        _clockTimer.Start();
        UpdateClock(DateTime.Now);

        _ = LoadShellPreferencesAsync();
        QueueRefresh();
    }

    public event Action<int>? OpenDocumentInBancoRequested;

    public event Action<string>? OpenDestinationRequested;

    public ObservableCollection<DashboardPaymentBreakdownItemViewModel> PaymentBreakdown { get; } = [];

    public ObservableCollection<DashboardAlertItemViewModel> Alerts { get; } = [];

    public ObservableCollection<DashboardRecentDocumentItemViewModel> RecentDocuments { get; } = [];

    public ObservableCollection<DashboardShortcutItemViewModel> QuickShortcuts { get; } = [];

    public ICommand RefreshCommand { get; }

    public ICommand SaveNotesCommand { get; }

    public ICommand HideSummaryWidgetCommand { get; }

    public ICommand HidePaymentsWidgetCommand { get; }

    public ICommand HideAlertsWidgetCommand { get; }

    public ICommand HideRecentDocumentsWidgetCommand { get; }

    public ICommand HideShortcutsWidgetCommand { get; }

    public ICommand HideCalendarWidgetCommand { get; }

    public ICommand HideClockWidgetCommand { get; }

    public ICommand HideNotesWidgetCommand { get; }

    public ICommand RestoreWidgetsCommand { get; }

    public ICommand OpenBancoCommand { get; }

    public ICommand OpenDocumentsCommand { get; }

    public ICommand OpenReorderCommand { get; }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool ShowSummaryWidget
    {
        get => _showSummaryWidget;
        private set => UpdateWidgetVisibility(ref _showSummaryWidget, value);
    }

    public bool ShowPaymentsWidget
    {
        get => _showPaymentsWidget;
        private set => UpdateWidgetVisibility(ref _showPaymentsWidget, value);
    }

    public bool ShowAlertsWidget
    {
        get => _showAlertsWidget;
        private set => UpdateWidgetVisibility(ref _showAlertsWidget, value);
    }

    public bool ShowRecentDocumentsWidget
    {
        get => _showRecentDocumentsWidget;
        private set => UpdateWidgetVisibility(ref _showRecentDocumentsWidget, value);
    }

    public bool ShowShortcutsWidget
    {
        get => _showShortcutsWidget;
        private set => UpdateWidgetVisibility(ref _showShortcutsWidget, value);
    }

    public bool ShowCalendarWidget
    {
        get => _showCalendarWidget;
        private set => UpdateWidgetVisibility(ref _showCalendarWidget, value);
    }

    public bool ShowClockWidget
    {
        get => _showClockWidget;
        private set => UpdateWidgetVisibility(ref _showClockWidget, value);
    }

    public bool ShowNotesWidget
    {
        get => _showNotesWidget;
        private set => UpdateWidgetVisibility(ref _showNotesWidget, value);
    }

    public bool AnyWidgetHidden =>
        !ShowSummaryWidget
        || !ShowPaymentsWidget
        || !ShowAlertsWidget
        || !ShowRecentDocumentsWidget
        || !ShowShortcutsWidget
        || !ShowCalendarWidget
        || !ShowClockWidget
        || !ShowNotesWidget;

    public string SalesTodayDisplay => FormatCurrency(_salesToday);

    public string SalesYesterdayDisplay => FormatCurrency(_salesYesterday);

    public string SalesWeekDisplay => FormatCurrency(_salesWeek);

    public string PaymentsWeekDisplay => FormatCurrency(_paymentsWeek);

    public string DocumentsTodayDisplay => $"{_documentsTodayCount} doc.";

    public string ReorderItemsDisplay => $"{_reorderItemsCount} articoli";

    public string LastUpdatedDisplay => $"Aggiornata {LastUpdatedTimeDisplay}";

    public string LastUpdatedTimeDisplay => _lastUpdated.ToString("dd/MM/yyyy HH:mm", ItCulture);

    public DateTime CalendarDate
    {
        get => _calendarDate;
        set => SetProperty(ref _calendarDate, value);
    }

    public string ClockTimeDisplay => _currentDateTime.ToString("HH:mm:ss", ItCulture);

    public string ClockDateDisplay => _currentDateTime.ToString("dddd dd MMMM yyyy", ItCulture);

    public string NotesText
    {
        get => _notesText;
        set => SetProperty(ref _notesText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool HasAlerts => Alerts.Count > 0;

    public bool HasRecentDocuments => RecentDocuments.Count > 0;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            var weekStart = today.AddDays(-6);

            var recentDocuments = await _documentReadService.GetRecentBancoDocumentsAsync(80, cancellationToken);
            var reorderSnapshot = await _reorderListRepository.GetCurrentListAsync(cancellationToken);

            var todayDocuments = recentDocuments.Where(document => document.Data.Date == today).ToList();
            var yesterdayDocuments = recentDocuments.Where(document => document.Data.Date == yesterday).ToList();
            var weekDocuments = recentDocuments.Where(document => document.Data.Date >= weekStart).ToList();

            _salesToday = todayDocuments.Sum(document => document.TotaleDocumento);
            _salesYesterday = yesterdayDocuments.Sum(document => document.TotaleDocumento);
            _salesWeek = weekDocuments.Sum(document => document.TotaleDocumento);
            _paymentsWeek = weekDocuments.Sum(document => document.TotalePagatoUfficiale + document.Pagatosospeso);
            _documentsTodayCount = todayDocuments.Count;
            _reorderItemsCount = reorderSnapshot.Items.Count;
            _lastUpdated = DateTime.Now;

            RebuildPayments(weekDocuments);
            RebuildAlerts(reorderSnapshot, todayDocuments);
            RebuildRecentDocuments(recentDocuments);

            NotifyMetricChanges();
            StatusText = $"Ultimo aggiornamento dashboard alle {LastUpdatedTimeDisplay}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Dashboard non aggiornata: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadShellPreferencesAsync()
    {
        _currentSettings = await _configurationService.LoadAsync();
        NotesText = _currentSettings.ShellUi.DashboardNotes ?? string.Empty;
    }

    private void RebuildPayments(IReadOnlyList<GestionaleDocumentSummary> weekDocuments)
    {
        PaymentBreakdown.Clear();

        var paymentRows = new[]
        {
            CreatePayment("Contanti", weekDocuments.Sum(item => item.PagatoContanti)),
            CreatePayment("Carta", weekDocuments.Sum(item => item.PagatoCarta)),
            CreatePayment("Web", weekDocuments.Sum(item => item.PagatoWeb)),
            CreatePayment("Buoni", weekDocuments.Sum(item => item.PagatoBuoni)),
            CreatePayment("Sospeso", weekDocuments.Sum(item => item.Pagatosospeso))
        };

        var total = paymentRows.Sum(item => item.Amount);
        foreach (var row in paymentRows.Where(item => item.Amount > 0))
        {
            PaymentBreakdown.Add(row with
            {
                Percentage = total <= 0 ? 0 : Math.Round((double)(row.Amount / total * 100), 1)
            });
        }

        if (PaymentBreakdown.Count == 0)
        {
            PaymentBreakdown.Add(new DashboardPaymentBreakdownItemViewModel("Nessun pagamento", 0, 0));
        }
    }

    private void RebuildAlerts(ReorderListSnapshot snapshot, IReadOnlyList<GestionaleDocumentSummary> todayDocuments)
    {
        Alerts.Clear();

        if (snapshot.Items.Count > 0)
        {
            Alerts.Add(new DashboardAlertItemViewModel(
                "Lista ordini aggiornata",
                $"{snapshot.Items.Count} articoli risultano attualmente nella lista riordino.",
                "Riordino"));

            foreach (var item in snapshot.Items.Take(3))
            {
                var supplier = string.IsNullOrWhiteSpace(item.FornitoreSelezionatoNome)
                    ? item.FornitoreSuggeritoNome
                    : item.FornitoreSelezionatoNome;

                Alerts.Add(new DashboardAlertItemViewModel(
                    item.CodiceArticolo,
                    $"{item.Descrizione} | q.ta {item.QuantitaDaOrdinare:0.##} {item.UnitaMisura}".Trim(),
                    string.IsNullOrWhiteSpace(supplier) ? "Da ordinare" : $"Fornitore {supplier}"));
            }
        }

        var suspendedToday = todayDocuments.Count(document => document.IsSospeso);
        if (suspendedToday > 0)
        {
            Alerts.Add(new DashboardAlertItemViewModel(
                "Documenti sospesi oggi",
                $"{suspendedToday} documenti Banco con componente sospeso risultano aperti nella giornata odierna.",
                "Controllo cassa"));
        }

        if (Alerts.Count == 0)
        {
            Alerts.Add(new DashboardAlertItemViewModel(
                "Nessun avviso operativo",
                "Non ci sono segnalazioni urgenti su vendite Banco o lista ordini.",
                "Dashboard"));
        }

        NotifyPropertyChanged(nameof(HasAlerts));
    }

    private void RebuildRecentDocuments(IReadOnlyList<GestionaleDocumentSummary> documents)
    {
        RecentDocuments.Clear();

        foreach (var document in documents.Take(6))
        {
            RecentDocuments.Add(new DashboardRecentDocumentItemViewModel(document, OpenDocument));
        }

        NotifyPropertyChanged(nameof(HasRecentDocuments));
    }

    private void BuildShortcuts()
    {
        QuickShortcuts.Clear();

        var entries = _navigationRegistry.GetEntries()
            .Where(entry => entry.IsVisibleInShell)
            .Where(entry => entry.Availability == NavigationEntryAvailability.Available)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DestinationKey))
            .Where(entry => !string.Equals(entry.DestinationKey, "dashboard.home", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => _navigationRegistry.GetMacroCategory(entry.MacroCategoryKey)?.RailOrder ?? int.MaxValue)
            .ThenBy(entry => entry.ContextOrder)
            .ToList();

        foreach (var entry in entries)
        {
            var macroCategory = _navigationRegistry.GetMacroCategory(entry.MacroCategoryKey);
            if (macroCategory is null)
            {
                continue;
            }

            QuickShortcuts.Add(new DashboardShortcutItemViewModel(
                entry.Key,
                entry.Title,
                entry.DestinationKey!,
                macroCategory.Title,
                ResolveIconData(entry, macroCategory),
                entry.DefaultAccentColor,
                () => OpenDestinationRequested?.Invoke(entry.DestinationKey!)));
        }
    }

    private void OpenDocument(int documentOid)
    {
        OpenDocumentInBancoRequested?.Invoke(documentOid);
    }

    private void QueueRefresh()
    {
        _ = RefreshAsync();
    }

    private void SaveNotes()
    {
        _ = SaveNotesAsync();
    }

    private async Task SaveNotesAsync()
    {
        _currentSettings ??= await _configurationService.LoadAsync();
        _currentSettings.ShellUi.DashboardNotes = NotesText?.Trim() ?? string.Empty;
        await _configurationService.SaveAsync(_currentSettings);
        StatusText = "Appunti dashboard salvati.";
    }

    private void RestoreWidgets()
    {
        ShowSummaryWidget = true;
        ShowPaymentsWidget = true;
        ShowAlertsWidget = true;
        ShowRecentDocumentsWidget = true;
        ShowShortcutsWidget = true;
        ShowCalendarWidget = true;
        ShowClockWidget = true;
        ShowNotesWidget = true;
    }

    private void SetWidgetVisibility(string propertyName, bool isVisible)
    {
        switch (propertyName)
        {
            case nameof(ShowSummaryWidget):
                ShowSummaryWidget = isVisible;
                break;
            case nameof(ShowPaymentsWidget):
                ShowPaymentsWidget = isVisible;
                break;
            case nameof(ShowAlertsWidget):
                ShowAlertsWidget = isVisible;
                break;
            case nameof(ShowRecentDocumentsWidget):
                ShowRecentDocumentsWidget = isVisible;
                break;
            case nameof(ShowShortcutsWidget):
                ShowShortcutsWidget = isVisible;
                break;
            case nameof(ShowCalendarWidget):
                ShowCalendarWidget = isVisible;
                break;
            case nameof(ShowClockWidget):
                ShowClockWidget = isVisible;
                break;
            case nameof(ShowNotesWidget):
                ShowNotesWidget = isVisible;
                break;
        }
    }

    private void UpdateClock(DateTime now)
    {
        _currentDateTime = now;
        CalendarDate = now.Date;
        NotifyPropertyChanged(nameof(ClockTimeDisplay));
        NotifyPropertyChanged(nameof(ClockDateDisplay));
    }

    private void NotifyMetricChanges()
    {
        NotifyPropertyChanged(nameof(SalesTodayDisplay));
        NotifyPropertyChanged(nameof(SalesYesterdayDisplay));
        NotifyPropertyChanged(nameof(SalesWeekDisplay));
        NotifyPropertyChanged(nameof(PaymentsWeekDisplay));
        NotifyPropertyChanged(nameof(DocumentsTodayDisplay));
        NotifyPropertyChanged(nameof(ReorderItemsDisplay));
        NotifyPropertyChanged(nameof(LastUpdatedDisplay));
        NotifyPropertyChanged(nameof(LastUpdatedTimeDisplay));
    }

    private void RaiseCommandStates()
    {
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreWidgetsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateWidgetVisibility(ref bool field, bool value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            NotifyPropertyChanged(nameof(AnyWidgetHidden));
            RaiseCommandStates();
        }
    }

    private static DashboardPaymentBreakdownItemViewModel CreatePayment(string label, decimal amount)
    {
        return new DashboardPaymentBreakdownItemViewModel(label, amount, 0);
    }

    private static string ResolveIconData(NavigationEntryDefinition entry, NavigationMacroCategoryDefinition macroCategory)
    {
        return entry.DestinationKey switch
        {
            BancoDestinationKey => "M3,13H5V11H3V13M3,17H5V15H3V17M3,9H5V7H3V9M7,13H21V11H7V13M7,17H21V15H7V17M7,9V11H21V9H7Z",
            DocumentsDestinationKey => "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20M12,19L8,15H10.5V12H13.5V15H16L12,19Z",
            ReorderDestinationKey => "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z",
            "magazzino.articolo" => "M4,2H20A2,2 0 0,1 22,4V16A2,2 0 0,1 20,18H16L12,22L8,18H4A2,2 0 0,1 2,16V4A2,2 0 0,1 4,2M6,7H18V9H6V7M6,11H16V13H6V11Z",
            "anagrafiche.punti" => "M12,2L15,8H22L16.5,12L18.8,19L12,14.8L5.2,19L7.5,12L2,8H9Z",
            "impostazioni.db" => "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.04 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.04 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z",
            "impostazioni.fastreport" => "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H13.81C13.28,21.09 13,20.05 13,19C13,15.69 15.69,13 19,13C19.34,13 19.67,13.03 20,13.08V8L14,2M13,9V3.5L18.5,9H13M18,18L15,15L16.41,13.58L18,15.17L21.59,11.58L23,13L18,18Z",
            "impostazioni.pos" => "M4,2H20A2,2 0 0,1 22,4V16A2,2 0 0,1 20,18H16L12,22L8,18H4A2,2 0 0,1 2,16V4A2,2 0 0,1 4,2M4,4V16H8.83L12,19.17L15.17,16H20V4H4M6,7H18V9H6V7M6,11H16V13H6V11Z",
            "impostazioni.fiscale" => "M18,3H6V7H18M19,12A1,1 0 0,1 18,11A1,1 0 0,1 19,10A1,1 0 0,1 20,11A1,1 0 0,1 19,12M16,19H8V14H16M19,8H5A3,3 0 0,0 2,11V17H6V21H18V17H22V11A3,3 0 0,0 19,8Z",
            "impostazioni.diagnostica" => "M22,21H2V3H4V19H6V10H10V19H12V6H16V19H18V13H22V21Z",
            "impostazioni.backup" => "M4,4H20V8H4V4M4,10H20V20H4V10M8,12V18H10V12H8M12,12V18H16V16H14V15H16V12H12Z",
            "impostazioni.temi" => "M12,3A9,9 0 0,1 21,12C21,16.97 16.97,21 12,21A9,9 0 0,1 3,12A9,9 0 0,1 12,3M12,5A7,7 0 0,0 5,12A7,7 0 0,0 12,19C13.85,19 15.55,18.28 16.81,17.11C15.86,16.8 15,16.13 14.44,15.28C13.66,14.09 13.62,12.67 14.16,11.45C14.5,10.68 15.11,10.06 15.87,9.7C16.63,9.34 17.49,9.26 18.3,9.47C17.2,6.86 14.82,5 12,5Z",
            _ => macroCategory.IconResourceKey switch
            {
                "IconDocumenti" => "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z",
                "IconFolder" => "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z",
                "IconPoints" => "M12,2L15,8H22L16.5,12L18.8,19L12,14.8L5.2,19L7.5,12L2,8H9Z",
                "IconCash" => "M5,6H23V18H5V6M14,9A3,3 0 0,1 17,12A3,3 0 0,1 14,15A3,3 0 0,1 11,12A3,3 0 0,1 14,9Z",
                "IconSettings" => "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5Z",
                "IconDiagnostics" => "M22,21H2V3H4V19H6V10H10V19H12V6H16V19H18V13H22V21Z",
                _ => "M3,13H5V11H3V13M3,17H5V15H3V17M3,9H5V7H3V9M7,13H21V11H7V13M7,17H21V15H7V17M7,9V11H21V9H7Z"
            }
        };
    }

    private static string FormatCurrency(decimal value)
    {
        return string.Format(ItCulture, "{0:C}", value);
    }
}

public sealed record DashboardPaymentBreakdownItemViewModel(string Label, decimal Amount, double Percentage)
{
    public string AmountDisplay => string.Format(CultureInfo.GetCultureInfo("it-IT"), "{0:C}", Amount);

    public string PercentageDisplay => $"{Percentage:0.#}%";
}

public sealed record DashboardAlertItemViewModel(string Title, string Description, string Context);

public sealed class DashboardShortcutItemViewModel
{
    public DashboardShortcutItemViewModel(
        string key,
        string title,
        string destinationKey,
        string categoryTitle,
        string iconData,
        string accentColor,
        Action openAction)
    {
        Key = key;
        Title = title;
        DestinationKey = destinationKey;
        CategoryTitle = categoryTitle;
        IconData = iconData;
        AccentColor = accentColor;
        OpenCommand = new RelayCommand(openAction);
    }

    public string Key { get; }

    public string Title { get; }

    public string DestinationKey { get; }

    public string CategoryTitle { get; }

    public string IconData { get; }

    public string AccentColor { get; }

    public ICommand OpenCommand { get; }
}

public sealed class DashboardRecentDocumentItemViewModel
{
    private static readonly CultureInfo ItCulture = CultureInfo.GetCultureInfo("it-IT");

    public DashboardRecentDocumentItemViewModel(GestionaleDocumentSummary document, Action<int> openAction)
    {
        Oid = document.Oid;
        NumeroDisplay = document.Numero.ToString(ItCulture);
        DataDisplay = document.Data.ToString("dd/MM/yyyy HH:mm", ItCulture);
        ClienteDisplay = document.ClienteLabel;
        TotaleDisplay = string.Format(ItCulture, "{0:C}", document.TotaleDocumento);
        PaymentDisplay = ResolvePaymentDisplay(document);
        OpenCommand = new RelayCommand(() => openAction(document.Oid));
    }

    public int Oid { get; }

    public string NumeroDisplay { get; }

    public string DataDisplay { get; }

    public string ClienteDisplay { get; }

    public string TotaleDisplay { get; }

    public string PaymentDisplay { get; }

    public ICommand OpenCommand { get; }

    private static string ResolvePaymentDisplay(GestionaleDocumentSummary document)
    {
        if (document.IsSospeso)
        {
            return "Sospeso";
        }

        var paymentModes = new List<string>();
        if (document.PagatoContanti > 0)
        {
            paymentModes.Add("Contanti");
        }

        if (document.PagatoCarta > 0)
        {
            paymentModes.Add("Carta");
        }

        if (document.PagatoWeb > 0)
        {
            paymentModes.Add("Web");
        }

        if (document.PagatoBuoni > 0)
        {
            paymentModes.Add("Buoni");
        }

        return paymentModes.Count == 0 ? "Da verificare" : string.Join(" + ", paymentModes);
    }
}
