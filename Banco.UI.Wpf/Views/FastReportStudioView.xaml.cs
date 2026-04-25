using System.Windows;
using System.Windows.Controls;
using Banco.UI.Wpf.ViewModels;
using Banco.Stampa;

namespace Banco.UI.Wpf.Views;

public partial class FastReportStudioView : UserControl
{
    public FastReportStudioView()
    {
        InitializeComponent();
    }

    private void PrinterMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not PrintLayoutDefinition layout)
        {
            return;
        }

        if (DataContext is FastReportStudioViewModel viewModel)
        {
            viewModel.SelectedLayout = layout;
        }

        if (button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }
}
