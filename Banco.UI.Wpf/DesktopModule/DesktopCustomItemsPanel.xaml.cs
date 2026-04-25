using System.Windows;
using System.Windows.Controls;

namespace Banco.UI.Wpf.DesktopModule;

public partial class DesktopCustomItemsPanel : UserControl
{
    public DesktopCustomItemsPanel()
    {
        InitializeComponent();
    }

    private void CustomItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DesktopSurfaceItemViewModel item }
            && DataContext is DesktopHomeViewModel viewModel)
        {
            viewModel.OpenItem(item);
        }
    }
}
