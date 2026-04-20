namespace Banco.Vendita.Points;

public sealed class PromotionEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int CampaignOid { get; init; }

    public Guid? RuleId { get; init; }

    public int? CustomerOid { get; init; }

    public Guid? LocalDocumentId { get; init; }

    public int? GestionaleDocumentOid { get; init; }

    public PromotionEventType EventType { get; init; }

    public PointsRewardType? RewardType { get; init; }

    public decimal AvailablePoints { get; init; }

    public decimal RequiredPoints { get; init; }

    public Guid? AppliedRowId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
