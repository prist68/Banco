using System.Windows;
using System.Windows.Controls;

namespace Banco.UI.Wpf.DesktopModule;

public partial class DesktopQuickActionsPanel : UserControl
{
    public DesktopQuickActionsPanel()
    {
        InitializeComponent();
    }

    private void QuickAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: DesktopQuickActionViewModel item }
            && DataContext is DesktopHomeViewModel viewModel)
        {
            viewModel.OpenDestination(item.DestinationKey);
        }
    }
}
