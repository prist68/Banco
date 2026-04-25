using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Articles;

namespace Banco.UI.Wpf.Views;

public partial class NegativeAvailabilityDialogWindow : Window
{
    public NegativeAvailabilityDialogWindow(GestionaleArticleSearchResult articolo, decimal quantitaRichiesta)
    {
        InitializeComponent();
        Eyebrow = "Banco / disponibilita`";
        DialogTitle = "Giacenza non disponibile";
        DialogMessage = "L'articolo selezionato ha giacenza zero o negativa. Scegli come proseguire prima di inserirlo nel documento.";
        ArticleLabel = articolo.DisplayLabel;
        HighlightText = string.Create(
            CultureInfo.GetCultureInfo("it-IT"),
            $"Giacenza attuale: {articolo.Giacenza:N2} | Quantita` richiesta: {quantitaRichiesta:N2}");
        DataContext = this;

        Loaded += (_, _) => AddToReorderButton.Focus();
    }

    public string Eyebrow { get; }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    public string ArticleLabel { get; }

    public string HighlightText { get; }

    public NegativeAvailabilityDecision Decision { get; private set; } = NegativeAvailabilityDecision.Annulla;

    private void AddToReorderButton_OnClick(object sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.VendiEAggiungiALista;
        DialogResult = true;
    }

    private void ForceButton_OnClick(object sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.ScaricaComunque;
        DialogResult = true;
    }

    private void ManualButton_OnClick(object sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.ConvertiInManuale;
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.Annulla;
        DialogResult = false;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Decision = NegativeAvailabilityDecision.Annulla;
        DialogResult = false;
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            Decision = NegativeAvailabilityDecision.ConvertiInManuale;
            DialogResult = true;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F8)
        {
            Decision = NegativeAvailabilityDecision.VendiEAggiungiALista;
            DialogResult = true;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F9)
        {
            Decision = NegativeAvailabilityDecision.ScaricaComunque;
            DialogResult = true;
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
