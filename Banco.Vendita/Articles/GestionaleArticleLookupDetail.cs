namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleLookupDetail
{
    public int ArticoloOid { get; init; }

    public int? CategoriaOid { get; init; }

    public int? IvaOid { get; init; }

    public int? TassaOid { get; init; }

    public bool UsaTassa { get; init; }

    public decimal? MoltiplicatoreTassa { get; init; }

    public int? UnitaMisuraOid { get; init; }

    public int? UnitaMisuraSecondariaOid { get; init; }

    public int? ContoCostoOid { get; init; }

    public int? ContoRicavoOid { get; init; }

    public int? CategoriaRicaricoOid { get; init; }

    public int? Variante1Oid { get; init; }

    public int? Variante2Oid { get; init; }

    public int? GaranziaMesiVendita { get; init; }

    public decimal VendutoUltimoMese { get; init; }

    public decimal VendutoUltimiTreMesi { get; init; }

    public int? TipoArticoloCode { get; init; }

    public int? TracciabilitaCode { get; init; }

    public int? TipoCostoArticoloCode { get; init; }

    public bool UsaVenditaAlBancoTouch { get; init; }

    public bool Esporta { get; init; }

    public bool EscludiInventario { get; init; }

    public bool EscludiTotaleDocumento { get; init; }

    public bool EscludiScontrino { get; init; }

    public bool EscludiScontoSoggetto { get; init; }

    public bool IsObsoleto { get; init; }

    public bool AggDescrBreveAllaDescrizione { get; init; }

    public string Fonte { get; init; } = string.Empty;

    public string CodiceTipo { get; init; } = string.Empty;

    public string CodiceValore { get; init; } = string.Empty;

    public string Avvertenze { get; init; } = string.Empty;

    public bool Online { get; init; }

    public int? DisponibilitaOnlineOid { get; init; }

    public string DisponibilitaOnlineLabel { get; init; } = string.Empty;

    public int? CondizioneCode { get; init; }

    public int? OperazioneSuCartaFedeltaCode { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string Descrizione { get; init; } = string.Empty;

    public string? VarianteLabel { get; init; }

    public string? BarcodeAlternativo { get; init; }

    public string DescrizioneBreveHtml { get; init; } = string.Empty;

    public string DescrizioneLungaHtml { get; init; } = string.Empty;

    public string Categoria { get; init; } = string.Empty;

    public string SottoCategoria { get; init; } = string.Empty;

    public string ContoCostoLabel { get; init; } = string.Empty;

    public string ContoRicavoLabel { get; init; } = string.Empty;

    public string CategoriaRicaricoLabel { get; init; } = string.Empty;

    public string Variante1LookupLabel { get; init; } = string.Empty;

    public string Variante2LookupLabel { get; init; } = string.Empty;

    public string? ImageUrl { get; init; }

    public string Brand { get; init; } = string.Empty;

    public string ExciseLabel { get; init; } = string.Empty;

    public DateTime? LastSaleDate { get; init; }

    public string ListinoNome { get; init; } = string.Empty;

    public decimal PrezzoVendita { get; init; }

    public decimal Giacenza { get; init; }

    public decimal QuantitaMinimaVendita { get; init; } = 1;

    public decimal QuantitaMultiplaVendita { get; init; } = 1;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<GestionaleArticleLookupSpecification> Specifications { get; init; } = [];

    public IReadOnlyList<GestionaleArticleQuantityPriceTier> FascePrezzoQuantita { get; init; } = [];
}
