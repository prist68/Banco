namespace Banco.Vendita.Points;

public sealed class GestionalePointsCampaignEditModel
{
    public int Oid { get; set; }

    public string NomeOperazione { get; set; } = string.Empty;

    public DateTime? Inizio { get; set; }

    public DateTime? Fine { get; set; }

    public bool? Attiva { get; set; }

    public decimal? EuroPerPunto { get; set; }

    public string? BaseCalcolo { get; set; }

    public decimal? ImportoMinimo { get; set; }

    public bool? CalcolaSuValoreIva { get; set; }

    public int? OptimisticLockField { get; set; }

    public bool IsNuovo => Oid <= 0;
}
