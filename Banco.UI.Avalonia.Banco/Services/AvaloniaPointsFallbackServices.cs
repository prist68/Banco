using Banco.Core.Domain.Entities;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Customers;
using Banco.Vendita.Points;

namespace Banco.UI.Avalonia.Banco.Services;

public sealed class AvaloniaPointsRewardRuleService : IPointsRewardRuleService
{
    public Task<IReadOnlyList<PointsRewardRule>> GetAsync(int campaignOid, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PointsRewardRule>>([]);
    }

    public Task SaveAsync(int campaignOid, IReadOnlyList<PointsRewardRule> rules, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class AvaloniaPointsCustomerBalanceService : IPointsCustomerBalanceService
{
    public PointsCustomerRewardSummary BuildSummary(
        GestionaleCustomerSummary? customer,
        GestionalePointsCampaignSummary? campaign,
        IReadOnlyList<PointsRewardRule> rewardRules,
        DocumentoLocale? document)
    {
        return new PointsCustomerRewardSummary
        {
            HistoricalPoints = customer?.PuntiAssegnati ?? customer?.PuntiTotali ?? customer?.PuntiIniziali ?? 0,
            CurrentDocumentPoints = 0,
            RequiredPoints = 0,
            StatusLabel = "Punti non applicati in questa fase Avalonia"
        };
    }
}
