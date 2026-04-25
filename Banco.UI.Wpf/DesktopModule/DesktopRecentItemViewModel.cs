namespace Banco.UI.Wpf.DesktopModule;

public sealed class DesktopRecentItemViewModel
{
    public DesktopRecentItemViewModel(
        string timeLabel,
        string title,
        string detail,
        string stateLabel,
        string? destinationKey)
    {
        TimeLabel = timeLabel;
        Title = title;
        Detail = detail;
        StateLabel = stateLabel;
        DestinationKey = destinationKey;
    }

    public string TimeLabel { get; }

    public string Title { get; }

    public string Detail { get; }

    public string StateLabel { get; }

    public string? DestinationKey { get; }
}
