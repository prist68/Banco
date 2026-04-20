using Banco.Core.Contracts.Navigation;
using Banco.Core.Infrastructure;
using Banco.Magazzino.ViewModels;
using Banco.Punti.ViewModels;
using Banco.Sidebar.ViewModels;
using Banco.UI.Wpf.PosModule;
using Banco.UI.Wpf.WinEcrModule;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private const string BancoDestinationKey = "banco.vendita";
    private const string DocumentsDestinationKey = "documenti.lista";
    private const string ReorderDestinationKey = "magazzino.riordino";
    private const string MagazzinoArticleDestinationKey = "magazzino.articolo";
    private const string PuntiDestinationKey = "anagrafiche.punti";
    private const string SettingsDestinationKey = "impostazioni.db";
    private const string FastReportDestinationKey = "impostazioni.fastreport";
    private const string PosDestinationKey = "impostazioni.pos";
    private const string FiscaleDestinationKey = "impostazioni.fiscale";
    private const string DiagnosticsDestinationKey = "impostazioni.diagnostica";
    private const string BackupDestinationKey = "impostazioni.backup";
    private const string ThemeDestinationKey = "impostazioni.temi";

    private readonly IApplicationConfigurationService _configurationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationRegistry _navigationRegistry;
    private readonly Dictionary<string, ShellWorkspaceDescriptor> _workspaceDescriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShellWorkspaceTabViewModel> _tabsByKey = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _sidebarSaveCts;
    private bool _isRestoringPreferences;
    private AppSettings? _currentSettings;
    private int _bancoTabSequence = 1;
    private double _sidebarWidth = 348;
    private ShellWorkspaceTabViewModel? _activeTab;

    public ShellViewModel(
        IApplicationConfigurationService configurationService,
        IServiceProvider serviceProvider,
        INavigationRegistry navigationRegistry,
        SidebarHostViewModel sidebar,
        BancoViewModel bancoViewModel,
        DocumentListViewModel documentListViewModel,
        UIDocumentListHostViewModel uiDocumentListHostViewModel,
        ReorderListViewModel reorderListViewModel,
        MagazzinoArticleViewModel magazzinoArticleViewModel,
        SettingsViewModel settingsViewModel,
        FastReportStudioViewModel fastReportStudioViewModel,
        PosConfigurationViewModel posConfigurationViewModel,
        WinEcrConfigurationViewModel winEcrConfigurationViewModel,
        DiagnosticsViewModel diagnosticsViewModel,
        BackupImportViewModel backupImportViewModel,
        ThemeManagementViewModel themeManagementViewModel,
        PuntiViewModel puntiViewModel)
    {
        _configurationService = configurationService;
        _serviceProvider = serviceProvider;
        _navigationRegistry = navigationRegistry;
        Sidebar = sidebar;
        _configurationService.SettingsChanged += OnSettingsChanged;
        Sidebar.NavigateRequested += OnSidebarNavigateRequested;
        Sidebar.PropertyChanged += OnSidebarPropertyChanged;

        BancoViewModel = bancoViewModel;
        DocumentListViewModel = documentListViewModel;
        UIDocumentListHostViewModel = uiDocumentListHostViewModel;
        ReorderListViewModel = reorderListViewModel;
        MagazzinoArticleViewModel = magazzinoArticleViewModel;
        SettingsViewModel = settingsViewModel;
        FastReportStudioViewModel = fastReportStudioViewModel;
        PosConfigurationViewModel = posConfigurationViewModel;
        WinEcrConfigurationViewModel = winEcrConfigurationViewModel;
        DiagnosticsViewModel = diagnosticsViewModel;
        BackupImportViewModel = backupImportViewModel;
        ThemeManagementViewModel = themeManagementViewModel;
        PuntiViewModel = puntiViewModel;

        RegisterWorkspaceDescriptors();
        ValidateDestinationResolution();

        DocumentListViewModel.OpenDocumentInBancoRequested += OnOpenDocumentInBancoRequested;
        DocumentListViewModel.OpenLocalDocumentInBancoRequested += OnOpenLocalDocumentInBancoRequested;
        DocumentListViewModel.NewBancoDocumentRequested += OnNewBancoDocumentRequested;
        SubscribeBancoViewModel(BancoViewModel);
        PuntiViewModel.PromotionsConfigurationSaved += OnPromotionsConfigurationSaved;

        BuildInitialWorkspace();
        _ = InitializeAsync();
    }

    public string TitoloApplicazione => "Banco";

    public string Versione => "1.0.0.0";

    public SidebarHostViewModel Sidebar { get; }

    public BancoViewModel BancoViewModel { get; }

    public BancoViewModel CurrentBancoViewModel => ActiveTab?.Content as BancoViewModel ?? BancoViewModel;

    public DocumentListViewModel DocumentListViewModel { get; }

    public UIDocumentListHostViewModel UIDocumentListHostViewModel { get; }

    public ReorderListViewModel ReorderListViewModel { get; }

    public MagazzinoArticleViewModel MagazzinoArticleViewModel { get; }

    public SettingsViewModel SettingsViewModel { get; }

    public FastReportStudioViewModel FastReportStudioViewModel { get; }

    public PosConfigurationViewModel PosConfigurationViewModel { get; }

    public WinEcrConfigurationViewModel WinEcrConfigurationViewModel { get; }

    public DiagnosticsViewModel DiagnosticsViewModel { get; }

    public BackupImportViewModel BackupImportViewModel { get; }

    public ThemeManagementViewModel ThemeManagementViewModel { get; }

    public PuntiViewModel PuntiViewModel { get; }

    public ObservableCollection<ShellWorkspaceTabViewModel> OpenTabs { get; } = [];

    public double SidebarRailWidth => 164;

    public double SidebarWidth
    {
        get => _sidebarWidth;
        set
        {
            if (SetProperty(ref _sidebarWidth, Math.Clamp(value, 240, 360)))
            {
                NotifyPropertyChanged(nameof(SidebarContextPanelWidth));
                if (!_isRestoringPreferences)
                {
                    ScheduleSidebarSave();
                }
            }
        }
    }

    public double SidebarContextPanelWidth => SidebarWidth;

    public ShellWorkspaceTabViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                NotifyPropertyChanged(nameof(CurrentView));
                NotifyPropertyChanged(nameof(CurrentBancoViewModel));
                UpdateSidebarActiveState();
            }
        }
    }

    public object? CurrentView => ActiveTab?.Content;

    public string NavigationStatus => "Navigazione pronta";

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        _currentSettings = e.Settings;
    }

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SidebarHostViewModel.IsContextPanelVisible), StringComparison.Ordinal))
        {
            return;
        }

        NotifyPropertyChanged(nameof(SidebarWidth));
        NotifyPropertyChanged(nameof(SidebarContextPanelWidth));
    }

    private void BuildInitialWorkspace()
    {
        OpenDestination(BancoDestinationKey);
    }

    private async Task InitializeAsync()
    {
        var settings = await _configurationService.LoadAsync();
        _isRestoringPreferences = true;
        try
        {
            _currentSettings = settings;
            SidebarWidth = Math.Clamp(settings.ShellUi.SidebarWidth, 240, 360);
        }
        finally
        {
            _isRestoringPreferences = false;
        }

        await Sidebar.InitializeAsync();
        UpdateSidebarActiveState();
    }

    private void RegisterWorkspaceDescriptors()
    {
        _workspaceDescriptors["workspace.banco"] = new ShellWorkspaceDescriptor("workspace.banco", CanClose: true, ResolveContent: () => BancoViewModel);
        _workspaceDescriptors["workspace.documenti"] = new ShellWorkspaceDescriptor("workspace.documenti", CanClose: true, ResolveContent: () => UIDocumentListHostViewModel);
        _workspaceDescriptors["workspace.riordino"] = new ShellWorkspaceDescriptor("workspace.riordino", CanClose: true, ResolveContent: () => ReorderListViewModel);
        _workspaceDescriptors["workspace.magazzino-articolo"] = new ShellWorkspaceDescriptor("workspace.magazzino-articolo", CanClose: true, ResolveContent: () => MagazzinoArticleViewModel);
        _workspaceDescriptors["workspace.punti"] = new ShellWorkspaceDescriptor("workspace.punti", CanClose: true, ResolveContent: () => PuntiViewModel);
        _workspaceDescriptors["workspace.configurazioni-generali"] = new ShellWorkspaceDescriptor("workspace.configurazioni-generali", CanClose: true, ResolveContent: () => SettingsViewModel);
        _workspaceDescriptors["workspace.fastreport"] = new ShellWorkspaceDescriptor("workspace.fastreport", CanClose: true, ResolveContent: () => FastReportStudioViewModel);
        _workspaceDescriptors["workspace.pos"] = new ShellWorkspaceDescriptor("workspace.pos", CanClose: true, ResolveContent: () => PosConfigurationViewModel);
        _workspaceDescriptors["workspace.fiscale"] = new ShellWorkspaceDescriptor("workspace.fiscale", CanClose: true, ResolveContent: () => WinEcrConfigurationViewModel);
        _workspaceDescriptors["workspace.diagnostica"] = new ShellWorkspaceDescriptor("workspace.diagnostica", CanClose: true, ResolveContent: () => DiagnosticsViewModel);
        _workspaceDescriptors["workspace.backup"] = new ShellWorkspaceDescriptor("workspace.backup", CanClose: true, ResolveContent: () => BackupImportViewModel);
        _workspaceDescriptors["workspace.temi"] = new ShellWorkspaceDescriptor("workspace.temi", CanClose: true, ResolveContent: () => ThemeManagementViewModel);
    }

    private void ValidateDestinationResolution()
    {
        var unresolvedDestinations = _navigationRegistry.GetDestinations()
            .Where(destination => destination.IsAvailable)
            .Where(destination => !_workspaceDescriptors.ContainsKey(destination.WorkspaceKey))
            .Select(destination => destination.Key)
            .ToList();

        if (unresolvedDestinations.Count > 0)
        {
            throw new InvalidOperationException($"Le seguenti DestinationKey non sono risolvibili dalla shell: {string.Join(", ", unresolvedDestinations)}.");
        }
    }

    private void OnSidebarNavigateRequested(object? sender, SidebarNavigateRequestedEventArgs e)
    {
        OpenDestination(e.DestinationKey);
    }

    private void OpenDestination(string destinationKey, bool activate = true)
    {
        var destination = _navigationRegistry.GetDestination(destinationKey)
            ?? throw new InvalidOperationException($"DestinationKey non registrata: {destinationKey}.");

        if (!destination.IsAvailable)
        {
            return;
        }

        if (!_workspaceDescriptors.TryGetValue(destination.WorkspaceKey, out var descriptor))
        {
            throw new InvalidOperationException($"WorkspaceKey non risolvibile per la destination {destination.Key}: {destination.WorkspaceKey}.");
        }

        var content = descriptor.ResolveContent();
        OpenTab(destination.WorkspaceKey, destination.Key, destination.Title, content, descriptor.CanClose, activate);
    }

    private void OnOpenDocumentInBancoRequested(int documentOid)
    {
        var banco = GetPreferredBancoViewModel();
        _ = banco.LoadGestionaleDocumentAsync(documentOid);
    }

    private void OnOpenLocalDocumentInBancoRequested(Guid documentId)
    {
        var banco = GetPreferredBancoViewModel();
        _ = banco.LoadLocalDocumentAsync(documentId);
    }

    private void OnNewBancoDocumentRequested()
    {
        OpenFreshBancoTab();
    }

    private void OnOfficialDocumentPublished(BancoLegacyPublishNotification notification)
    {
        _ = DocumentListViewModel.RefreshAsync();
    }

    private void OnOfficialDocumentDeleted(int documentoGestionaleOid)
    {
        _ = DocumentListViewModel.RefreshAsync();
    }

    private void OnOfficialDocumentMissing(int documentoGestionaleOid)
    {
        MessageBox.Show(
            $"Il documento gestionale {documentoGestionaleOid} non esiste piu` nel database.",
            "Documento inesistente",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnDirectArticleMissing(string articleToken)
    {
        MessageBox.Show(
            $"L'articolo \"{articleToken}\" non esiste nel database.",
            "Articolo inesistente",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnPromotionsConfigurationSaved()
    {
        var bancoTabs = OpenTabs
            .Select(tab => tab.Content)
            .OfType<BancoViewModel>()
            .Distinct()
            .ToList();

        if (!bancoTabs.Contains(BancoViewModel))
        {
            bancoTabs.Add(BancoViewModel);
        }

        foreach (var banco in bancoTabs)
        {
            _ = banco.RefreshPromoConfigurationAsync();
        }
    }

    private void OpenTab(string key, string destinationKey, string title, object content, bool canClose, bool activate)
    {
        if (_tabsByKey.TryGetValue(key, out var existingTab))
        {
            if (activate)
            {
                ActiveTab = existingTab;
            }

            return;
        }

        var tab = new ShellWorkspaceTabViewModel(key, destinationKey, title, content, canClose, CloseTab);
        _tabsByKey[key] = tab;
        OpenTabs.Add(tab);

        if (activate)
        {
            ActiveTab = tab;
        }
    }

    public BancoViewModel OpenFreshBancoTab()
    {
        var destination = _navigationRegistry.GetDestination(BancoDestinationKey)
            ?? throw new InvalidOperationException("DestinationKey banco.vendita non registrata.");

        if (!_workspaceDescriptors.TryGetValue(destination.WorkspaceKey, out var descriptor))
        {
            throw new InvalidOperationException($"WorkspaceKey non risolvibile per la destination {destination.Key}: {destination.WorkspaceKey}.");
        }

        var key = $"{destination.WorkspaceKey}-{++_bancoTabSequence}";
        var title = $"Banco {_bancoTabSequence}";
        var bancoViewModel = ActivatorUtilities.CreateInstance<BancoViewModel>(_serviceProvider);
        SubscribeBancoViewModel(bancoViewModel);
        OpenTab(key, destination.Key, title, bancoViewModel, descriptor.CanClose, activate: true);
        return bancoViewModel;
    }

    public BancoViewModel GetPreferredBancoViewModel()
    {
        if (ActiveTab?.Content is BancoViewModel activeBanco)
        {
            return activeBanco;
        }

        var fallbackTab = OpenTabs.LastOrDefault(tab => tab.Content is BancoViewModel);
        if (fallbackTab?.Content is BancoViewModel fallbackBanco)
        {
            ActiveTab = fallbackTab;
            return fallbackBanco;
        }

        OpenDestination(BancoDestinationKey);
        return BancoViewModel;
    }

    private void SubscribeBancoViewModel(BancoViewModel bancoViewModel)
    {
        bancoViewModel.ShowDocumentListRequested += () => OpenDestination(DocumentsDestinationKey);
        bancoViewModel.OpenSettingsRequested += () => OpenDestination(SettingsDestinationKey);
        bancoViewModel.OfficialDocumentPublished += OnOfficialDocumentPublished;
        bancoViewModel.OfficialDocumentDeleted += OnOfficialDocumentDeleted;
        bancoViewModel.OfficialDocumentMissing += OnOfficialDocumentMissing;
        bancoViewModel.DirectArticleMissing += OnDirectArticleMissing;
    }

    private void CloseTab(ShellWorkspaceTabViewModel tab)
    {
        if (!tab.CanClose || !_tabsByKey.Remove(tab.Key))
        {
            return;
        }

        var tabIndex = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);

        if (ReferenceEquals(ActiveTab, tab))
        {
            if (OpenTabs.Count == 0)
            {
                OpenDestination(BancoDestinationKey);
                return;
            }

            var nextIndex = Math.Clamp(tabIndex - 1, 0, OpenTabs.Count - 1);
            ActiveTab = OpenTabs[nextIndex];
        }
    }

    public void CloseWorkspaceTab(ShellWorkspaceTabViewModel tab)
    {
        CloseTab(tab);
    }

    public void OpenSettingsWorkspace()
    {
        OpenDestination(SettingsDestinationKey);
    }

    private void UpdateSidebarActiveState()
    {
        Sidebar.SetActiveDestination(ActiveTab?.DestinationKey ?? BancoDestinationKey);
    }

    private async Task PersistSidebarPreferencesAsync()
    {
        _currentSettings ??= await _configurationService.LoadAsync();
        _currentSettings.ShellUi.SidebarWidth = SidebarWidth;
        await _configurationService.SaveAsync(_currentSettings);
    }

    private void ScheduleSidebarSave()
    {
        _sidebarSaveCts?.Cancel();
        _sidebarSaveCts?.Dispose();

        var cancellationTokenSource = new CancellationTokenSource();
        _sidebarSaveCts = cancellationTokenSource;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, cancellationTokenSource.Token);
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    await PersistSidebarPreferencesAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Il resize continua: ignoriamo il salvataggio intermedio.
            }
        });
    }

    private sealed record ShellWorkspaceDescriptor(string WorkspaceKey, bool CanClose, Func<object> ResolveContent);
}
