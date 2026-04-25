namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleLegacyOfferPropagationRequest
{
    public int ArticoloOid { get; init; }

    public decimal AliquotaIva { get; init; }

    public GestionaleArticleLegacyPriceTierUpdate PriceTier { get; init; } = new();

    public IReadOnlyList<GestionaleArticleLegacyOfferPropagationTarget> Targets { get; init; } = [];
}
