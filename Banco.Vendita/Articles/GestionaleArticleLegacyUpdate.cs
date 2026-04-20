namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleLegacyUpdate
{
    public int ArticoloOid { get; init; }

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public string DescrizioneArticolo { get; init; } = string.Empty;

    public string UnitaMisuraPrincipale { get; init; } = "PZ";

    public string? UnitaMisuraSecondaria { get; init; }

    public decimal? MoltiplicatoreUnitaSecondaria { get; init; }

    public decimal QuantitaMinimaVendita { get; init; } = 1;

    public decimal QuantitaMultiplaVendita { get; init; } = 1;

    public decimal PrezzoVendita { get; init; }
}
