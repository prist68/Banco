using Avalonia;
using Avalonia.Controls;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed partial class BancoSearchBox : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<BancoSearchBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<BancoSearchBox, string>(nameof(Watermark), "Cerca...");

    public BancoSearchBox()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }
}
