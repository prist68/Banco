using Banco.Core.Contracts.Navigation;

namespace Banco.Sidebar.ViewModels;

public sealed class SidebarCustomizationEntryViewModel : ViewModelBase
{
    private readonly SidebarHostViewModel _host;
    private string _destinationKey;
    private bool _isVisible;
    private string _accentColor;

    public SidebarCustomizationEntryViewModel(
        SidebarHostViewModel host,
        string entryKey,
        string slotTitle,
        string macroCategoryKey,
        string groupTitle,
        string destinationKey,
        bool isVisible,
        string accentColor,
        bool supportsTargetOverride,
        bool supportsVisibility,
        bool supportsAccentCustomization,
        bool supportsReorder)
    {
        _host = host;
        EntryKey = entryKey;
        SlotTitle = slotTitle;
        MacroCategoryKey = macroCategoryKey;
        GroupTitle = groupTitle;
        _destinationKey = destinationKey;
        _isVisible = isVisible;
        _accentColor = accentColor;
        SupportsTargetOverride = supportsTargetOverride;
        SupportsVisibility = supportsVisibility;
        SupportsAccentCustomization = supportsAccentCustomization;
        SupportsReorder = supportsReorder;
        MoveUpCommand = new RelayCommand(() => _host.MoveShortcut(this, -1), () => SupportsReorder);
        MoveDownCommand = new RelayCommand(() => _host.MoveShortcut(this, 1), () => SupportsReorder);
    }

    public string EntryKey { get; }

    public string SlotTitle { get; }

    public string MacroCategoryKey { get; }

    public string GroupTitle { get; }

    public bool SupportsTargetOverride { get; }

    public bool SupportsVisibility { get; }

    public bool SupportsAccentCustomization { get; }

    public bool SupportsReorder { get; }

    public IReadOnlyList<NavigationDestinationDefinition> AvailableDestinations => _host.AvailableDestinations;

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public string DestinationKey
    {
        get => _destinationKey;
        set
        {
            if (SetProperty(ref _destinationKey, value))
            {
                _host.ApplyCustomization(this);
                NotifyPropertyChanged(nameof(DestinationTitle));
            }
        }
    }

    public string DestinationTitle => _host.ResolveDestinationTitle(_destinationKey);

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                _host.ApplyCustomization(this);
            }
        }
    }

    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetProperty(ref _accentColor, value))
            {
                _host.ApplyCustomization(this);
            }
        }
    }
}
