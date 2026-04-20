using Banco.Core.Domain.Entities;
using Banco.Vendita.Customers;
using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPointsCustomerBalanceService
{
    PointsCustomerRewardSummary BuildSummary(
        GestionaleCustomerSummary? customer,
        GestionalePointsCampaignSummary? campaign,
        IReadOnlyList<PointsRewardRule> rewardRules,
        DocumentoLocale? document);
}
