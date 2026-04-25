namespace Banco.UI.Wpf.DesktopModule;

public sealed class DesktopAlertItemViewModel
{
    public DesktopAlertItemViewModel(
        string title,
        string detail,
        string stateLabel,
        string accentColor,
        string? destinationKey)
    {
        Title = title;
        Detail = detail;
        StateLabel = stateLabel;
        AccentColor = accentColor;
        DestinationKey = destinationKey;
    }

    public string Title { get; }

    public string Detail { get; }

    public string StateLabel { get; }

    public string AccentColor { get; }

    public string? DestinationKey { get; }

    public bool CanOpen => !string.IsNullOrWhiteSpace(DestinationKey);
}
