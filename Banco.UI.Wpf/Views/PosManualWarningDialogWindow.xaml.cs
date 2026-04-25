using System.Windows;
using System.Windows.Input;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Views;

public partial class PosManualWarningDialogWindow : Window
{
    public PosManualWarningDialogWindow(string dialogMessage)
    {
        InitializeComponent();
        DialogMessage = dialogMessage;
        DataContext = this;
    }

    public string DialogMessage { get; }

    public PosManualWarningChoice Choice { get; private set; } = PosManualWarningChoice.TornaScheda;

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = PosManualWarningChoice.TornaScheda;
        DialogResult = false;
    }

    private void ManualButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = PosManualWarningChoice.StampaManuale;
        DialogResult = true;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = PosManualWarningChoice.TornaScheda;
        DialogResult = false;
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
