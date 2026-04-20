using Banco.Vendita.Abstractions;
using Banco.Vendita.Points;

namespace Banco.Punti.Services;

public sealed class PointsRewardRuleService : IPointsRewardRuleService
{
    private readonly IPointsRewardRuleRepository _repository;
    private readonly IGestionaleArticleReadService _articleReadService;

    public PointsRewardRuleService(
        IPointsRewardRuleRepository repository,
        IGestionaleArticleReadService articleReadService)
    {
        _repository = repository;
        _articleReadService = articleReadService;
    }

    public async Task<IReadOnlyList<PointsRewardRule>> GetAsync(int campaignOid, CancellationToken cancellationToken = default)
    {
        var rules = await _repository.GetByCampaignOidAsync(campaignOid, cancellationToken);
        foreach (var rule in rules)
        {
            await EnrichRewardArticleAsync(rule, cancellationToken);
        }

        return rules;
    }

    public Task SaveAsync(int campaignOid, IReadOnlyList<PointsRewardRule> rules, CancellationToken cancellationToken = default)
    {
        foreach (var rule in rules)
        {
            rule.CampaignOid = campaignOid;
            rule.UpdatedAt = DateTimeOffset.Now;
        }

        return _repository.SaveRangeAsync(campaignOid, rules, cancellationToken);
    }

    private async Task EnrichRewardArticleAsync(PointsRewardRule rule, CancellationToken cancellationToken)
    {
        if (rule.RewardType != PointsRewardType.ArticoloPremio ||
            rule.RewardArticleOid.GetValueOrDefault() <= 0)
        {
            return;
        }

        if (rule.RewardArticleTipoArticoloOid.HasValue &&
            rule.RewardArticlePrezzoVendita.GetValueOrDefault() > 0)
        {
            return;
        }

        var searchText = string.IsNullOrWhiteSpace(rule.RewardArticleCode)
            ? rule.RewardArticleDescription
            : rule.RewardArticleCode;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var results = await _articleReadService.SearchArticlesAsync(searchText, null, 20, cancellationToken);
        var article = results.FirstOrDefault(item => item.Oid == rule.RewardArticleOid)
            ?? results.FirstOrDefault(item => string.Equals(item.CodiceArticolo, rule.RewardArticleCode, StringComparison.OrdinalIgnoreCase));

        if (article is null)
        {
            return;
        }

        rule.ApplyArticle(article);
    }
}
