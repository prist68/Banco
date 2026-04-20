namespace Banco.Vendita.Points;

public sealed class GestionalePointsCampaignSummary
{
    public int Oid { get; init; }

    public bool? Attiva { get; init; }

    public string? BaseCalcolo { get; init; }

    public bool? CalcolaSuValoreIva { get; init; }

    public decimal? EuroPerPunto { get; init; }

    public DateTime? Fine { get; init; }

    public decimal? ImportoMinimo { get; init; }

    public DateTime? Inizio { get; init; }

    public string NomeOperazione { get; init; } = string.Empty;

    public string StatoLabel => Attiva switch
    {
        true => "Attiva",
        false => "Disattiva",
        null => "n.d."
    };

    public string BaseCalcoloLabel => string.IsNullOrWhiteSpace(BaseCalcolo) ? "n.d." : BaseCalcolo;

    public string CalcolaSuValoreIvaLabel => CalcolaSuValoreIva switch
    {
        true => "Si",
        false => "No",
        null => "n.d."
    };

    public string EuroPerPuntoLabel => EuroPerPunto.HasValue ? EuroPerPunto.Value.ToString("N2") : "n.d.";

    public string ImportoMinimoLabel => ImportoMinimo.HasValue ? ImportoMinimo.Value.ToString("N2") : "n.d.";

    public string PeriodoLabel
    {
        get
        {
            var inizio = Inizio.HasValue ? Inizio.Value.ToString("dd/MM/yyyy") : "n.d.";
            var fine = Fine.HasValue ? Fine.Value.ToString("dd/MM/yyyy") : "n.d.";
            return $"{inizio} - {fine}";
        }
    }
}
