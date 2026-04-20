namespace Banco.Vendita.Customers;

public sealed class GestionaleCustomerSummary
{
    public int Oid { get; init; }

    public bool IsFornitore { get; init; }

    public bool IsClienteGenerico => Oid <= 1 || DisplayLabel.Contains("cliente generico", StringComparison.OrdinalIgnoreCase);

    public string RagioneSociale { get; init; } = string.Empty;

    public string? Nome { get; init; }

    public string? CodiceCartaFedelta { get; init; }

    public decimal? PuntiIniziali { get; init; }

    public decimal? PuntiAssegnati { get; init; }

    public decimal? PuntiTotali { get; init; }

    public bool HaRaccoltaPunti { get; init; }

    public int? ClienteListinoOid { get; init; }

    public string? ClienteListinoNome { get; init; }

    public decimal? PuntiDisponibili => PuntiAssegnati ?? PuntiIniziali;

    public decimal? Punti => PuntiDisponibili;

    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(RagioneSociale) && !string.IsNullOrWhiteSpace(Nome))
            {
                return $"{Oid} - {RagioneSociale} ({Nome})";
            }

            if (!string.IsNullOrWhiteSpace(RagioneSociale))
            {
                return $"{Oid} - {RagioneSociale}";
            }

            if (!string.IsNullOrWhiteSpace(Nome))
            {
                return $"{Oid} - {Nome}";
            }

            return $"Soggetto #{Oid}";
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Nome) && !string.IsNullOrWhiteSpace(RagioneSociale))
            {
                return $"{Nome} {RagioneSociale}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(RagioneSociale))
            {
                return RagioneSociale;
            }

            return Nome ?? string.Empty;
        }
    }

    public string PuntiInizialiLabel => PuntiIniziali.HasValue ? PuntiIniziali.Value.ToString("N2") : "n.d.";

    public string PuntiAssegnatiLabel => PuntiAssegnati.HasValue ? PuntiAssegnati.Value.ToString("N2") : "n.d.";

    public string PuntiLabel => PuntiDisponibili.HasValue ? PuntiDisponibili.Value.ToString("N2") : "n.d.";

    public string PuntiDettaglioLabel => $"Disponibili {PuntiLabel} | Iniziali {PuntiInizialiLabel}";
}
