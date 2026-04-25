using System.Collections.ObjectModel;
using System.Windows.Input;
using Banco.Core.Contracts.Navigation;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;

namespace Banco.UI.Wpf.DesktopModule;

public sealed class DesktopHomeViewModel : ViewModelBase
{
    private readonly INavigationRegistry _navigationRegistry;
    private readonly IApplicationConfigurationService _configurationService;
    private readonly Dictionary<string, NavigationEntryDefinition> _entryIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _persistLock = new(1, 1);
    private AppSettings? _currentSettings;
    private string? _lastDroppedEntryKey;
    private DateTime _lastDropUtc;
    private double _lastDropLeft;
    private double _lastDropTop;

    public DesktopHomeViewModel(
        INavigationRegistry navigationRegistry,
        IApplicationConfigurationService configurationService)
    {
        _navigationRegistry = navigationRegistry;
        _configurationService = configurationService;
        _configurationService.SettingsChanged += OnSettingsChanged;

        BuildShortcutPalette();
        _ = LoadAsync();
    }

    public event Action<string>? OpenDestinationRequested;

    public ObservableCollection<DesktopSurfaceItemViewModel> DesktopItems { get; } = [];

    public ObservableCollection<DesktopShortcutPaletteItemViewModel> AvailableShortcuts { get; } = [];

    public ObservableCollection<DesktopQuickActionViewModel> QuickActions { get; } = [];

    public ObservableCollection<DesktopStatusTileViewModel> StatusTiles { get; } = [];

    public ObservableCollection<DesktopAlertItemViewModel> OperationalAlerts { get; } = [];

    public ObservableCollection<DesktopRecentItemViewModel> RecentItems { get; } = [];

