namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleQuantityPriceTier
{
    public decimal QuantitaMinima { get; init; }

    public decimal PrezzoUnitario { get; init; }

    public DateTime? DataFine { get; init; }
}
