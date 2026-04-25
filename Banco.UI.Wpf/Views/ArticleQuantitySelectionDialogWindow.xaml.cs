using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Banco.Vendita.Articles;

namespace Banco.UI.Wpf.Views;

public partial class ArticleQuantitySelectionDialogWindow : Window
{
    private readonly GestionaleArticlePricingDetail _pricingDetail;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("it-IT");

    public ArticleQuantitySelectionDialogWindow(
        GestionaleArticleSearchResult articolo,
        GestionaleArticlePricingDetail pricingDetail,
        decimal initialQuantity)
    {
        InitializeComponent();
        _pricingDetail = pricingDetail;
        Eyebrow = "Banco / quantita`";
        DialogTitle = $"{articolo.CodiceArticolo} - {articolo.Descrizione}";
        DialogMessage = "Seleziona la quantita`: il prezzo viene calcolato sulle fasce quantita` del legacy.";
        FooterHint = "Selezionando una soglia valida, il campo quantita` viene aggiornato automaticamente.";
        DataContext = this;

        var normalizedInitialQuantity = NormalizeQuantity(initialQuantity);
        QuantityTiers = pricingDetail.FascePrezzoQuantita
            .OrderBy(item => item.QuantitaMinima)
            .Select(item => new ArticleQuantityTierItem(item, pricingDetail.UnitaMisuraPrincipale))
            .ToList();
        QuantityTiersListBox.ItemsSource = QuantityTiers;

        ArticleRulesTextBlock.Text = BuildRulesLabel(pricingDetail);
        QuantityTextBox.Text = FormatEditableQuantity(normalizedInitialQuantity);
        Loaded += (_, _) =>
        {
            if (QuantityTiers.Count > 0)
            {
                var preferredTierIndex = QuantityTiers
                    .Select((item, index) => new { item, index })
                    .FirstOrDefault(entry => entry.item.QuantitaMinima >= normalizedInitialQuantity)?.index
                    ?? (QuantityTiers.Count - 1);
                QuantityTiersListBox.SelectedIndex = preferredTierIndex;
                QuantityTiersListBox.UpdateLayout();
                QuantityTiersListBox.ScrollIntoView(QuantityTiersListBox.SelectedItem);

                if (QuantityTiersListBox.ItemContainerGenerator.ContainerFromIndex(preferredTierIndex) is ListBoxItem selectedItem)
                {
                    selectedItem.Focus();
                }
                else
                {
                    QuantityTiersListBox.Focus();
                }
            }
            else
            {
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
            }

            UpdateAppliedPricePreview();
        };
    }

    public string Eyebrow { get; }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    public string FooterHint { get; }

    public IReadOnlyList<ArticleQuantityTierItem> QuantityTiers { get; }

    public decimal? SelectedQuantity { get; private set; }

    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        var quantity = ParseQuantity(QuantityTextBox.Text);
        if (!quantity.HasValue || quantity.Value <= 0)
        {
            QuantityTextBox.Focus();
            QuantityTextBox.SelectAll();
            return;
        }

