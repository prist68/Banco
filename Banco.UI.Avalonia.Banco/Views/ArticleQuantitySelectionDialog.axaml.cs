using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Banco.Vendita.Articles;

namespace Banco.UI.Avalonia.Banco.Views;

public sealed partial class ArticleQuantitySelectionDialog : Window
{
    private readonly GestionaleArticlePricingDetail _pricingDetail;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("it-IT");

    public ArticleQuantitySelectionDialog()
    {
        InitializeComponent();
        _pricingDetail = null!;
    }

    public ArticleQuantitySelectionDialog(
        GestionaleArticleSearchResult article,
        GestionaleArticlePricingDetail pricingDetail,
        decimal initialQuantity)
        : this()
    {
        _pricingDetail = pricingDetail;
        DialogTitle = $"{article.CodiceArticolo} - {article.Descrizione}";
        RulesText = BuildRulesLabel(pricingDetail);
        QuantityTiers = new ObservableCollection<ArticleQuantityTierItem>(
            pricingDetail.FascePrezzoQuantita
                .OrderBy(item => item.QuantitaMinima)
                .Select(item => new ArticleQuantityTierItem(item, pricingDetail.UnitaMisuraPrincipale)));

        DataContext = this;
        QuantityTiersListBox.ItemsSource = QuantityTiers;
        QuantityTextBox.Text = FormatEditableQuantity(NormalizeQuantity(initialQuantity));
        Loaded += OnLoaded;
    }

    public string DialogTitle { get; } = string.Empty;

    public string RulesText { get; } = string.Empty;

    public ObservableCollection<ArticleQuantityTierItem> QuantityTiers { get; } = [];

    public decimal? SelectedQuantity { get; private set; }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (QuantityTiers.Count > 0)
        {
            var quantity = ParseQuantity(QuantityTextBox.Text) ?? 1;
            var preferredTier = QuantityTiers
                .Select((item, index) => new { item, index })
                .FirstOrDefault(entry => entry.item.QuantitaMinima >= quantity)?.index
                ?? QuantityTiers.Count - 1;
            QuantityTiersListBox.SelectedIndex = preferredTier;
            QuantityTiersListBox.Focus();
        }
        else
        {
            QuantityTextBox.Focus();
            QuantityTextBox.SelectAll();
        }

        UpdateAppliedPricePreview();
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void QuantityTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateAppliedPricePreview();
    }

    private void QuantityTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    private void QuantityTiersListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (QuantityTiersListBox.SelectedItem is ArticleQuantityTierItem tier)
        {
            QuantityTextBox.Text = FormatEditableQuantity(tier.QuantitaMinima);
        }
    }

    private void QuantityTiersListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        ConfirmSelection();
    }

    private void QuantityTiersListBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (TrySelectTierFromShortcutKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ConfirmSelection();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TrySelectTierFromShortcutKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void ConfirmSelection()
    {
        var quantity = ParseQuantity(QuantityTextBox.Text);
        if (!quantity.HasValue || quantity.Value <= 0)
        {
            QuantityTextBox.Focus();
            QuantityTextBox.SelectAll();
            return;
        }

        SelectedQuantity = NormalizeQuantity(quantity.Value);
        QuantityTextBox.Text = FormatEditableQuantity(SelectedQuantity.Value);
        Close(true);
    }

    private void UpdateAppliedPricePreview()
    {
        var quantity = ParseQuantity(QuantityTextBox.Text);
        if (!quantity.HasValue || quantity.Value <= 0)
        {
            AppliedPriceTextBlock.Text = "Inserisci una quantita valida per vedere il prezzo applicato.";
            return;
        }

        var normalizedQuantity = NormalizeQuantity(quantity.Value);
        var tier = _pricingDetail.FascePrezzoQuantita
            .Where(item => normalizedQuantity >= item.QuantitaMinima)
            .OrderByDescending(item => item.QuantitaMinima)
            .FirstOrDefault();

        AppliedPriceTextBlock.Text = tier is null
            ? $"Q.ta {normalizedQuantity:N2} {_pricingDetail.UnitaMisuraPrincipale} | nessuna fascia specifica"
            : $"{tier.PrezzoUnitario:N2} / {_pricingDetail.UnitaMisuraPrincipale} | Totale riga {(tier.PrezzoUnitario * normalizedQuantity):N2}";
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
        var minimumQuantity = _pricingDetail.QuantitaMinimaVendita <= 0 ? 1 : _pricingDetail.QuantitaMinimaVendita;
        normalized = Math.Max(normalized, minimumQuantity);

        var multipleQuantity = _pricingDetail.QuantitaMultiplaVendita <= 0 ? 1 : _pricingDetail.QuantitaMultiplaVendita;
        if (multipleQuantity > 1)
        {
            normalized = Math.Ceiling(normalized / multipleQuantity) * multipleQuantity;
        }

        return normalized;
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
        QuantityTiersListBox.ScrollIntoView(QuantityTiers[index]);
        QuantityTiersListBox.Focus();
        return true;
    }

    private static string BuildRulesLabel(GestionaleArticlePricingDetail pricingDetail)
    {
        var parts = new List<string> { $"U.M. principale: {pricingDetail.UnitaMisuraPrincipale}." };
        if (pricingDetail.HasSecondaryUnit)
        {
            parts.Add($"Confezione legacy: {pricingDetail.UnitaMisuraSecondaria} = {pricingDetail.MoltiplicatoreUnitaSecondaria:N0} {pricingDetail.UnitaMisuraPrincipale}.");
        }

        if (pricingDetail.QuantitaMinimaVendita > 1)
        {
            parts.Add($"Q.ta minima: {pricingDetail.QuantitaMinimaVendita:N0}.");
        }

        if (pricingDetail.QuantitaMultiplaVendita > 1)
        {
            parts.Add($"Q.ta multipla: {pricingDetail.QuantitaMultiplaVendita:N0}.");
        }

        return string.Join(" ", parts);
    }

    public sealed class ArticleQuantityTierItem
    {
        public ArticleQuantityTierItem(GestionaleArticleQuantityPriceTier tier, string unit)
        {
            QuantitaMinima = tier.QuantitaMinima;
            QuantitaLabel = $"{tier.QuantitaMinima:N0} {unit}";
            PrezzoLabel = $"{tier.PrezzoUnitario:N2} EUR";
            TotaleLabel = tier.QuantitaMinima > 1 ? $"x{tier.QuantitaMinima:N0}" : string.Empty;
        }

        public decimal QuantitaMinima { get; }

        public string QuantitaLabel { get; }

        public string PrezzoLabel { get; }

        public string TotaleLabel { get; }
    }
}
