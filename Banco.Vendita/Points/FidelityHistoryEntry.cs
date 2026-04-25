namespace Banco.Vendita.Points;

public sealed class FidelityHistoryEntry
{
    public int DocumentoOid { get; init; }

    public long NumeroDocumento { get; init; }

    public int AnnoDocumento { get; init; }

    public DateTime DataDocumento { get; init; }

    public decimal TotaleDocumento { get; init; }

    public decimal EarnedPoints { get; init; }

    public decimal SpentPoints { get; init; }

    public decimal ProgressivePoints { get; init; }

    public string DetailLines { get; init; } = string.Empty;

    public string StatusLabel => ProgressivePoints < 0 ? "Negativo" : "Ok";

    public string DocumentoLabel => $"{NumeroDocumento}/{AnnoDocumento}";

    public string DocumentoShortLabel => NumeroDocumento.ToString();

    public string EarnedPointsLabel => EarnedPoints == 0 ? "-" : EarnedPoints.ToString("N0");

    public string SpentPointsLabel => SpentPoints == 0 ? "-" : SpentPoints.ToString("N0");

    public string ProgressivePointsLabel => ProgressivePoints.ToString("N0");
}
