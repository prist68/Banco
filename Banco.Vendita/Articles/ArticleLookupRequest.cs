namespace Banco.Vendita.Articles;

public sealed class ArticleLookupRequest
{
    public string SearchText { get; init; } = string.Empty;

    public int? SelectedPriceListOid { get; init; }

    public string Title { get; init; } = "Ricerca articoli";

    public string Subtitle { get; init; } = "Cerca dal catalogo legacy e aggiungi l'articolo corretto al documento.";

    public bool PreferVariantResults { get; init; }
}
