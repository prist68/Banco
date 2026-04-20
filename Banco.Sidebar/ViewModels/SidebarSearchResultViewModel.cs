namespace Banco.Sidebar.ViewModels;

public sealed class SidebarSearchResultViewModel : ViewModelBase
{
    private bool _isSelected;

    public SidebarSearchResultViewModel(string destinationKey, string title, string subtitle, string macroCategoryKey, string? entryKey)
    {
        DestinationKey = destinationKey;
        Title = title;
        Subtitle = subtitle;
        MacroCategoryKey = macroCategoryKey;
        EntryKey = entryKey;
    }

    public string DestinationKey { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string MacroCategoryKey { get; }

    public string? EntryKey { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
