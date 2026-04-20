using System.Collections.ObjectModel;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ShellNavigationCategoryViewModel : ViewModelBase
{
    private bool _isVisible = true;
    private bool _isExpanded = true;
    private bool _isActive;

    public ShellNavigationCategoryViewModel(string key, string title)
    {
        Key = key;
        Title = title;
        Items = [];
    }

    public string Key { get; }

    public string Title { get; }

    public ObservableCollection<ShellNavigationItemViewModel> Items { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
