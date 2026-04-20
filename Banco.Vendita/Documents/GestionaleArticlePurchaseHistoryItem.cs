namespace Banco.Vendita.Documents;

public sealed class GestionaleArticlePurchaseHistoryItem
{
    public int RigaOid { get; init; }

    public int DocumentoOid { get; init; }

    public string TipoDocumento { get; init; } = string.Empty;

    public DateTime DataDocumento { get; init; }

    public int? FornitoreOid { get; init; }

    public string FornitoreNominativo { get; init; } = string.Empty;

    public int? ArticoloOid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string DescrizioneArticolo { get; init; } = string.Empty;

    public string RiferimentoFattura { get; init; } = string.Empty;

    public decimal Quantita { get; init; }

    public decimal PrezzoUnitario { get; init; }

    public decimal TotaleRiga { get; init; }
}
