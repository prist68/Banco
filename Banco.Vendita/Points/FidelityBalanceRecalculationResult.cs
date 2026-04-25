namespace Banco.Vendita.Points;

public sealed class FidelityBalanceRecalculationResult
{
    public int CustomerOid { get; init; }

    public string CardCode { get; init; } = string.Empty;

    public decimal InitialPoints { get; init; }

    public decimal PreviousLegacyCurrentPoints { get; init; }

    public decimal ComputedCurrentPoints { get; init; }

    public int DocumentCount { get; init; }

    public bool LegacyUpdated { get; init; }

    public decimal DeltaPoints => ComputedCurrentPoints - PreviousLegacyCurrentPoints;
}
