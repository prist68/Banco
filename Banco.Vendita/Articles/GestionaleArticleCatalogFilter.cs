namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleCatalogFilter
{
    public string SearchText { get; init; } = string.Empty;

    public string? CategoryPath { get; init; }

    public bool OnlyAvailable { get; init; }

    public bool OnlyWithImage { get; init; }
}
