using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Banco.UI.Avalonia.Banco.ViewModels;
using Banco.Vendita.Articles;

namespace Banco.UI.Avalonia.Banco.Views;

public sealed partial class NegativeAvailabilityDialog : Window
{
    public NegativeAvailabilityDialog()
    {
        InitializeComponent();
    }

    public NegativeAvailabilityDialog(GestionaleArticleSearchResult article, decimal requestedQuantity)
        : this()
    {
        ArticleLabel = article.DisplayLabel;
        HighlightText = string.Create(
            CultureInfo.GetCultureInfo("it-IT"),
            $"Giacenza attuale: {article.Giacenza:N2} | Quantita richiesta: {requestedQuantity:N2}");
        DataContext = this;
    }

    public string ArticleLabel { get; } = string.Empty;

    public string HighlightText { get; } = string.Empty;

    public NegativeAvailabilityDecision Decision { get; private set; } = NegativeAvailabilityDecision.Annulla;

    private void AddToReorderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.VendiEAggiungiALista;
        Close(true);
    }

    private void ForceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.ScaricaComunque;
        Close(true);
    }

    private void ManualButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.ConvertiInManuale;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.Annulla;
        Close(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                Decision = NegativeAvailabilityDecision.ConvertiInManuale;
                Close(true);
                e.Handled = true;
                return;
            case Key.F8:
                Decision = NegativeAvailabilityDecision.VendiEAggiungiALista;
                Close(true);
                e.Handled = true;
                return;
            case Key.F9:
                Decision = NegativeAvailabilityDecision.ScaricaComunque;
                Close(true);
                e.Handled = true;
                return;
        }

        base.OnKeyDown(e);
    }
}
