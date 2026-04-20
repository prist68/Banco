using System.Windows;

namespace Banco.UI.Wpf.Views;

public enum AzzeraContenutoChoice
{
    Rimanda = 0,
    AggiungiArticolo = 1,
    Annulla = 2
}

public partial class AzzeraContenutoChoiceDialogWindow : Window
{
    public AzzeraContenutoChoiceDialogWindow()
    {
        InitializeComponent();
    }

    public AzzeraContenutoChoice Choice { get; private set; } = AzzeraContenutoChoice.Rimanda;

    private void RimandaButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = AzzeraContenutoChoice.Rimanda;
        DialogResult = false;
    }

    private void AggiungiArticoloButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = AzzeraContenutoChoice.AggiungiArticolo;
        DialogResult = true;
    }

    private void AnnullaButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = AzzeraContenutoChoice.Annulla;
        DialogResult = true;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Choice = AzzeraContenutoChoice.Rimanda;
        DialogResult = false;
    }
}