    public bool HasItems => DesktopItems.Count > 0;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
    }

    public void OpenItem(DesktopSurfaceItemViewModel item)
    {
        if (item.Kind != DesktopSurfaceItemKind.Shortcut || string.IsNullOrWhiteSpace(item.DestinationKey))
        {
            return;
        }

        OpenDestinationRequested?.Invoke(item.DestinationKey);
    }

    public void OpenDestination(string? destinationKey)
    {
        if (string.IsNullOrWhiteSpace(destinationKey))
        {
            return;
        }

        OpenDestinationRequested?.Invoke(destinationKey);
    }

    public void UpdateItemPosition(DesktopSurfaceItemViewModel item, double left, double top)
    {
        item.Left = Math.Max(16, left);
        item.Top = Math.Max(16, top);
    }

    public void AddShortcutFromEntry(string entryKey, double left, double top)
    {
        if (!_entryIndex.TryGetValue(entryKey, out var entry))
        {
            return;
        }

        var paletteItem = AvailableShortcuts.FirstOrDefault(item => string.Equals(item.EntryKey, entry.Key, StringComparison.OrdinalIgnoreCase));
        if (paletteItem is null)
        {
            return;
        }

        var (width, height) = GetItemSize(DesktopSurfaceItemKind.Shortcut);
        var normalizedLeft = Math.Max(16, left - width / 2);
        var normalizedTop = Math.Max(16, top - height / 2);
        var nowUtc = DateTime.UtcNow;
        if (string.Equals(_lastDroppedEntryKey, entryKey, StringComparison.OrdinalIgnoreCase)
            && (nowUtc - _lastDropUtc).TotalMilliseconds < 600
            && Math.Abs(normalizedLeft - _lastDropLeft) < 8
            && Math.Abs(normalizedTop - _lastDropTop) < 8)
        {
            return;
        }

        DesktopItems.Add(new DesktopSurfaceItemViewModel(
            Guid.NewGuid().ToString("N"),
            DesktopSurfaceItemKind.Shortcut,
            paletteItem.Title,
            paletteItem.SectionTitle,
            normalizedLeft,
            normalizedTop,
            width,
            height,
            paletteItem.AccentColor,
            paletteItem.IconData,
            paletteItem.DestinationKey,
            RemoveDesktopItem)
        {
            EntryKey = paletteItem.EntryKey
        });

        _lastDroppedEntryKey = entryKey;
        _lastDropUtc = nowUtc;
        _lastDropLeft = normalizedLeft;
        _lastDropTop = normalizedTop;

        NotifyPropertyChanged(nameof(HasItems));
        _ = PersistDesktopAsync();
    }

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        _currentSettings = e.Settings;
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _currentSettings = await _configurationService.LoadAsync(cancellationToken);
        var settings = _currentSettings.ShellUi.DesktopItems ?? [];

        DesktopItems.Clear();
        foreach (var itemSettings in settings.OrderBy(item => item.Top).ThenBy(item => item.Left))
        {
            var item = CreateItemFromSettings(itemSettings);
            if (item is not null)
            {
                DesktopItems.Add(item);
            }
        }

        NotifyPropertyChanged(nameof(HasItems));
    }

    private void BuildShortcutPalette()
    {
        AvailableShortcuts.Clear();
        _entryIndex.Clear();

        var entries = _navigationRegistry.GetEntries()
            .Where(entry => entry.IsVisibleInShell)
            .Where(entry => entry.Availability == NavigationEntryAvailability.Available)
            .Where(entry => entry.ShowInContextPanel)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DestinationKey))
            .Where(entry => !string.Equals(entry.DestinationKey, "dashboard.home", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => _navigationRegistry.GetMacroCategory(entry.MacroCategoryKey)?.RailOrder ?? int.MaxValue)
            .ThenBy(entry => entry.GroupTitle ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.ContextOrder)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entry in entries)
        {
            var macro = _navigationRegistry.GetMacroCategory(entry.MacroCategoryKey);
            if (macro is null)
            {
                continue;
            }

            _entryIndex[entry.Key] = entry;
            AvailableShortcuts.Add(new DesktopShortcutPaletteItemViewModel(
                entry.Key,
                entry.Title,
                entry.DestinationKey!,
                macro.Title,
                entry.GroupTitle ?? macro.Title,
                ResolveIconData(entry, macro),
                entry.DefaultAccentColor));
        }

        BuildOperationalSections();
    }

    private void BuildOperationalSections()
    {
        QuickActions.Clear();
        QuickActions.Add(new DesktopQuickActionViewModel("Nuova vendita", "Banco operativo", IconCartData, "#4F86DC", "banco.vendita"));
        QuickActions.Add(new DesktopQuickActionViewModel("Documenti", "Lista vendite", IconDocumentData, "#C47F1F", "documenti.lista"));
        QuickActions.Add(new DesktopQuickActionViewModel("Gestione articolo", "Scheda FM-like", IconArticleData, "#4E748F", "magazzino.gestione-articolo"));
        QuickActions.Add(new DesktopQuickActionViewModel("Lista riordino", "Fabbisogni aperti", IconFolderData, "#4E748F", "magazzino.riordino"));
        QuickActions.Add(new DesktopQuickActionViewModel("Clienti e punti", "Fidelity", IconPointsData, "#3FA06E", "anagrafiche.punti"));
        QuickActions.Add(new DesktopQuickActionViewModel("Stampa", "FastReport POS80", IconPrintData, "#7059B8", "impostazioni.fastreport"));

        StatusTiles.Clear();
        StatusTiles.Add(new DesktopStatusTileViewModel("Vendite oggi", "-", "In attesa del riepilogo reale", "Dati", "#4F86DC"));
        StatusTiles.Add(new DesktopStatusTileViewModel("Cortesie aperte", "-", "Documenti non fiscalizzati", "Controllo", "#C47F1F"));
        StatusTiles.Add(new DesktopStatusTileViewModel("Sospesi", "-", "Da verificare in Documenti", "Banco", "#B55454"));
        StatusTiles.Add(new DesktopStatusTileViewModel("DB", "db_diltech", "Connessione configurata", "Legacy", "#3FA06E"));
        StatusTiles.Add(new DesktopStatusTileViewModel("POS", "Nexi", "Stato operativo sintetico", "Pagamenti", "#4E748F"));
        StatusTiles.Add(new DesktopStatusTileViewModel("Fiscale", "WinEcr", "Registratore separato", "Fiscale", "#7059B8"));

        OperationalAlerts.Clear();
        OperationalAlerts.Add(new DesktopAlertItemViewModel("F.E. disponibili", "Slot pronto per segnalare fatture elettroniche o notifiche future.", "Predisposto", "#7059B8", null));
        OperationalAlerts.Add(new DesktopAlertItemViewModel("Documenti non scontrinati", "Apri Documenti per controllare Cortesia, Salva e sospesi recuperabili.", "Da controllare", "#C47F1F", "documenti.lista"));
        OperationalAlerts.Add(new DesktopAlertItemViewModel("Backup", "Verifica ultima copia e pianificazione archivio.", "Archivio", "#3FA06E", "impostazioni.archivio"));

        RecentItems.Clear();
        RecentItems.Add(new DesktopRecentItemViewModel("--:--", "Ultimi documenti", "Area pronta per mostrare aperture recenti reali.", "Predisposto", "documenti.lista"));
        RecentItems.Add(new DesktopRecentItemViewModel("--:--", "Ultimi articoli", "Area pronta per gli ultimi articoli aperti o modificati.", "Predisposto", "magazzino.gestione-articolo"));
        RecentItems.Add(new DesktopRecentItemViewModel("--:--", "Ultimi clienti", "Area pronta per richiami cliente e fidelity.", "Predisposto", "anagrafiche.punti"));
    }

    private DesktopSurfaceItemViewModel? CreateItemFromSettings(DesktopSurfaceItemSettings settings)
    {
        return settings.Kind switch
        {
            DesktopSurfaceItemKind.Shortcut => CreateShortcutItem(settings),
            _ => null
        };
    }

    private DesktopSurfaceItemViewModel? CreateShortcutItem(DesktopSurfaceItemSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.EntryKey) && _entryIndex.TryGetValue(settings.EntryKey, out var entry))
        {
            var paletteItem = AvailableShortcuts.FirstOrDefault(item => string.Equals(item.EntryKey, entry.Key, StringComparison.OrdinalIgnoreCase));
            if (paletteItem is null)
            {
                return null;
            }

            var (width, height) = GetItemSize(DesktopSurfaceItemKind.Shortcut);
            return new DesktopSurfaceItemViewModel(
                settings.ItemId,
                DesktopSurfaceItemKind.Shortcut,
                paletteItem.Title,
                paletteItem.SectionTitle,
                settings.Left,
                settings.Top,
                width,
                height,
                paletteItem.AccentColor,
                paletteItem.IconData,
                paletteItem.DestinationKey,
                RemoveDesktopItem)
            {
                EntryKey = paletteItem.EntryKey
            };
        }

        if (string.IsNullOrWhiteSpace(settings.DestinationKey) || string.IsNullOrWhiteSpace(settings.Title))
        {
            return null;
        }

        var (fallbackWidth, fallbackHeight) = GetItemSize(DesktopSurfaceItemKind.Shortcut);
        return new DesktopSurfaceItemViewModel(
            settings.ItemId,
            DesktopSurfaceItemKind.Shortcut,
            settings.Title,
            settings.Subtitle ?? string.Empty,
            settings.Left,
            settings.Top,
            fallbackWidth,
            fallbackHeight,
            settings.AccentColor ?? "#4F86DC",
            settings.IconData ?? "M3,13H5V11H3V13M3,17H5V15H3V17M3,9H5V7H3V9M7,13H21V11H7V13M7,17H21V15H7V17M7,9V11H21V9H7Z",
            settings.DestinationKey,
            RemoveDesktopItem)
        {
            EntryKey = settings.EntryKey
        };
    }

    private void RemoveDesktopItem(DesktopSurfaceItemViewModel item)
    {
        if (!DesktopItems.Remove(item))
        {
            return;
        }

        NotifyPropertyChanged(nameof(HasItems));
        _ = PersistDesktopAsync();
    }

    private async Task PersistDesktopAsync(CancellationToken cancellationToken = default)
    {
        await _persistLock.WaitAsync(cancellationToken);
        try
        {
            var latestSettings = await _configurationService.LoadAsync(cancellationToken);
            latestSettings.ShellUi.DesktopItems = DesktopItems
                .Select(item => new DesktopSurfaceItemSettings
                {
                    ItemId = item.ItemId,
                    Kind = item.Kind,
                    EntryKey = item.EntryKey,
                    DestinationKey = item.Kind == DesktopSurfaceItemKind.Shortcut ? item.DestinationKey : null,
                    Title = item.Title,
                    Subtitle = item.Subtitle,
                    AccentColor = item.AccentColor,
                    IconData = item.IconData,
                    Left = item.Left,
                    Top = item.Top
                })
                .ToList();

            await _configurationService.SaveAsync(latestSettings, cancellationToken);
            _currentSettings = latestSettings;
        }
        finally
        {
            _persistLock.Release();
        }
    }

    private static (double Width, double Height) GetItemSize(DesktopSurfaceItemKind kind)
    {
        return kind switch
        {
            DesktopSurfaceItemKind.Shortcut => (116, 120),
            _ => (128, 128)
        };
    }

    private const string IconCartData = "M17,18A2,2 0 0,1 19,20A2,2 0 0,1 17,22A2,2 0 0,1 15,20A2,2 0 0,1 17,18M1,2H4.27L5.21,4H20A1,1 0 0,1 21,5C21,5.17 20.95,5.34 20.88,5.5L17.3,11.97C16.96,12.58 16.3,13 15.55,13H8.1L7.2,14.63L7.17,14.75A0.25,0.25 0 0,0 7.42,15H19V17H7C5.89,17 5,16.1 5,15C5,14.65 5.09,14.32 5.24,14.04L6.6,11.59L3,4H1V2Z";

    private const string IconDocumentData = "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M6,4H13V9H18V20H6V4M8,12V14H16V12H8M8,16V18H13V16H8Z";

    private const string IconArticleData = "M4,2H20A2,2 0 0,1 22,4V16A2,2 0 0,1 20,18H16L12,22L8,18H4A2,2 0 0,1 2,16V4A2,2 0 0,1 4,2M6,7H18V9H6V7M6,11H16V13H6V11Z";

    private const string IconFolderData = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z";

    private const string IconPointsData = "M12,2L15,8H22L16.5,12L18.8,19L12,14.8L5.2,19L7.5,12L2,8H9Z";

    private const string IconPrintData = "M18,3H6V7H18M19,12A1,1 0 0,1 18,11A1,1 0 0,1 19,10A1,1 0 0,1 20,11A1,1 0 0,1 19,12M16,19H8V14H16M19,8H5A3,3 0 0,0 2,11V17H6V21H18V17H22V11A3,3 0 0,0 19,8Z";

    private static string ResolveIconData(NavigationEntryDefinition entry, NavigationMacroCategoryDefinition macroCategory)
    {
        return entry.DestinationKey switch
        {
            "banco.vendita" => "M3,13H5V11H3V13M3,17H5V15H3V17M3,9H5V7H3V9M7,13H21V11H7V13M7,17H21V15H7V17M7,9V11H21V9H7Z",
            "documenti.lista" => "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20M12,19L8,15H10.5V12H13.5V15H16L12,19Z",
            "magazzino.riordino" => "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z",
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
}

public sealed class DesktopShortcutPaletteItemViewModel
{
    public DesktopShortcutPaletteItemViewModel(
        string entryKey,
        string title,
        string destinationKey,
        string categoryTitle,
        string sectionTitle,
        string iconData,
        string accentColor)
    {
        EntryKey = entryKey;
        Title = title;
        DestinationKey = destinationKey;
        CategoryTitle = categoryTitle;
        SectionTitle = sectionTitle;
        IconData = iconData;
        AccentColor = accentColor;
    }

    public string EntryKey { get; }

    public string Title { get; }

    public string DestinationKey { get; }

    public string CategoryTitle { get; }

    public string SectionTitle { get; }

    public string IconData { get; }

    public string AccentColor { get; }

    public string DisplayLabel => $"{CategoryTitle} / {Title}";
}

public sealed class DesktopSurfaceItemViewModel : ViewModelBase
{
    private double _left;
    private double _top;

    public DesktopSurfaceItemViewModel(
        string itemId,
        DesktopSurfaceItemKind kind,
        string title,
        string subtitle,
        double left,
        double top,
        double width,
        double height,
        string accentColor,
        string iconData,
        string? destinationKey,
        Action<DesktopSurfaceItemViewModel> removeAction)
    {
        ItemId = itemId;
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        _left = left;
        _top = top;
        Width = width;
        Height = height;
        AccentColor = accentColor;
        IconData = iconData;
        DestinationKey = destinationKey;
        RemoveCommand = new RelayCommand(() => removeAction(this));
    }

    public string ItemId { get; }

    public DesktopSurfaceItemKind Kind { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public double Width { get; }

    public double Height { get; }

    public string AccentColor { get; }

    public string IconData { get; }

    public string? DestinationKey { get; }

    public string? EntryKey { get; init; }

    public ICommand RemoveCommand { get; }

    public bool IsShortcut => Kind == DesktopSurfaceItemKind.Shortcut;

    public double Left
    {
        get => _left;
        set => SetProperty(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => SetProperty(ref _top, value);
    }
}
