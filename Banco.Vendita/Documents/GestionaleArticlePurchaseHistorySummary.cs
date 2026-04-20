namespace Banco.Vendita.Documents;

public sealed class GestionaleArticlePurchaseHistorySummary
{
    public decimal TotaleAcquistato { get; init; }

    public decimal PezziAcquistati { get; init; }

    public decimal UltimoPrezzo { get; init; }

    public bool HasResults { get; init; }
}
