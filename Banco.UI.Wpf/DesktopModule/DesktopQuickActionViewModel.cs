namespace Banco.UI.Wpf.DesktopModule;

public sealed class DesktopQuickActionViewModel
{
    public DesktopQuickActionViewModel(
        string title,
        string subtitle,
        string iconData,
        string accentColor,
        string destinationKey)
    {
        Title = title;
        Subtitle = subtitle;
        IconData = iconData;
        AccentColor = accentColor;
        DestinationKey = destinationKey;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string IconData { get; }

    public string AccentColor { get; }

    public string DestinationKey { get; }
}
