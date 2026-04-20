namespace Banco.Vendita.Points;

public sealed class GestionalePointsArticleSummary
{
    public int Oid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string Descrizione { get; init; } = string.Empty;

    public decimal? EuroPerPunto { get; init; }

    public bool? OperazioneSuCartaFedelta { get; init; }

    public int? PuntiDaSottrarre { get; init; }

    public int? Tracciabilita { get; init; }

    public string EuroPerPuntoLabel => EuroPerPunto.HasValue ? EuroPerPunto.Value.ToString("N2") : "n.d.";

    public string ModalitaLabel
    {
        get
        {
            var puntiDaSottrarre = PuntiDaSottrarre.GetValueOrDefault();
            if (puntiDaSottrarre > 0)
            {
                return $"Scarico {puntiDaSottrarre} punti";
            }

            if (EuroPerPunto.HasValue && EuroPerPunto.Value > 0)
            {
                return $"1 punto ogni {EuroPerPunto.Value:N2} euro";
            }

            return "n.d.";
        }
    }

    public string OperazioneSuCartaFedeltaLabel => OperazioneSuCartaFedelta switch
    {
        true => "Sì",
        false => "No",
        null => "n.d."
    };

    public string TracciabilitaLabel => Tracciabilita.HasValue ? Tracciabilita.Value.ToString() : "n.d.";
}
