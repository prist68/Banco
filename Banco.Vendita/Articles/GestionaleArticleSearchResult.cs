namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleSearchResult
{
    public int Oid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string Descrizione { get; init; } = string.Empty;

    public decimal PrezzoVendita { get; init; }

    public decimal Giacenza { get; init; }

    public int IvaOid { get; init; }

    public decimal AliquotaIva { get; init; }

    public int? TipoArticoloOid { get; init; }

    public int? ArticoloPadreOid { get; init; }

    public string? BarcodeAlternativo { get; init; }

    public int? VarianteDettaglioOid1 { get; init; }

    public int? VarianteDettaglioOid2 { get; init; }

    public string? VarianteNome { get; init; }

    public string? VarianteDescrizione { get; init; }

    public bool IsVariante => ArticoloPadreOid.HasValue || !string.IsNullOrWhiteSpace(VarianteDescrizione);

    public bool IsGiftCardArticle => TipoArticoloOid == 7;

    public string TipoDisponibilitaLabel => IsVariante ? "Disponibilita` variante" : "Disponibilita` articolo";

    public string VarianteLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(VarianteNome) && string.IsNullOrWhiteSpace(VarianteDescrizione))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(VarianteNome) && !string.IsNullOrWhiteSpace(VarianteDescrizione))
            {
                return $"{VarianteNome}: {VarianteDescrizione}";
            }

            return VarianteNome ?? VarianteDescrizione ?? string.Empty;
        }
    }

    public string DisplayLabel
    {
        get
        {
            var suffix = string.IsNullOrWhiteSpace(VarianteLabel)
                ? string.Empty
                : $" [{VarianteLabel}]";

            return $"{CodiceArticolo} - {Descrizione}{suffix}";
        }
    }
}
