namespace Banco.Vendita.Documents;

public sealed class GestionaleDocumentSummary
{
    public int Oid { get; init; }

    public int Numero { get; init; }

    public int Anno { get; init; }

    public DateTime Data { get; init; }

    public int SoggettoOid { get; init; }

    public string SoggettoNominativo { get; init; } = string.Empty;

    public string Operatore { get; init; } = string.Empty;

    public decimal TotaleDocumento { get; init; }

    public decimal TotaleImponibile { get; init; }

    public decimal TotaleIva { get; init; }

    public decimal Pagatosospeso { get; init; }

    public decimal PagatoContanti { get; init; }

    public decimal PagatoCarta { get; init; }

    public decimal PagatoWeb { get; init; }

    public decimal PagatoBuoni { get; init; }

    public decimal TotalePagatoUfficiale { get; init; }

    public decimal ResiduoPagamento => Math.Max(0, TotaleDocumento - TotalePagatoUfficiale - Pagatosospeso);

    public int? ScontrinoNumero { get; init; }

    public bool HasLegacyFiscalSignal => ScontrinoNumero.HasValue && ScontrinoNumero.Value > 0;

    public bool IsNonScontrinatoLegacy => !HasLegacyFiscalSignal;

    public bool IsSospeso => Pagatosospeso > 0;

    public bool IsPagato => !IsSospeso && TotaleDocumento > 0 && TotalePagatoUfficiale >= TotaleDocumento;

    public string ClienteLabel => string.IsNullOrWhiteSpace(SoggettoNominativo)
        ? $"Soggetto #{SoggettoOid}"
        : $"{SoggettoOid} - {SoggettoNominativo}";

    public string DocumentoLabel => $"{Numero}/{Anno}";

    public string OraVenditaLabel => Data.ToString("HH:mm");

    public string ScontrinoLabel => string.Empty;

    public string StatoLabel => IsSospeso
        ? "Documento Banco legacy con componente sospeso"
        : "Documento Banco legacy";
}
