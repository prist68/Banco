namespace Banco.Vendita.Articles;

public sealed class GestionaleArticlePricingDetail
{
    public int ArticoloOid { get; init; }

    public string UnitaMisuraPrincipale { get; init; } = "PZ";

    public string? UnitaMisuraSecondaria { get; init; }

    public decimal MoltiplicatoreUnitaSecondaria { get; init; }

    public decimal QuantitaMinimaVendita { get; init; } = 1;

    public decimal QuantitaMultiplaVendita { get; init; } = 1;

    public IReadOnlyList<GestionaleArticleQuantityPriceTier> FascePrezzoQuantita { get; init; } = [];

    public bool HasSecondaryUnit => !string.IsNullOrWhiteSpace(UnitaMisuraSecondaria) && MoltiplicatoreUnitaSecondaria > 0;

    public bool RichiedeSceltaQuantita => FascePrezzoQuantita.Count > 1 || QuantitaMinimaVendita > 1 || QuantitaMultiplaVendita > 1;
}
