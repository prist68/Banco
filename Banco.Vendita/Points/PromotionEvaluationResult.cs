namespace Banco.Vendita.Points;

public sealed class PromotionEvaluationResult
{
    public PromotionEventType EventType { get; init; }

    public bool IsGenericCustomer { get; init; }

    public bool IsConfigured { get; init; }

    public bool IsEligible => EventType == PromotionEventType.Eligible;

    public bool IsApplied => EventType == PromotionEventType.Applied;

    public bool ShouldShowPopup { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public PointsCustomerRewardSummary Summary { get; init; } = new();

    public PointsRewardRule? RewardRule { get; init; }

    public IReadOnlyList<PointsRewardRule> EligibleRewardRules { get; init; } = [];
}
