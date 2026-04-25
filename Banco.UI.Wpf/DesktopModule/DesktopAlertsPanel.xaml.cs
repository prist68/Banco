using System.Windows;
using System.Windows.Controls;

namespace Banco.UI.Wpf.DesktopModule;

public partial class DesktopAlertsPanel : UserControl
{
    public DesktopAlertsPanel()
    {
        InitializeComponent();
    }

    private void Alert_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DesktopAlertItemViewModel item }
            && item.CanOpen
            && DataContext is DesktopHomeViewModel viewModel)
        {
            viewModel.OpenDestination(item.DestinationKey);
        }
    }
}
