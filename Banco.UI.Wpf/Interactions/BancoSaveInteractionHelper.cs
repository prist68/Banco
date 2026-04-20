using System.Windows;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Interactions;

internal static class BancoSaveInteractionHelper
{
    public static async Task ExecuteOfficialSaveAsync(BancoViewModel viewModel, Window? owner)
    {
        if (viewModel.HasPendingLocalChanges)
        {
            if (!BancoDefaultPaymentInteractionHelper.EnsureDefaultCashPayment(viewModel, owner, "Salva"))
            {
                return;
            }
        }

        await viewModel.SalvaDocumentoAsync();
    }
}
