using System.Windows;
using System.Windows.Media;

namespace Banco.UI.Wpf.Views;

public partial class GridColorPickerWindow : Window
{
    public Color SelectedColor { get; private set; }

    public GridColorPickerWindow(Color initialColor)
    {
        InitializeComponent();
        SliderR.Value = initialColor.R;
        SliderG.Value = initialColor.G;
        SliderB.Value = initialColor.B;
        UpdatePreview();
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var color = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
        PreviewBorder.Background = new SolidColorBrush(color);
        PreviewLabel.Text = "Anteprima";
        PreviewHex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        ValueR.Text = ((byte)SliderR.Value).ToString();
        ValueG.Text = ((byte)SliderG.Value).ToString();
        ValueB.Text = ((byte)SliderB.Value).ToString();

        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        PreviewLabel.Foreground = new SolidColorBrush(luminance < 150 ? Colors.White : Color.FromRgb(31, 50, 80));
        PreviewHex.Foreground = new SolidColorBrush(luminance < 150 ? Colors.White : Color.FromRgb(31, 50, 80));
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        SelectedColor = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
