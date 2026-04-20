using Banco.Core.Domain.Entities;
using Banco.Vendita.Customers;

namespace Banco.Vendita.Points;

public sealed class PromotionEvaluationContext
{
    public GestionaleCustomerSummary? Customer { get; init; }

    public GestionalePointsCampaignSummary? Campaign { get; init; }

    public IReadOnlyList<PointsRewardRule> RewardRules { get; init; } = [];

    public DocumentoLocale? Document { get; init; }

    public bool RewardAlreadyApplied { get; init; }

    public PromotionEventType? LastEventType { get; init; }

    public Guid? LastEventRuleId { get; init; }

    public decimal? LastEventAvailablePoints { get; init; }

    public decimal? LastEventRequiredPoints { get; init; }
}
