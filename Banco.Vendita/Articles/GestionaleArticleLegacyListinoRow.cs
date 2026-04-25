namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleLegacyListinoRow
{
    public int RowOid { get; init; }

    public int ArticoloOid { get; init; }

    public int ListinoOid { get; init; }

    public string ListinoNome { get; init; } = string.Empty;

    public decimal QuantitaMinima { get; init; } = 1;

    public decimal UltimoCostoLegacy { get; init; }

    public decimal PrezzoNetto { get; init; }

    public decimal PrezzoIvato { get; init; }

    public DateTime? DataFine { get; init; }

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public string VarianteLabel { get; init; } = string.Empty;

    public GestionaleArticleLegacyListinoRowKind RowKind { get; init; }

    public bool IsVariantSpecific => VarianteDettaglioOid1.HasValue || VarianteDettaglioOid2.HasValue;

    public bool IsBaseRow => QuantitaMinima <= 1;

    public bool MatchesVariantScope(int? varianteDettaglioOid1, int? varianteDettaglioOid2) =>
        NormalizeOid(VarianteDettaglioOid1) == NormalizeOid(varianteDettaglioOid1)
        && NormalizeOid(VarianteDettaglioOid2) == NormalizeOid(varianteDettaglioOid2);

    private static int NormalizeOid(int? value) => value.GetValueOrDefault();
}
