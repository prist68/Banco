using Avalonia;
using Avalonia.Controls;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed partial class BancoEmptyState : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<BancoEmptyState, string>(nameof(Title), "Nessun dato");

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<BancoEmptyState, string>(nameof(Message), "Non ci sono elementi da mostrare.");

    public BancoEmptyState()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}
