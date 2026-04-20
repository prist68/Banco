using System.Windows.Input;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ShellNavigationItemViewModel : ViewModelBase
{
    private bool _isVisible = true;
    private bool _isActive;

    public ShellNavigationItemViewModel(
        string key,
        string title,
        string categoryKey,
        Func<object> contentFactory,
        Action<ShellNavigationItemViewModel> openAction)
    {
        Key = key;
        Title = title;
        CategoryKey = categoryKey;
        ContentFactory = contentFactory;
        OpenCommand = new RelayCommand(() => openAction(this));
    }

    public string Key { get; }

    public string CategoryKey { get; }

    public string Title { get; }

    public Func<object> ContentFactory { get; }

    public ICommand OpenCommand { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
