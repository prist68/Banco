namespace Banco.Vendita.Configuration;

public enum DesktopSurfaceItemKind
{
    Shortcut = 0,
    Clock = 1,
    Calendar = 2,
    Notes = 3
}

public sealed class DesktopSurfaceItemSettings
{
    public string ItemId { get; set; } = Guid.NewGuid().ToString("N");

    public DesktopSurfaceItemKind Kind { get; set; }

    public string? EntryKey { get; set; }

    public string? DestinationKey { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public string? AccentColor { get; set; }

    public string? IconData { get; set; }

    public double Left { get; set; }

    public double Top { get; set; }

    public string? NotesText { get; set; }

    public DateTime? CalendarDate { get; set; }
}
