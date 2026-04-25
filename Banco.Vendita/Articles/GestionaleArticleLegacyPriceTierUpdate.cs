namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleLegacyPriceTierUpdate
{
    public decimal QuantitaMinima { get; init; }

    public decimal PrezzoNetto { get; init; }

    public decimal PrezzoIvato { get; init; }

    public DateTime? DataFine { get; init; }
}
