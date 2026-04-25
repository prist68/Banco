using Avalonia;
using Avalonia.Controls;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed partial class BancoGridToolbar : UserControl
{
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<BancoGridToolbar, string>(nameof(SearchText), string.Empty);

    public static readonly StyledProperty<string> SearchWatermarkProperty =
        AvaloniaProperty.Register<BancoGridToolbar, string>(nameof(SearchWatermark), "Cerca...");

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<BancoGridToolbar, object?>(nameof(Actions));

    public BancoGridToolbar()
    {
        InitializeComponent();
    }

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string SearchWatermark
    {
        get => GetValue(SearchWatermarkProperty);
        set => SetValue(SearchWatermarkProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
