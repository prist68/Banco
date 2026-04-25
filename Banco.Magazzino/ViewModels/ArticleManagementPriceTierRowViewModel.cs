using System.Globalization;
using Banco.Vendita.Articles;

namespace Banco.Magazzino.ViewModels;

public sealed class ArticleManagementPriceTierRowViewModel
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public ArticleManagementPriceTierRowViewModel(GestionaleArticleQuantityPriceTier tier)
    {
        QuantitaMinima = tier.QuantitaMinima;
        PrezzoUnitario = tier.PrezzoUnitario;
        DataFine = tier.DataFine;
    }

    public decimal QuantitaMinima { get; }

    public decimal PrezzoUnitario { get; }

    public DateTime? DataFine { get; }

    // Blindatura locale: il simbolo euro non passa da StringFormat XAML,
    // cosi' eventuali problemi di encoding del file visuale non sporcano la resa.
    public string QuantitaMinimaLabel => QuantitaMinima.ToString("0.00", ItalianCulture);

    public string PrezzoUnitarioLabel => $"{PrezzoUnitario.ToString("0.00", ItalianCulture)} \u20AC";

    public string DataFineLabel => DataFine?.ToString("dd/MM/yyyy", ItalianCulture) ?? "-";
}
