using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Banco.UI.Wpf.Infrastructure.Selection;

/// <summary>
/// Adorner che disegna sfondo semitrasparente, bordo e luce animata
/// sulla riga selezionata di una DataGrid.
/// Il rendering interessa solo l'area celle, escludendo il RowHeader.
/// </summary>
internal sealed class LuceCorsaAdorner : Adorner
{
    /// <summary>
    /// Offset corrente della luce lungo il bordo (0.0 = sinistra, 1.0 = destra).
    /// Animato da <see cref="AvviaAnimazione"/>.
    /// </summary>
    public static readonly DependencyProperty AnimationOffsetProperty =
        DependencyProperty.Register(
            nameof(AnimationOffset),
            typeof(double),
            typeof(LuceCorsaAdorner),
            new PropertyMetadata(0.0, OnAnimationOffsetChanged));

    public double AnimationOffset
    {
        get => (double)GetValue(AnimationOffsetProperty);
        set => SetValue(AnimationOffsetProperty, value);
    }

    private static void OnAnimationOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((LuceCorsaAdorner)d).InvalidateVisual();
    }

    public LuceCorsaAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// Avvia l'animazione della luce che scorre lungo il bordo.
    /// </summary>
    public void AvviaAnimazione()
    {
        var animazione = new DoubleAnimation
        {
            From = -0.15,
            To = 1.15,
            Duration = TimeSpan.FromSeconds(2.5),
            RepeatBehavior = RepeatBehavior.Forever
        };
        BeginAnimation(AnimationOffsetProperty, animazione);
    }

    /// <summary>
    /// Ferma l'animazione senza rimuovere l'adorner.
    /// Sfondo e bordo statico restano visibili.
    /// </summary>
    public void FermaAnimazione()
    {
        BeginAnimation(AnimationOffsetProperty, null);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Calcola l'area delle sole celle (escluso RowHeader)
        var rect = CalcolaAreaCelle();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var risorse = Application.Current.Resources;

        // 1. Sfondo semitrasparente della selezione
        var sfondoBrush = risorse["GrigliaSelezioneSfondoBrush"] as Brush
                          ?? FallbackSfondo;
        drawingContext.DrawRectangle(sfondoBrush, null, rect);

        // Rettangolo con offset di mezzo pixel per rendering nitido del bordo
        var borderRect = new Rect(
            rect.X + 0.5, rect.Y + 0.5,
            rect.Width - 1.0, rect.Height - 1.0);

        // 2. Bordo statico della selezione
        var bordoBrush = risorse["GrigliaSelezioneBordoBrush"] as Brush
                         ?? FallbackBordo;
        var bordoPen = new Pen(bordoBrush, 1.0);
        drawingContext.DrawRectangle(null, bordoPen, borderRect);

        // 3. Luce animata (solo se abilitata globalmente)
        if (!SelezioneLuceCorsaService.AnimazioneAbilitata)
        {
            return;
        }

        var luceBrush = risorse["GrigliaSelezioneLuceBrush"] as SolidColorBrush;
        var coloreAccento = luceBrush?.Color ?? Color.FromRgb(0x3D, 0x73, 0xC6);

        var offset = AnimationOffset;
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };

        // Segmento luminoso: trasparente -> accento semitrasparente -> trasparente
        var coloreGlow = Color.FromArgb(0x80, coloreAccento.R, coloreAccento.G, coloreAccento.B);

        gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, Clamp(offset - 0.12)));
        gradientBrush.GradientStops.Add(new GradientStop(coloreGlow, Clamp(offset)));
        gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, Clamp(offset + 0.12)));

        var lucePen = new Pen(gradientBrush, 1.8);
        drawingContext.DrawRectangle(null, lucePen, borderRect);
    }

    // ── Calcolo area celle ──────────────────────────────────────────

    /// <summary>
    /// Calcola il rettangolo dell'area celle, escludendo il RowHeader.
    /// Cerca il DataGridCellsPresenter nel visual tree; in fallback
    /// usa il RowHeader per calcolare l'offset manuale.
    /// </summary>
    private Rect CalcolaAreaCelle()
    {
        if (AdornedElement is not DataGridRow riga)
        {
            return new Rect(0, 0, AdornedElement.RenderSize.Width, AdornedElement.RenderSize.Height);
        }

        // Primo tentativo: trova il presenter delle celle
        var cellsPresenter = TrovaElementoVisuale<DataGridCellsPresenter>(riga);
        if (cellsPresenter is not null)
        {
            var posizione = cellsPresenter.TranslatePoint(new Point(0, 0), riga);
            return new Rect(posizione.X, posizione.Y,
                            cellsPresenter.ActualWidth, cellsPresenter.ActualHeight);
        }

        // Fallback: offset in base alla larghezza del RowHeader
        var rowHeader = TrovaElementoVisuale<DataGridRowHeader>(riga);
        if (rowHeader is not null)
        {
            var offsetX = rowHeader.ActualWidth;
            return new Rect(offsetX, 0, riga.ActualWidth - offsetX, riga.ActualHeight);
        }

        // Ultimo fallback: intera riga
        return new Rect(0, 0, riga.ActualWidth, riga.ActualHeight);
    }

    // ── Utilita` ────────────────────────────────────────────────────

    private static T? TrovaElementoVisuale<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
            {
                return found;
            }

            var result = TrovaElementoVisuale<T>(child);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    private static double Clamp(double value) => Math.Max(0.0, Math.Min(1.0, value));

    // Brush di fallback nel caso le risorse non siano disponibili
    private static readonly Brush FallbackSfondo =
        new SolidColorBrush(Color.FromArgb(0x18, 0x3D, 0x73, 0xC6));
    private static readonly Brush FallbackBordo =
        new SolidColorBrush(Color.FromArgb(0x4D, 0x3D, 0x73, 0xC6));

    static LuceCorsaAdorner()
    {
        FallbackSfondo.Freeze();
        FallbackBordo.Freeze();
    }
}
