namespace Banco.Vendita.Articles;

public sealed class GestionaleArticlePricingDetail
{
    public int ArticoloOid { get; init; }

    public int? ListinoOid { get; init; }

    public string ListinoNome { get; init; } = string.Empty;

    public DateTime? DataFineDefault { get; init; }

    public string UnitaMisuraPrincipale { get; init; } = "PZ";

    public string? UnitaMisuraSecondaria { get; init; }

    public decimal MoltiplicatoreUnitaSecondaria { get; init; }

    public decimal QuantitaMinimaVendita { get; init; } = 1;

    public decimal QuantitaMultiplaVendita { get; init; } = 1;

    public IReadOnlyList<GestionaleArticleQuantityPriceTier> FascePrezzoQuantita { get; init; } = [];

    public bool HasSecondaryUnit => !string.IsNullOrWhiteSpace(UnitaMisuraSecondaria) && MoltiplicatoreUnitaSecondaria > 0;

    public bool HasQuantityPriceOffer => FascePrezzoQuantita.Count > 1;

    public bool HasMandatoryQuantityConstraints => QuantitaMinimaVendita > 1 || QuantitaMultiplaVendita > 1;

    public bool RichiedeSceltaQuantita => HasMandatoryQuantityConstraints || HasQuantityPriceOffer;
}
