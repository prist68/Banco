using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Banco.UI.Grid.Core.Grid;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed class BancoStatusBadge : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<BancoStatusBadge, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<BancoGridColorRole> ColorRoleProperty =
        AvaloniaProperty.Register<BancoStatusBadge, BancoGridColorRole>(
            nameof(ColorRole),
            BancoGridColorRole.None);

    private readonly TextBlock _textBlock = new()
    {
        FontSize = 11,
        FontWeight = FontWeight.SemiBold
    };

    public BancoStatusBadge()
    {
        Classes.Add("bancoStatusBadge");
        Child = _textBlock;
        UpdateVisualState();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BancoGridColorRole ColorRole
    {
        get => GetValue(ColorRoleProperty);
        set => SetValue(ColorRoleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            _textBlock.Text = Text;
        }

        if (change.Property == ColorRoleProperty)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        var color = ColorRole switch
        {
            BancoGridColorRole.Success => "#42A873",
            BancoGridColorRole.Warning => "#D49327",
            BancoGridColorRole.Danger => "#D94B4B",
            BancoGridColorRole.Accent => "#0FA978",
            BancoGridColorRole.Info => "#4F86DC",
            _ => "#5B7D91"
        };

        var brush = SolidColorBrush.Parse(color);
        BorderBrush = brush;
        _textBlock.Foreground = brush;
    }
}
