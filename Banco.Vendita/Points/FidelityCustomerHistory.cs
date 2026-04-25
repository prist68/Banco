namespace Banco.Vendita.Points;

public sealed class FidelityCustomerHistory
{
    public int CustomerOid { get; init; }

    public string CardCode { get; init; } = string.Empty;

    public decimal InitialPoints { get; init; }

    public decimal LegacyCurrentPoints { get; init; }

    public decimal ComputedCurrentPoints { get; init; }

    public decimal DeltaPoints => ComputedCurrentPoints - LegacyCurrentPoints;

    public IReadOnlyList<FidelityHistoryEntry> Entries { get; init; } = [];
}
