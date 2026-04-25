using System.Windows;
using System.Windows.Controls;
using Banco.Core.Contracts.Navigation;
using Banco.Sidebar.ViewModels;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.DesktopModule;

public partial class DesktopHomeView : UserControl
{
    public DesktopHomeView()
    {
        InitializeComponent();
    }

    private DesktopHomeViewModel? CurrentViewModel => DataContext as DesktopHomeViewModel;

    private void DesktopCanvas_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ShellDragDropFormats.DesktopShortcutEntryKey)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DesktopCanvas_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ShellDragDropFormats.DesktopShortcutEntryKey) ||
            e.Data.GetData(ShellDragDropFormats.DesktopShortcutEntryKey) is not string entryKey ||
            CurrentViewModel is null)
        {
            return;
        }

        var dropPoint = e.GetPosition(DesktopCanvas);
        CurrentViewModel.AddShortcutFromEntry(entryKey, dropPoint.X, dropPoint.Y);
        e.Handled = true;
    }

    private void CommandSearchResultButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: SidebarSearchResultViewModel result })
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is ShellViewModel shellViewModel)
        {
            shellViewModel.Sidebar.OpenSearchResult(result);
            e.Handled = true;
        }
    }
}
