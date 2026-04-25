namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleLegacyOffersUpdate
{
    public int ArticoloOid { get; init; }

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public decimal AliquotaIva { get; init; }

    public IReadOnlyList<GestionaleArticleLegacyPriceTierUpdate> PriceTiers { get; init; } = [];
}
