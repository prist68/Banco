using System.Collections.ObjectModel;
using System.Windows;
using Banco.Core.Contracts.Navigation;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;

namespace Banco.Sidebar.ViewModels;

public sealed class SidebarHostViewModel : ViewModelBase
{
    private static readonly string[] AccentPalette =
    [
        "#4F86DC",
        "#3FA06E",
        "#C47F1F",
        "#B55454",
        "#7059B8",
        "#4E748F"
    ];

    private readonly IApplicationConfigurationService _configurationService;
    private readonly INavigationRegistry _navigationRegistry;
    private readonly Dictionary<string, NavigationMacroCategoryDefinition> _macroDefinitions;
    private readonly Dictionary<string, NavigationDestinationDefinition> _destinationDefinitions;
    private readonly Dictionary<string, NavigationEntryDefinition> _entryDefinitions;
    private readonly Dictionary<string, SidebarMacroCategorySettings> _macroOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SidebarShortcutSettings> _shortcutOverrides = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings? _currentSettings;
    private bool _isApplyingSettings;
    private bool _isContextPanelOpen;
    private string _searchText = string.Empty;
    private string? _highlightedEntryKey;
    private SidebarMacroCategoryViewModel? _selectedMacroCategory;

    public SidebarHostViewModel(IApplicationConfigurationService configurationService, INavigationRegistry navigationRegistry)
    {
        _configurationService = configurationService;
        _navigationRegistry = navigationRegistry;
        _macroDefinitions = _navigationRegistry.GetMacroCategories().ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        _destinationDefinitions = _navigationRegistry.GetDestinations().ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        _entryDefinitions = _navigationRegistry.GetEntries().ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        CustomizeSidebarCommand = new RelayCommand(OpenCustomizationWindow);
        ResetCustomizationCommand = new RelayCommand(ResetCustomization);
        _configurationService.SettingsChanged += OnSettingsChanged;
    }

    public event EventHandler<SidebarNavigateRequestedEventArgs>? NavigateRequested;

    public ObservableCollection<SidebarMacroCategoryViewModel> MacroCategories { get; } = [];

    public ObservableCollection<SidebarSearchResultViewModel> SearchResults { get; } = [];

    public ObservableCollection<SidebarCustomizationEntryViewModel> CustomizableEntries { get; } = [];

    public IReadOnlyList<NavigationDestinationDefinition> AvailableDestinations =>
        _navigationRegistry.GetDestinations().Where(destination => destination.IsAvailable).OrderBy(destination => destination.Title).ToList();

    public IReadOnlyList<string> AvailableAccentColors => AccentPalette;

    public RelayCommand CustomizeSidebarCommand { get; }

