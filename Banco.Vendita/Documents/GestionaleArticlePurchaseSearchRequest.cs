namespace Banco.Vendita.Documents;

public sealed class GestionaleArticlePurchaseSearchRequest
{
    public int? ArticoloOid { get; init; }

    public int? FornitoreOid { get; init; }

    public DateTime? DataInizio { get; init; }

    public DateTime? DataFine { get; init; }

    public int MaxResults { get; init; } = 500;
}
