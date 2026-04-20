namespace Banco.Vendita.Documents;

public sealed class GestionaleArticlePurchaseQuickInfo
{
    public int ArticoloOid { get; init; }

    public DateTime DataUltimoAcquisto { get; init; }

    public int? FornitoreOid { get; init; }

    public string FornitoreNominativo { get; init; } = string.Empty;

    public string RiferimentoFattura { get; init; } = string.Empty;

    public decimal PrezzoUnitario { get; init; }
}
