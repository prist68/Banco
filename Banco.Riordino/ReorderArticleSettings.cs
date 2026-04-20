namespace Banco.Riordino;

public sealed class ReorderArticleSettings
{
    public string SettingsKey { get; set; } = string.Empty;

    public int ArticoloOid { get; set; }

    public string CodiceArticolo { get; set; } = string.Empty;

    public string DescrizioneArticolo { get; set; } = string.Empty;

    public string? BarcodeAlternativo { get; set; }

    public int? VarianteDettaglioOid1 { get; set; }

    public int? VarianteDettaglioOid2 { get; set; }

    public string VarianteLabel { get; set; } = string.Empty;

    public bool AcquistoAConfezione { get; set; }

    public bool VenditaAPezzoSingolo { get; set; }

    public decimal? PezziPerConfezione { get; set; }

    public decimal? MultiploOrdine { get; set; }

    public decimal? LottoMinimoOrdine { get; set; }

    public int? GiorniCopertura { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public bool InheritedFromParent { get; set; }

    public string InheritedFromSettingsKey { get; set; } = string.Empty;

    public static string BuildSettingsKey(
        int articoloOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        string? barcodeAlternativo)
    {
        var barcode = (barcodeAlternativo ?? string.Empty).Trim().ToUpperInvariant();
        return $"{articoloOid}|{varianteDettaglioOid1 ?? 0}|{varianteDettaglioOid2 ?? 0}|{barcode}";
    }
}
