namespace Banco.Vendita.Documents;

public sealed class GestionaleDocumentDetail
{
    public int Oid { get; init; }

    public int Numero { get; init; }

    public int Anno { get; init; }

    public DateTime Data { get; init; }

    public int SoggettoOid { get; init; }

    public string SoggettoNominativo { get; init; } = string.Empty;

    public decimal TotaleDocumento { get; init; }

    public decimal TotaleImponibile { get; init; }

    public decimal TotaleIva { get; init; }

    public decimal PagatoContanti { get; init; }

    public decimal PagatoCarta { get; init; }

    public decimal PagatoWeb { get; init; }

    public decimal PagatoBuoni { get; init; }

    public decimal PagatoSospeso { get; init; }

    public decimal TotalePagatoUfficiale { get; init; }

    public int? Fatturato { get; init; }

    public int? ScontrinoNumero { get; init; }

    public bool HasLegacyFiscalSignal => LegacyScontrinoState.IsFiscalizzato(Fatturato);

    public bool IsNonScontrinatoLegacy => LegacyScontrinoState.IsNonScontrinato(Fatturato);

    public string Operatore { get; init; } = string.Empty;

    public int? ListinoOid { get; init; }

    public string? ListinoNome { get; init; }

    public string DocumentoLabel => $"{Numero}/{Anno}";

    public string StatoLabel => PagatoSospeso > 0
        ? "Documento Banco legacy con componente sospeso"
        : "Documento Banco legacy";

    public IReadOnlyList<GestionaleDocumentRowDetail> Righe { get; set; } = [];
}