    public RelayCommand ResetCustomizationCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RebuildSearchResults();
            }
        }
    }

    public bool IsContextPanelVisible => _isContextPanelOpen && SelectedMacroCategory?.Groups.Count > 0;

    public SidebarMacroCategoryViewModel? SelectedMacroCategory
    {
        get => _selectedMacroCategory;
        private set
        {
            if (SetProperty(ref _selectedMacroCategory, value))
            {
                foreach (var macroCategory in MacroCategories)
                {
                    macroCategory.IsActive = ReferenceEquals(macroCategory, value);
                }

                NotifyPropertyChanged(nameof(SelectedGroups));
                NotifyPropertyChanged(nameof(HasSelectedGroups));
                NotifyPropertyChanged(nameof(IsContextPanelVisible));
                NotifyPropertyChanged(nameof(EmptyCategoryMessage));
                RebuildSearchResults();
            }
        }
    }

    public IEnumerable<SidebarSectionGroupViewModel> SelectedGroups =>
        SelectedMacroCategory?.Groups ?? Enumerable.Empty<SidebarSectionGroupViewModel>();

    public bool HasSelectedGroups => SelectedMacroCategory?.Groups.Count > 0;

    public string EmptyCategoryMessage =>
        SelectedMacroCategory is null
            ? "Seleziona una sezione principale."
            : $"La sezione {SelectedMacroCategory.Title} verra` ampliata nei prossimi step.";

    public async Task InitializeAsync()
    {
        await ReloadFromSettingsAsync(await _configurationService.LoadAsync());
    }

    public async Task ReloadFromSettingsAsync(AppSettings settings)
    {
        _currentSettings = settings;
        _macroOverrides.Clear();
        _shortcutOverrides.Clear();

        foreach (var overrideItem in settings.ShellUi.SidebarMacroCategories)
        {
            if (!string.IsNullOrWhiteSpace(overrideItem.MacroCategoryKey))
            {
                _macroOverrides[overrideItem.MacroCategoryKey] = overrideItem;
            }
        }

        foreach (var overrideItem in settings.ShellUi.SidebarShortcuts)
        {
            if (!string.IsNullOrWhiteSpace(overrideItem.EntryKey))
            {
                _shortcutOverrides[overrideItem.EntryKey] = overrideItem;
            }
        }

        RebuildNavigationModel();
        var selectedKey = settings.ShellUi.SelectedMacroCategoryKey;
        var selectedCategory = MacroCategories.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
            ?? MacroCategories.FirstOrDefault();
        if (selectedCategory is not null)
        {
            ActivateMacroCategory(selectedCategory, persistSelection: false);
        }

        RebuildSearchResults();
        await Task.CompletedTask;
    }

    public void OpenContextPanelForMacro(string macroCategoryKey)
    {
        var macroCategory = MacroCategories.FirstOrDefault(item => string.Equals(item.Key, macroCategoryKey, StringComparison.OrdinalIgnoreCase));
        if (macroCategory is null)
        {
            return;
        }

        ActivateMacroCategory(macroCategory, persistSelection: false);
        SetContextPanelOpen(true);
    }

    public void KeepContextPanelOpen()
    {
        if (SelectedMacroCategory?.Groups.Count > 0)
        {
            SetContextPanelOpen(true);
        }
    }

    public void CloseContextPanel()
    {
        SetContextPanelOpen(false);
    }

    public void SetActiveDestination(string destinationKey)
    {
        var activeEntry = MacroCategories
            .SelectMany(category => category.Groups)
            .SelectMany(group => group.Items)
            .FirstOrDefault(item => string.Equals(item.DestinationKey, destinationKey, StringComparison.OrdinalIgnoreCase) && item.IsEnabled);

        foreach (var item in MacroCategories.SelectMany(category => category.Groups).SelectMany(group => group.Items))
        {
            item.IsActive = ReferenceEquals(item, activeEntry);
            item.IsHighlighted = string.Equals(item.EntryKey, _highlightedEntryKey, StringComparison.OrdinalIgnoreCase);
        }

        if (activeEntry is null)
        {
            return;
        }

        var macroCategory = MacroCategories.FirstOrDefault(item => string.Equals(item.Key, activeEntry.MacroCategoryKey, StringComparison.OrdinalIgnoreCase));
        if (macroCategory is not null)
        {
            ActivateMacroCategory(macroCategory, persistSelection: false);
        }
    }

    public string ResolveDestinationTitle(string destinationKey)
    {
        return _destinationDefinitions.TryGetValue(destinationKey, out var destination)
            ? destination.Title
            : destinationKey;
    }

    public void MoveMacroCategory(SidebarMacroCategoryViewModel macroCategory, int offset)
    {
        var index = MacroCategories.IndexOf(macroCategory);
        var destinationIndex = index + offset;
        if (index < 0 || destinationIndex < 0 || destinationIndex >= MacroCategories.Count)
        {
            return;
        }

        MacroCategories.Move(index, destinationIndex);
        for (var currentIndex = 0; currentIndex < MacroCategories.Count; currentIndex++)
        {
            var overrideItem = GetOrCreateMacroOverride(MacroCategories[currentIndex].Key);
            overrideItem.Order = currentIndex;
            overrideItem.AccentColor = MacroCategories[currentIndex].AccentColor;
        }

        _ = PersistSidebarPreferencesAsync();
    }

    public void MoveShortcut(SidebarCustomizationEntryViewModel shortcut, int offset)
    {
        var siblings = CustomizableEntries
            .Where(item => string.Equals(item.MacroCategoryKey, shortcut.MacroCategoryKey, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(item.GroupTitle, shortcut.GroupTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var index = siblings.IndexOf(shortcut);
        var destinationIndex = index + offset;
        if (index < 0 || destinationIndex < 0 || destinationIndex >= siblings.Count)
        {
            return;
        }

        var originCollectionIndex = CustomizableEntries.IndexOf(shortcut);
        var destinationCollectionIndex = CustomizableEntries.IndexOf(siblings[destinationIndex]);
        CustomizableEntries.Move(originCollectionIndex, destinationCollectionIndex);

        for (var currentIndex = 0; currentIndex < CustomizableEntries.Count; currentIndex++)
        {
            var overrideItem = GetOrCreateShortcutOverride(CustomizableEntries[currentIndex].EntryKey);
            overrideItem.Order = currentIndex;
        }

        RebuildNavigationModel();
        _ = PersistSidebarPreferencesAsync();
    }

    public void ApplyCustomization(SidebarCustomizationEntryViewModel customizationEntry)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        var overrideItem = GetOrCreateShortcutOverride(customizationEntry.EntryKey);
        if (customizationEntry.SupportsTargetOverride)
        {
            overrideItem.TargetKey = customizationEntry.DestinationKey;
        }

        overrideItem.IsVisible = customizationEntry.SupportsVisibility ? customizationEntry.IsVisible : true;
        if (customizationEntry.SupportsAccentCustomization)
        {
            overrideItem.AccentColor = customizationEntry.AccentColor;
        }

        RebuildNavigationModel();
        _ = PersistSidebarPreferencesAsync();
    }

    public void OpenSearchResult(SidebarSearchResultViewModel result)
    {
        _highlightedEntryKey = result.EntryKey;
        var category = MacroCategories.FirstOrDefault(item => string.Equals(item.Key, result.MacroCategoryKey, StringComparison.OrdinalIgnoreCase));
        if (category is not null)
        {
            ActivateMacroCategory(category, persistSelection: true);
            SetContextPanelOpen(true);
        }

        SetActiveDestination(result.DestinationKey);
        NavigateRequested?.Invoke(this, new SidebarNavigateRequestedEventArgs(result.DestinationKey, result.MacroCategoryKey, result.EntryKey));
    }

    public void UpdateMacroAccent(string macroCategoryKey, string accentColor)
    {
        var overrideItem = GetOrCreateMacroOverride(macroCategoryKey);
        overrideItem.AccentColor = accentColor;
        var macroCategory = MacroCategories.FirstOrDefault(item => string.Equals(item.Key, macroCategoryKey, StringComparison.OrdinalIgnoreCase));
        if (macroCategory is not null)
        {
            macroCategory.AccentColor = accentColor;
        }

        _ = PersistSidebarPreferencesAsync();
    }

    private SidebarMacroCategoryViewModel CreateMacroCategory(NavigationMacroCategoryDefinition definition)
    {
        return new SidebarMacroCategoryViewModel(
            definition.Key,
            definition.Title,
            definition.IconResourceKey,
            definition.DefaultAccentColor,
            item => ActivateMacroCategory(item, persistSelection: true));
    }

    private void ActivateMacroCategory(SidebarMacroCategoryViewModel macroCategory, bool persistSelection)
    {
        SelectedMacroCategory = macroCategory;
        if (persistSelection)
        {
            _ = PersistSidebarPreferencesAsync();
        }
    }

    private void SetContextPanelOpen(bool isOpen)
    {
        if (_isContextPanelOpen == isOpen)
        {
            return;
        }

        _isContextPanelOpen = isOpen;
        NotifyPropertyChanged(nameof(IsContextPanelVisible));
    }

    private void RebuildNavigationModel()
    {
        _isApplyingSettings = true;
        try
        {
            var selectedKey = SelectedMacroCategory?.Key;
            MacroCategories.Clear();

            foreach (var definition in _navigationRegistry.GetMacroCategories().OrderBy(GetMacroOrder).ThenBy(item => item.RailOrder))
            {
                var macroCategory = CreateMacroCategory(definition);
                if (_macroOverrides.TryGetValue(definition.Key, out var macroOverride) && !string.IsNullOrWhiteSpace(macroOverride.AccentColor))
                {
                    macroCategory.AccentColor = macroOverride.AccentColor!;
                }

                foreach (var group in BuildGroupsForMacro(definition.Key))
                {
                    macroCategory.Groups.Add(group);
                }

                MacroCategories.Add(macroCategory);
            }

            RebuildCustomizationEntries();
            if (!string.IsNullOrWhiteSpace(selectedKey))
            {
                SelectedMacroCategory = MacroCategories.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
                    ?? MacroCategories.FirstOrDefault();
            }
        }
        finally
        {
            _isApplyingSettings = false;
            NotifyPropertyChanged(nameof(SelectedGroups));
            NotifyPropertyChanged(nameof(HasSelectedGroups));
            NotifyPropertyChanged(nameof(IsContextPanelVisible));
            NotifyPropertyChanged(nameof(EmptyCategoryMessage));
        }
    }

    private IEnumerable<SidebarSectionGroupViewModel> BuildGroupsForMacro(string macroCategoryKey)
    {
        return _entryDefinitions.Values
            .Where(entry => string.Equals(entry.MacroCategoryKey, macroCategoryKey, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.ShowInContextPanel)
            .OrderBy(GetShortcutOrder)
            .ThenBy(entry => entry.ContextOrder)
            .GroupBy(entry => new { entry.GroupKey, entry.GroupTitle })
            .Select(group =>
            {
                var viewModel = new SidebarSectionGroupViewModel(group.Key.GroupKey!, group.Key.GroupTitle!);
                foreach (var entry in group)
                {
                    var shortcut = CreateShortcut(entry);
                    if (shortcut.IsVisible || shortcut.IsInformational)
                    {
                        viewModel.Items.Add(shortcut);
                    }
                }

                return viewModel;
            })
            .Where(group => group.Items.Count > 0);
    }

    private SidebarShortcutItemViewModel CreateShortcut(NavigationEntryDefinition entry)
    {
        var shortcutOverride = _shortcutOverrides.TryGetValue(entry.Key, out var overrideItem)
            ? overrideItem
            : null;
        var destinationKey = ResolveEffectiveDestinationKey(entry, shortcutOverride);
        var destination = !string.IsNullOrWhiteSpace(destinationKey) && _destinationDefinitions.TryGetValue(destinationKey, out var destinationDefinition)
            ? destinationDefinition
            : null;
        var accentColor = !string.IsNullOrWhiteSpace(shortcutOverride?.AccentColor)
            ? shortcutOverride!.AccentColor!
            : entry.DefaultAccentColor;
        var item = new SidebarShortcutItemViewModel(
            entry.Key,
            entry.Title,
            entry.GroupKey ?? string.Empty,
            entry.GroupTitle ?? string.Empty,
            entry.MacroCategoryKey,
            destinationKey,
            entry.Availability == NavigationEntryAvailability.Available && destination?.IsAvailable != false,
            entry.Availability == NavigationEntryAvailability.Informational,
            accentColor,
            entry.InfoText,
            OnShortcutOpen)
        {
            IsVisible = !entry.SupportsVisibility || shortcutOverride?.IsVisible != false
        };

        var displayTitle = entry.SupportsTargetOverride && destination is not null && !string.Equals(entry.DestinationKey, destination.Key, StringComparison.OrdinalIgnoreCase)
            ? destination.Title
            : entry.Title;
        item.ApplyDestination(destinationKey, displayTitle);
        return item;
    }

    private void OnShortcutOpen(SidebarShortcutItemViewModel item)
    {
        if (!item.IsEnabled || string.IsNullOrWhiteSpace(item.DestinationKey))
        {
            return;
        }

        _highlightedEntryKey = item.EntryKey;
        SetActiveDestination(item.DestinationKey);
        NavigateRequested?.Invoke(this, new SidebarNavigateRequestedEventArgs(item.DestinationKey, item.MacroCategoryKey, item.EntryKey));
    }

    private void RebuildSearchResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        foreach (var entry in _navigationRegistry.SearchEntries(SearchText.Trim(), 12))
        {
            var shortcutOverride = _shortcutOverrides.TryGetValue(entry.Key, out var overrideItem)
                ? overrideItem
                : null;
            if (shortcutOverride?.IsVisible == false)
            {
                continue;
            }

            var destinationKey = ResolveEffectiveDestinationKey(entry, shortcutOverride);
            if (string.IsNullOrWhiteSpace(destinationKey) || !_destinationDefinitions.TryGetValue(destinationKey, out var destination))
            {
                continue;
            }

            var title = entry.SupportsTargetOverride && !string.Equals(entry.DestinationKey, destination.Key, StringComparison.OrdinalIgnoreCase)
                ? destination.Title
                : entry.Title;
            var subtitle = string.IsNullOrWhiteSpace(entry.GroupTitle)
                ? ResolveMacroTitle(entry.MacroCategoryKey)
                : $"{ResolveMacroTitle(entry.MacroCategoryKey)} / {entry.GroupTitle}";
            SearchResults.Add(new SidebarSearchResultViewModel(destination.Key, title, subtitle, entry.MacroCategoryKey, entry.Key));
        }
    }

    private void RebuildCustomizationEntries()
    {
        CustomizableEntries.Clear();
        foreach (var entry in _entryDefinitions.Values.OrderBy(item => GetMacroOrder(_macroDefinitions[item.MacroCategoryKey])).ThenBy(GetShortcutOrder).ThenBy(item => item.ContextOrder))
        {
            if (!entry.SupportsTargetOverride && !entry.SupportsVisibility && !entry.SupportsAccentCustomization && !entry.SupportsReorder)
            {
                continue;
            }

            var shortcutOverride = _shortcutOverrides.TryGetValue(entry.Key, out var overrideItem)
                ? overrideItem
                : null;
            var destinationKey = ResolveEffectiveDestinationKey(entry, shortcutOverride) ?? string.Empty;
            var accentColor = !string.IsNullOrWhiteSpace(shortcutOverride?.AccentColor)
                ? shortcutOverride!.AccentColor!
                : entry.DefaultAccentColor;
            var isVisible = shortcutOverride?.IsVisible ?? true;
            CustomizableEntries.Add(new SidebarCustomizationEntryViewModel(
                this,
                entry.Key,
                entry.Title,
                entry.MacroCategoryKey,
                entry.GroupTitle ?? string.Empty,
                destinationKey,
                isVisible,
                accentColor,
                entry.SupportsTargetOverride,
                entry.SupportsVisibility,
                entry.SupportsAccentCustomization,
                entry.SupportsReorder));
        }
    }

    private string ResolveMacroTitle(string macroCategoryKey)
    {
        return _macroDefinitions.TryGetValue(macroCategoryKey, out var macroDefinition)
            ? macroDefinition.Title
            : macroCategoryKey;
    }

    private string? ResolveEffectiveDestinationKey(NavigationEntryDefinition entry, SidebarShortcutSettings? shortcutOverride)
    {
        if (entry.SupportsTargetOverride && !string.IsNullOrWhiteSpace(shortcutOverride?.TargetKey))
        {
            return shortcutOverride.TargetKey;
        }

        return entry.DestinationKey;
    }

    private int GetMacroOrder(NavigationMacroCategoryDefinition definition)
    {
        return _macroOverrides.TryGetValue(definition.Key, out var macroOverride)
            ? macroOverride.Order
            : definition.RailOrder;
    }

    private int GetShortcutOrder(NavigationEntryDefinition definition)
    {
        return _shortcutOverrides.TryGetValue(definition.Key, out var shortcutOverride)
            ? shortcutOverride.Order
            : definition.ContextOrder;
    }

    private SidebarMacroCategorySettings GetOrCreateMacroOverride(string macroCategoryKey)
    {
        if (_macroOverrides.TryGetValue(macroCategoryKey, out var existing))
        {
            return existing;
        }

        var created = new SidebarMacroCategorySettings { MacroCategoryKey = macroCategoryKey };
        _macroOverrides[macroCategoryKey] = created;
        return created;
    }

    private SidebarShortcutSettings GetOrCreateShortcutOverride(string entryKey)
    {
        if (_shortcutOverrides.TryGetValue(entryKey, out var existing))
        {
            return existing;
        }

        var created = new SidebarShortcutSettings { EntryKey = entryKey };
        _shortcutOverrides[entryKey] = created;
        return created;
    }

    private async Task PersistSidebarPreferencesAsync()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _currentSettings ??= await _configurationService.LoadAsync();
        _currentSettings.ShellUi.SelectedMacroCategoryKey = SelectedMacroCategory?.Key ?? _currentSettings.ShellUi.SelectedMacroCategoryKey;
        _currentSettings.ShellUi.SidebarMacroCategories = _macroOverrides.Values.OrderBy(item => item.Order).ToList();
        _currentSettings.ShellUi.SidebarShortcuts = _shortcutOverrides.Values.OrderBy(item => item.Order).ThenBy(item => item.EntryKey).ToList();
        await _configurationService.SaveAsync(_currentSettings);
    }

    private void OnSettingsChanged(object? sender, ApplicationConfigurationChangedEventArgs e)
    {
        _ = ReloadFromSettingsAsync(e.Settings);
    }

    private void OpenCustomizationWindow()
    {
        var window = new Views.SidebarCustomizationWindow
        {
            Owner = Application.Current?.MainWindow,
            DataContext = this
        };
        window.ShowDialog();
    }

    private void ResetCustomization()
    {
        _macroOverrides.Clear();
        _shortcutOverrides.Clear();
        RebuildNavigationModel();
        _ = PersistSidebarPreferencesAsync();
    }
}
