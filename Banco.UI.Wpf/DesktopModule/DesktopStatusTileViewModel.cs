namespace Banco.UI.Wpf.DesktopModule;

public sealed class DesktopStatusTileViewModel
{
    public DesktopStatusTileViewModel(string title, string value, string detail, string stateLabel, string accentColor)
    {
        Title = title;
        Value = value;
        Detail = detail;
        StateLabel = stateLabel;
        AccentColor = accentColor;
    }

    public string Title { get; }

    public string Value { get; }

    public string Detail { get; }

    public string StateLabel { get; }

    public string AccentColor { get; }
}
