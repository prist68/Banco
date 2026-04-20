namespace Banco.Vendita.Configuration;

public sealed class ShellUiSettings
{
    public double SidebarWidth { get; set; } = 286;

    public Dictionary<string, bool> NavigationExpandedStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string SelectedMacroCategoryKey { get; set; } = "banco";

    public List<SidebarMacroCategorySettings> SidebarMacroCategories { get; set; } = [];

    public List<SidebarShortcutSettings> SidebarShortcuts { get; set; } = [];
}
