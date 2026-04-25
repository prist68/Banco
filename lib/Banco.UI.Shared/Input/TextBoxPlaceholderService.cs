using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Banco.UI.Shared.Input;

public static class TextBoxPlaceholderService
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(TextBoxPlaceholderService),
            new PropertyMetadata(string.Empty, OnPlaceholderChanged));

    public static string GetPlaceholder(DependencyObject obj) => (string)obj.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject obj, string value) => obj.SetValue(PlaceholderProperty, value);

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.Loaded -= TextBox_OnChanged;
        textBox.TextChanged -= TextBox_OnChanged;
        textBox.GotKeyboardFocus -= TextBox_OnChanged;
        textBox.LostKeyboardFocus -= TextBox_OnChanged;
        textBox.Unloaded -= TextBox_OnUnloaded;

        if (string.IsNullOrWhiteSpace(e.NewValue as string))
        {
            RemoveAdorner(textBox);
            return;
        }

        textBox.Loaded += TextBox_OnChanged;
        textBox.TextChanged += TextBox_OnChanged;
        textBox.GotKeyboardFocus += TextBox_OnChanged;
        textBox.LostKeyboardFocus += TextBox_OnChanged;
        textBox.Unloaded += TextBox_OnUnloaded;

        UpdateAdorner(textBox);
    }

    private static void TextBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateAdorner(textBox);
        }
    }

    private static void TextBox_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            RemoveAdorner(textBox);
        }
    }

    private static void UpdateAdorner(TextBox textBox)
    {
        if (!textBox.IsLoaded)
        {
            return;
        }

        var placeholder = GetPlaceholder(textBox);
        if (string.IsNullOrWhiteSpace(placeholder))
        {
            RemoveAdorner(textBox);
            return;
        }

        var layer = AdornerLayer.GetAdornerLayer(textBox);
        if (layer is null)
        {
            return;
        }

        var adorner = GetOrCreateAdorner(textBox, layer);
        adorner.Placeholder = placeholder;
        adorner.Visibility = string.IsNullOrEmpty(textBox.Text) && !textBox.IsKeyboardFocused
            ? Visibility.Visible
            : Visibility.Collapsed;
        adorner.InvalidateVisual();
    }

    private static PlaceholderAdorner GetOrCreateAdorner(TextBox textBox, AdornerLayer layer)
    {
        var adorners = layer.GetAdorners(textBox);
        if (adorners is not null)
        {
            foreach (var adorner in adorners)
            {
                if (adorner is PlaceholderAdorner placeholderAdorner)
                {
                    return placeholderAdorner;
                }
            }
        }

        var newAdorner = new PlaceholderAdorner(textBox);
        layer.Add(newAdorner);
        return newAdorner;
    }

    private static void RemoveAdorner(TextBox textBox)
    {
        var layer = AdornerLayer.GetAdornerLayer(textBox);
        var adorners = layer?.GetAdorners(textBox);
        if (adorners is null)
        {
            return;
        }

        foreach (var adorner in adorners)
        {
            if (adorner is PlaceholderAdorner)
            {
                layer!.Remove(adorner);
            }
        }
    }

    private sealed class PlaceholderAdorner : Adorner
    {
        private static readonly Brush PlaceholderBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x9A, 0xAF));

        public PlaceholderAdorner(TextBox adornedElement)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public string Placeholder { get; set; } = string.Empty;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (AdornedElement is not TextBox textBox || string.IsNullOrWhiteSpace(Placeholder))
            {
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var formattedText = new FormattedText(
                Placeholder,
                CultureInfo.GetCultureInfo("it-IT"),
                textBox.FlowDirection,
                new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize,
                PlaceholderBrush,
                dpi);

            var point = new Point(
                textBox.Padding.Left + 1,
                textBox.Padding.Top + 1);

            drawingContext.DrawText(formattedText, point);
        }
    }
}