        var normalizedQuantity = NormalizeQuantity(quantity.Value);
        QuantityTextBox.Text = FormatEditableQuantity(normalizedQuantity);
        SelectedQuantity = normalizedQuantity;
        DialogResult = true;
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void QuantityTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateAppliedPricePreview();
    }

    private void QuantityTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        PrimaryButton_OnClick(sender, e);
        e.Handled = true;
    }

    private void QuantityTiersListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuantityTiersListBox.SelectedItem is not ArticleQuantityTierItem tier)
        {
            return;
        }

        QuantityTextBox.Text = FormatEditableQuantity(tier.QuantitaMinima);
    }

    private void QuantityTiersListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QuantityTiersListBox.SelectedItem is not ArticleQuantityTierItem)
        {
            return;
        }

        PrimaryButton_OnClick(sender, e);
    }

    private void QuantityTiersListBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (TrySelectTierFromShortcutKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        PrimaryButton_OnClick(sender, e);
        e.Handled = true;
    }

    private void QuantityTiersListBoxItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem item)
        {
            return;
        }

        item.IsSelected = true;
        item.Focus();
        e.Handled = true;
    }

    private void UpdateAppliedPricePreview()
    {
        var quantity = ParseQuantity(QuantityTextBox.Text);
        if (!quantity.HasValue || quantity.Value <= 0)
        {
            AppliedPriceTextBlock.Text = "Inserisci una quantita` valida per vedere il prezzo applicato.";
            return;
        }

        var normalizedQuantity = NormalizeQuantity(quantity.Value);
        var appliedTier = _pricingDetail.FascePrezzoQuantita
            .Where(item => normalizedQuantity >= item.QuantitaMinima)
            .OrderByDescending(item => item.QuantitaMinima)
            .FirstOrDefault();

        if (appliedTier is null)
        {
            AppliedPriceTextBlock.Text = $"Q.ta` {normalizedQuantity:N2} {_pricingDetail.UnitaMisuraPrincipale} | nessuna fascia specifica";
            return;
        }

        var totale = appliedTier.PrezzoUnitario * normalizedQuantity;
        AppliedPriceTextBlock.Text = $"{appliedTier.PrezzoUnitario:N2} / {_pricingDetail.UnitaMisuraPrincipale} | Totale riga {totale:N2}";
    }

    private decimal? ParseQuantity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, _culture, out var quantity))
        {
            return quantity;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity)
            ? quantity
            : null;
    }

    private string FormatEditableQuantity(decimal quantity)
    {
        return quantity.ToString("0.############################", _culture);
    }

    private decimal NormalizeQuantity(decimal requestedQuantity)
    {
        var normalized = requestedQuantity <= 0 ? 1 : requestedQuantity;

        var quantitaMinima = _pricingDetail.QuantitaMinimaVendita <= 0 ? 1 : _pricingDetail.QuantitaMinimaVendita;
        normalized = Math.Max(normalized, quantitaMinima);

        var quantitaMultipla = _pricingDetail.QuantitaMultiplaVendita <= 0 ? 1 : _pricingDetail.QuantitaMultiplaVendita;
        if (quantitaMultipla > 1)
        {
            normalized = Math.Ceiling(normalized / quantitaMultipla) * quantitaMultipla;
        }

        return normalized;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (TrySelectTierFromShortcutKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private bool TrySelectTierFromShortcutKey(Key key)
    {
        var index = key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1
        };

        if (index < 0 || index >= QuantityTiers.Count)
        {
            return false;
        }

        QuantityTiersListBox.SelectedIndex = index;
        QuantityTiersListBox.UpdateLayout();
        QuantityTiersListBox.ScrollIntoView(QuantityTiersListBox.SelectedItem);

        if (QuantityTiersListBox.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
        {
            item.Focus();
        }
        else
        {
            QuantityTiersListBox.Focus();
        }

        return true;
    }

    private static string BuildRulesLabel(GestionaleArticlePricingDetail pricingDetail)
    {
        var chunks = new List<string>
        {
            $"U.M. principale: {pricingDetail.UnitaMisuraPrincipale}."
        };

        if (pricingDetail.HasSecondaryUnit)
        {
            chunks.Add($"Confezione legacy: {pricingDetail.UnitaMisuraSecondaria} = {pricingDetail.MoltiplicatoreUnitaSecondaria:N0} {pricingDetail.UnitaMisuraPrincipale}.");
        }

        if (pricingDetail.QuantitaMinimaVendita > 1)
        {
            chunks.Add($"Q.ta` minima: {pricingDetail.QuantitaMinimaVendita:N0}.");
        }

        if (pricingDetail.QuantitaMultiplaVendita > 1)
        {
            chunks.Add($"Q.ta` multipla: {pricingDetail.QuantitaMultiplaVendita:N0}.");
        }

        return string.Join(" ", chunks);
    }

    public sealed class ArticleQuantityTierItem
    {
        public ArticleQuantityTierItem(GestionaleArticleQuantityPriceTier tier, string unitaMisuraPrincipale)
        {
            QuantitaMinima = tier.QuantitaMinima;
            QuantitaLabel = $"{tier.QuantitaMinima:N0} {unitaMisuraPrincipale}";
            PrezzoLabel = $"{tier.PrezzoUnitario:N2} €";
            TotaleLabel = tier.QuantitaMinima > 1
                ? $"x{tier.QuantitaMinima:N0}"
                : string.Empty;
        }

        public decimal QuantitaMinima { get; }

        public string QuantitaLabel { get; }

        public string PrezzoLabel { get; }

        public string TotaleLabel { get; }
    }
}
