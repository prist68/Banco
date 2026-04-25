using Avalonia;
using Avalonia.Controls;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed partial class BancoPanel : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<BancoPanel, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<BancoPanel, string>(nameof(Subtitle), string.Empty);

    public static readonly StyledProperty<object?> BodyProperty =
        AvaloniaProperty.Register<BancoPanel, object?>(nameof(Body));

    public BancoPanel()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
}
