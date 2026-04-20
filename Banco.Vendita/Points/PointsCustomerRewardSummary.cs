namespace Banco.Vendita.Points;

public sealed class PointsCustomerRewardSummary
{
    public decimal HistoricalPoints { get; init; }

    public decimal CurrentDocumentPoints { get; init; }

    public decimal TotalAvailablePoints => HistoricalPoints + CurrentDocumentPoints;

    public decimal RequiredPoints { get; init; }

    public decimal MissingPoints => Math.Max(0, RequiredPoints - TotalAvailablePoints);

    public string RuleName { get; init; } = string.Empty;

    public string RewardDescription { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public string MissingPointsLabel => MissingPoints <= 0 ? "Premio disponibile" : $"{MissingPoints:N2} punti mancanti";
}
