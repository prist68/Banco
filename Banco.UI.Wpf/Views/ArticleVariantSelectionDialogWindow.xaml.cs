using System.Windows;
using System.Windows.Input;
using Banco.Vendita.Articles;

namespace Banco.UI.Wpf.Views;

public partial class ArticleVariantSelectionDialogWindow : Window
{
    public sealed class VariantChoiceRow
    {
        public required GestionaleArticleSearchResult Article { get; init; }

        public string VariantTitle =>
            string.IsNullOrWhiteSpace(Article.VarianteLabel)
                ? Article.DisplayLabel
                : Article.VarianteLabel;

        public string VariantSubtitle
        {
            get
            {
                return $"{Article.CodiceArticolo} · variante legacy";
            }
        }

        public string PriceLabel => $"{Article.PrezzoVendita:N2} €";

        public string StockLabel => $"Disp. {Article.Giacenza:N2}";
    }

    public ArticleVariantSelectionDialogWindow(
        GestionaleArticleSearchResult parentArticle,
        IReadOnlyList<GestionaleArticleSearchResult> variants)
    {
        ArgumentNullException.ThrowIfNull(parentArticle);
        ArgumentNullException.ThrowIfNull(variants);

        InitializeComponent();
        ParentArticleTextBlock.Text = parentArticle.DisplayLabel;
        VariantsListBox.ItemsSource = variants
            .Select(static article => new VariantChoiceRow { Article = article })
            .ToArray();

        if (VariantsListBox.Items.Count > 0)
        {
            VariantsListBox.SelectedIndex = 0;
        }
    }

    public GestionaleArticleSearchResult? SelectedVariant =>
        (VariantsListBox.SelectedItem as VariantChoiceRow)?.Article;

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedVariant is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void VariantsListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ConfirmButton.IsEnabled = SelectedVariant is not null;
    }

    private void VariantsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedVariant is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void VariantsListBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SelectedVariant is not null)
        {
            e.Handled = true;
            DialogResult = true;
        }
    }
}
