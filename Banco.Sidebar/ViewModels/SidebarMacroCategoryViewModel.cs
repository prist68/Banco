using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Banco.Sidebar.ViewModels;

public sealed class SidebarMacroCategoryViewModel : ViewModelBase
{
    private readonly Action<SidebarMacroCategoryViewModel> _activateAction;
    private bool _isActive;
    private string _accentColor;

    public SidebarMacroCategoryViewModel(
        string key,
        string title,
        string iconResourceKey,
        string accentColor,
        Action<SidebarMacroCategoryViewModel> activateAction)
    {
        Key = key;
        Title = title;
        IconResourceKey = iconResourceKey;
        _accentColor = accentColor;
        _activateAction = activateAction;
        ActivateCommand = new RelayCommand(() => _activateAction(this));
    }

    public string Key { get; }

    public string Title { get; }

    public string IconResourceKey { get; }

    public ObservableCollection<SidebarSectionGroupViewModel> Groups { get; } = [];

    public RelayCommand ActivateCommand { get; }

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
}
