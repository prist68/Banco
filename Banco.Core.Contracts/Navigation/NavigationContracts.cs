namespace Banco.Core.Contracts.Navigation;

public interface INavigationRegistry
{
    IReadOnlyList<NavigationMacroCategoryDefinition> GetMacroCategories();

    IReadOnlyList<NavigationDestinationDefinition> GetDestinations();

    IReadOnlyList<NavigationEntryDefinition> GetEntries();

    IReadOnlyList<NavigationEntryDefinition> SearchEntries(string query, int maxResults = 12);

    IReadOnlyList<NavigationEntryDefinition> SearchEntries(string query, string macroCategoryKey, int maxResults = 12);

    NavigationMacroCategoryDefinition? GetMacroCategory(string macroCategoryKey);

    NavigationDestinationDefinition? GetDestination(string destinationKey);

    NavigationEntryDefinition? GetEntry(string entryKey);
}

public sealed record NavigationMacroCategoryDefinition(
    string Key,
    string Title,
    string IconResourceKey,
    int RailOrder,
    string DefaultAccentColor,
    bool SupportsCustomization = true);

public sealed record NavigationDestinationDefinition(
    string Key,
    string Title,
    string WorkspaceKey,
    bool SupportsMultipleTabs = false,
    bool IsAvailable = true);

public sealed record NavigationEntryDefinition(
    string Key,
    string Title,
    string MacroCategoryKey,
    string? GroupKey,
    string? DestinationKey,
    NavigationEntryAvailability Availability,
    int ContextOrder,
    bool IsVisibleInShell,
    bool IsSearchable,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Aliases,
    bool ShowInContextPanel,
    string DefaultAccentColor,
    bool SupportsTargetOverride = false,
    bool SupportsVisibility = false,
    bool SupportsAccentCustomization = false,
    bool SupportsReorder = false,
    string? GroupTitle = null,
    string? InfoText = null);

public enum NavigationEntryAvailability
{
    Available = 0,
    DisabledPhase = 1,
    Informational = 2
}
