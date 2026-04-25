namespace Banco.Vendita.Configuration;

public sealed class ShellUiSettings
{
    public double SidebarWidth { get; set; } = 286;

    public string StartupDestinationKey { get; set; } = "dashboard.home";

    public string DashboardNotes { get; set; } = string.Empty;

    public List<DesktopSurfaceItemSettings> DesktopItems { get; set; } = [];

    public Dictionary<string, bool> NavigationExpandedStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string SelectedMacroCategoryKey { get; set; } = "dashboard";

    public List<SidebarMacroCategorySettings> SidebarMacroCategories { get; set; } = [];

    public List<SidebarShortcutSettings> SidebarShortcuts { get; set; } = [];
}
