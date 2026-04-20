namespace Banco.Vendita.Configuration;

public sealed class SidebarMacroCategorySettings
{
    public string MacroCategoryKey { get; set; } = string.Empty;

    public int Order { get; set; }

    public string? AccentColor { get; set; }
}
