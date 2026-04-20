using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace Banco.UI.Wpf.Infrastructure.Selection;

/// <summary>
/// Servizio centralizzato per l'effetto luce corsa sulla riga selezionata delle DataGrid.
/// Si attiva con un solo setter nel RowStyle di qualsiasi griglia:
/// <code>
///   &lt;Setter Property="sel:SelezioneLuceCorsaService.IsAbilitato" Value="True" /&gt;
/// </code>
/// Compatibile con virtualizzazione, multi-selezione, alternanza colori e RowHeader.
/// Non interferisce con <c>BancoGrigliaColoriService</c> ne` con le palette alternate.
/// </summary>
public static class SelezioneLuceCorsaService
{
    /// <summary>
    /// Proprietà globale per abilitare/disabilitare la sola animazione.
    /// Se false, sfondo semitrasparente e bordo restano visibili ma la luce non si muove.
    /// Modificabile a runtime senza riavviare l'applicazione.
    /// </summary>
    public static bool AnimazioneAbilitata { get; set; } = true;

    // ── Attached property per opt-in ────────────────────────────────

    public static readonly DependencyProperty IsAbilitatoProperty =
        DependencyProperty.RegisterAttached(
            "IsAbilitato",
            typeof(bool),
            typeof(SelezioneLuceCorsaService),
            new PropertyMetadata(false, OnIsAbilitatoChanged));

    public static bool GetIsAbilitato(DependencyObject obj) => (bool)obj.GetValue(IsAbilitatoProperty);
    public static void SetIsAbilitato(DependencyObject obj, bool value) => obj.SetValue(IsAbilitatoProperty, value);

    // ── Stato per riga (ConditionalWeakTable: non previene il GC) ───

    private static readonly ConditionalWeakTable<DataGridRow, RigaStato> _stati = new();

    private sealed class RigaStato
    {
        public LuceCorsaAdorner? Adorner;
        public bool EventiRegistrati;
    }

    // ── Gestione ciclo di vita ──────────────────────────────────────

    private static void OnIsAbilitatoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGridRow riga)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Registra(riga);
        }
        else
        {
            Deregistra(riga);
        }
    }

    private static void Registra(DataGridRow riga)
    {
        var stato = _stati.GetOrCreateValue(riga);
        if (stato.EventiRegistrati)
        {
            return; // gia` registrato, evita duplicazione
        }

        riga.Selected += Riga_Selected;
        riga.Unselected += Riga_Unselected;
        riga.Loaded += Riga_Loaded;
        riga.Unloaded += Riga_Unloaded;
        stato.EventiRegistrati = true;

        // Se la riga e` gia` selezionata e caricata, aggiungi subito l'adorner
        if (riga.IsLoaded && riga.IsSelected)
        {
            AggiungiAdorner(riga, stato);
        }
    }

    private static void Deregistra(DataGridRow riga)
    {
        if (!_stati.TryGetValue(riga, out var stato))
        {
            return;
        }

        riga.Selected -= Riga_Selected;
        riga.Unselected -= Riga_Unselected;
        riga.Loaded -= Riga_Loaded;
        riga.Unloaded -= Riga_Unloaded;
        stato.EventiRegistrati = false;

        RimuoviAdorner(riga, stato);
        _stati.Remove(riga);
    }

    // ── Handler eventi riga ─────────────────────────────────────────

    private static void Riga_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is DataGridRow riga && _stati.TryGetValue(riga, out var stato))
        {
            AggiungiAdorner(riga, stato);
        }
    }

    private static void Riga_Unselected(object sender, RoutedEventArgs e)
    {
        if (sender is DataGridRow riga && _stati.TryGetValue(riga, out var stato))
        {
            RimuoviAdorner(riga, stato);
        }
    }

    private static void Riga_Loaded(object sender, RoutedEventArgs e)
    {
        // Riaggiungi l'adorner quando la riga torna visibile dopo virtualizzazione/scroll
        if (sender is DataGridRow riga && riga.IsSelected
            && _stati.TryGetValue(riga, out var stato))
        {
            AggiungiAdorner(riga, stato);
        }
    }

    private static void Riga_Unloaded(object sender, RoutedEventArgs e)
    {
        // Rimuovi l'adorner quando la riga esce dalla viewport (virtualizzazione)
        if (sender is DataGridRow riga && _stati.TryGetValue(riga, out var stato))
        {
            RimuoviAdorner(riga, stato);
        }
    }

    // ── Gestione adorner ────────────────────────────────────────────

    private static void AggiungiAdorner(DataGridRow riga, RigaStato stato)
    {
        // Non duplicare adorner gia` presenti
        if (stato.Adorner is not null)
        {
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(riga);
        if (layer is null)
        {
            // Layer non ancora disponibile: riprova al prossimo layout pass
            riga.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (riga.IsSelected && riga.IsLoaded && stato.Adorner is null)
                {
                    AggiungiAdorner(riga, stato);
                }
            });
            return;
        }

        var adorner = new LuceCorsaAdorner(riga);
        layer.Add(adorner);
        stato.Adorner = adorner;

        if (AnimazioneAbilitata)
        {
            adorner.AvviaAnimazione();
        }
    }

    private static void RimuoviAdorner(DataGridRow riga, RigaStato stato)
    {
        if (stato.Adorner is null)
        {
            return;
        }

        stato.Adorner.FermaAnimazione();

        var layer = AdornerLayer.GetAdornerLayer(riga);
        layer?.Remove(stato.Adorner);

        stato.Adorner = null;
    }
}
