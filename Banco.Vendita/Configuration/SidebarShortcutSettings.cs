namespace Banco.Vendita.Configuration;

public sealed class SidebarShortcutSettings
{
    public string EntryKey { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool IsVisible { get; set; } = true;

    public string? AccentColor { get; set; }

    public string? TargetKey { get; set; }
}
