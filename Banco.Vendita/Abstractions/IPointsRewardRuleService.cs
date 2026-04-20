using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPointsRewardRuleService
{
    Task<IReadOnlyList<PointsRewardRule>> GetAsync(int campaignOid, CancellationToken cancellationToken = default);

    Task SaveAsync(int campaignOid, IReadOnlyList<PointsRewardRule> rules, CancellationToken cancellationToken = default);
}
