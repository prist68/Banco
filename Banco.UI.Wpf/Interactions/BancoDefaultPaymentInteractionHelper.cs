using System.Windows;
using Banco.UI.Wpf.ViewModels;
using Banco.UI.Wpf.Views;

namespace Banco.UI.Wpf.Interactions;

internal static class BancoDefaultPaymentInteractionHelper
{
    public static bool EnsureDefaultCashPayment(BancoViewModel viewModel, Window? owner, string commandLabel)
    {
        if (!viewModel.RichiedeConfermaContantiPredefiniti)
        {
            return true;
        }

        var dialog = new ConfirmationDialogWindow(
            "Banco / pagamenti",
            "Nessun pagamento inserito",
            $"Per procedere con {commandLabel} verra` impostato automaticamente l'intero importo su Contanti. Confermi?",
            "Si",
            "No",
            "Premendo Invio confermi il contante predefinito. Il pagamento restera` comunque modificabile richiamando la scheda.")
        {
            Owner = owner
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        viewModel.ApplicaContantiPredefinitiPerChiusura();
        return true;
    }
}
