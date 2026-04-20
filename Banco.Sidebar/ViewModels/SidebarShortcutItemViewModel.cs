using System.Windows.Media;

namespace Banco.Sidebar.ViewModels;

public sealed class SidebarShortcutItemViewModel : ViewModelBase
{
    private readonly Action<SidebarShortcutItemViewModel> _openAction;
    private bool _isActive;
    private bool _isHighlighted;
    private bool _isVisible = true;
    private string _displayTitle;
    private string _accentColor;

    public SidebarShortcutItemViewModel(
        string entryKey,
        string baseTitle,
        string groupKey,
        string groupTitle,
        string macroCategoryKey,
        string? destinationKey,
        bool isEnabled,
        bool isInformational,
        string accentColor,
        string? infoText,
        Action<SidebarShortcutItemViewModel> openAction)
    {
        EntryKey = entryKey;
        BaseTitle = baseTitle;
        GroupKey = groupKey;
        GroupTitle = groupTitle;
        MacroCategoryKey = macroCategoryKey;
        DestinationKey = destinationKey;
        IsEnabled = isEnabled;
        IsInformational = isInformational;
        InfoText = infoText;
        _displayTitle = baseTitle;
        _accentColor = accentColor;
        _openAction = openAction;
        OpenCommand = new RelayCommand(() => _openAction(this), () => IsEnabled);
    }

    public string EntryKey { get; }

    public string BaseTitle { get; }

    public string GroupKey { get; }

    public string GroupTitle { get; }

    public string MacroCategoryKey { get; }

    public string? DestinationKey { get; private set; }

    public bool IsEnabled { get; }

    public bool IsInformational { get; }

    public string? InfoText { get; }

    public RelayCommand OpenCommand { get; }

    public string DisplayTitle
    {
        get => _displayTitle;
        set => SetProperty(ref _displayTitle, value);
    }

    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetProperty(ref _accentColor, value))
            {
                NotifyPropertyChanged(nameof(AccentBrush));
            }
        }
    }

    public Brush AccentBrush => (Brush)new BrushConverter().ConvertFrom(_accentColor)!;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public void ApplyDestination(string? destinationKey, string displayTitle)
    {
        DestinationKey = destinationKey;
        DisplayTitle = displayTitle;
    }
}
