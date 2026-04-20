namespace Banco.Stampa;

public sealed class FastReportStoreProfile
{
    public string RagioneSociale { get; init; } = string.Empty;

    public string Indirizzo { get; init; } = string.Empty;

    public string Cap { get; init; } = string.Empty;

    public string Citta { get; init; } = string.Empty;

    public string Provincia { get; init; } = string.Empty;

    public string PartitaIva { get; init; } = string.Empty;

    public string Telefono { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RiferimentoScontrino { get; init; } = string.Empty;

    public string IntestazioneCompleta
    {
        get
        {
            var localita = string.Join(" ",
                new[] { Cap, ComposeCityProvince() }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()));

            return string.Join(" - ",
                new[] { Indirizzo, localita }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()));
        }
    }

    public string ContattiCompleti
    {
        get
        {
            var items = new List<string>();
            if (!string.IsNullOrWhiteSpace(Telefono))
            {
                items.Add($"Tel. {Telefono.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(Email))
            {
                items.Add($"Email {Email.Trim()}");
            }

            return string.Join(" - ", items);
        }
    }

    public string PartitaIvaVisuale => string.IsNullOrWhiteSpace(PartitaIva)
        ? string.Empty
        : $"Partita iva {PartitaIva.Trim()}";

    private string ComposeCityProvince()
    {
        if (string.IsNullOrWhiteSpace(Citta))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(Provincia)
            ? Citta.Trim()
            : $"{Citta.Trim()} ({Provincia.Trim()})";
    }
}
