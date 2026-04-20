using System.Collections.ObjectModel;

namespace Banco.Sidebar.ViewModels;

public sealed class SidebarSectionGroupViewModel : ViewModelBase
{
    public SidebarSectionGroupViewModel(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public string Key { get; }

    public string Title { get; }

    public ObservableCollection<SidebarShortcutItemViewModel> Items { get; } = [];
}
