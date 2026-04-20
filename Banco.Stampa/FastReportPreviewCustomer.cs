namespace Banco.Stampa;

public sealed class FastReportPreviewCustomer
{
    public int ClienteOid { get; init; }

    public string Nominativo { get; init; } = string.Empty;

    public string Indirizzo { get; init; } = string.Empty;

    public string Cap { get; init; } = string.Empty;

    public string Citta { get; init; } = string.Empty;

    public string Provincia { get; init; } = string.Empty;

    public string PartitaIva { get; init; } = string.Empty;

    public string CodiceFiscale { get; init; } = string.Empty;

    public string Telefono { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string CodiceCartaFedelta { get; init; } = string.Empty;

    public string IndirizzoCompleto { get; init; } = string.Empty;

    public string ContattiCompleti { get; init; } = string.Empty;

    public string FiscaleCompleto { get; init; } = string.Empty;

    public string PuntiPrecedentiVisuale { get; init; } = string.Empty;

    public string PuntiAttualiVisuale { get; init; } = string.Empty;
}
