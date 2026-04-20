using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPointsRewardRuleRepository
{
    Task<IReadOnlyList<PointsRewardRule>> GetByCampaignOidAsync(int campaignOid, CancellationToken cancellationToken = default);

    Task SaveRangeAsync(int campaignOid, IReadOnlyList<PointsRewardRule> rules, CancellationToken cancellationToken = default);
}
