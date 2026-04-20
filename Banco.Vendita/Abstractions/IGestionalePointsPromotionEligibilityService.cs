using Banco.Vendita.Customers;
using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IGestionalePointsPromotionEligibilityService
{
    GestionalePointsPromotionEligibilityResult Evaluate(
        GestionaleCustomerSummary? customer,
        GestionalePointsCampaignSummary? campaign);
}
