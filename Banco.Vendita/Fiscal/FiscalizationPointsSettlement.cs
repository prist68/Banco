namespace Banco.Vendita.Fiscal;

public sealed class FiscalizationPointsSettlement
{
    public int CustomerOid { get; init; }

    public int? CampaignOid { get; init; }

    public decimal StartingCurrentPoints { get; init; }

    public decimal EarnedPoints { get; init; }

    public decimal SpentPoints { get; init; }

    public decimal DeltaPoints => EarnedPoints - SpentPoints;
}
