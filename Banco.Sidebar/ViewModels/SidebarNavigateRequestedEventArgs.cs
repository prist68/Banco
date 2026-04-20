namespace Banco.Sidebar.ViewModels;

public sealed class SidebarNavigateRequestedEventArgs : EventArgs
{
    public SidebarNavigateRequestedEventArgs(string destinationKey, string macroCategoryKey, string? entryKey = null)
    {
        DestinationKey = destinationKey;
        MacroCategoryKey = macroCategoryKey;
        EntryKey = entryKey;
    }

    public string DestinationKey { get; }

    public string MacroCategoryKey { get; }

    public string? EntryKey { get; }
}
