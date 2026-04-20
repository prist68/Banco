using Banco.Vendita.Abstractions;
using Banco.Vendita.Customers;
using Banco.Vendita.Points;

namespace Banco.Punti.Services;

public sealed class GestionalePointsPromotionEligibilityService : IGestionalePointsPromotionEligibilityService
{
    public GestionalePointsPromotionEligibilityResult Evaluate(
        GestionaleCustomerSummary? customer,
        GestionalePointsCampaignSummary? campaign)
    {
        if (customer is null)
        {
            return GestionalePointsPromotionEligibilityResult.Blocked(
                "Cliente non selezionato",
                "La promo non si applica finché il cliente non è identificato.");
        }

        if (customer.IsClienteGenerico)
        {
            return GestionalePointsPromotionEligibilityResult.Blocked(
                "Cliente generico",
                "La promo resta bloccata finché il cliente non viene identificato.",
                customerIsGeneric: true);
        }

        if (customer.HaRaccoltaPunti != true)
        {
            return GestionalePointsPromotionEligibilityResult.Blocked(
                "Carta fedeltà non attiva",
                "Questo cliente non è agganciato alla raccolta punti: inserisci o verifica il codice carta fedeltà.");
        }

        if (campaign is null)
        {
            return GestionalePointsPromotionEligibilityResult.Blocked(
                "Nessuna campagna selezionata",
                "Seleziona una campagna punti prima di proporre la promo.");
        }

        if (campaign.Attiva != true)
        {
            return GestionalePointsPromotionEligibilityResult.Blocked(
                "Campagna inattiva",
                $"La campagna '{campaign.NomeOperazione}' non è attiva.");
        }

        return GestionalePointsPromotionEligibilityResult.Allowed(
            "Promo pronta",
            $"Cliente identificato e campagna '{campaign.NomeOperazione}' disponibili per la vendita.");
    }
}
