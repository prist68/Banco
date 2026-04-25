using System.Windows;
using System.Windows.Controls;

namespace Banco.UI.Wpf.DesktopModule;

public partial class DesktopRecentPanel : UserControl
{
    public DesktopRecentPanel()
    {
        InitializeComponent();
    }

    private void Recent_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DesktopRecentItemViewModel item }
            && DataContext is DesktopHomeViewModel viewModel)
        {
            viewModel.OpenDestination(item.DestinationKey);
        }
    }
}
