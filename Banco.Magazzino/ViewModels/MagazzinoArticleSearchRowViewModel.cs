using System.Globalization;
using Banco.Vendita.Articles;

namespace Banco.Magazzino.ViewModels;

public sealed class MagazzinoArticleSearchRowViewModel
{
    private MagazzinoArticleSearchRowViewModel()
    {
    }

    public GestionaleArticleSearchResult? Article { get; init; }

    public bool IsSummary { get; init; }

    public bool IsChild { get; init; }

    public bool IsSelectable => Article is not null;

    public int FamilyOid { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string QuantityLabel { get; init; } = string.Empty;

    public static MagazzinoArticleSearchRowViewModel CreateSummary(
        GestionaleArticleSearchResult article,
        int familyOid,
        string codiceArticolo,
        string descrizione,
        decimal totalQuantity,
        int variantCount)
    {
        return new MagazzinoArticleSearchRowViewModel
        {
            Article = article,
            FamilyOid = familyOid,
            IsSummary = true,
            Title = $"{codiceArticolo} - {descrizione}",
            Subtitle = variantCount == 1
                ? "1 variante disponibile"
                : $"{variantCount} varianti disponibili",
            QuantityLabel = FormatQuantity(totalQuantity)
        };
    }

    public static MagazzinoArticleSearchRowViewModel CreateArticle(GestionaleArticleSearchResult article)
    {
        var title = string.IsNullOrWhiteSpace(article.VarianteLabel)
            ? $"{article.CodiceArticolo} - {article.Descrizione}"
            : article.VarianteLabel;

        var subtitle = string.IsNullOrWhiteSpace(article.VarianteLabel)
            ? "Articolo singolo"
            : $"{article.CodiceArticolo} - {article.Descrizione}";

        return new MagazzinoArticleSearchRowViewModel
        {
            Article = article,
            FamilyOid = ResolveFamilyOid(article),
            IsChild = article.IsVariante,
            Title = title,
            Subtitle = subtitle,
            QuantityLabel = FormatQuantity(article.Giacenza)
        };
    }

    public static int ResolveFamilyOid(GestionaleArticleSearchResult article) =>
        article.ArticoloPadreOid ?? article.Oid;

    private static string FormatQuantity(decimal quantity) =>
        quantity.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
}
