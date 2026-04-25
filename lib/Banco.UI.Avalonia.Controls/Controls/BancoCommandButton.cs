using Avalonia;
using Avalonia.Controls;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed class BancoCommandButton : Button
{
    public static readonly StyledProperty<BancoCommandButtonVariant> VariantProperty =
        AvaloniaProperty.Register<BancoCommandButton, BancoCommandButtonVariant>(
            nameof(Variant),
            BancoCommandButtonVariant.Secondary);

    public BancoCommandButton()
    {
        Classes.Add("bancoCommandButton");
        UpdateVariantClass();
    }

    public BancoCommandButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == VariantProperty)
        {
            UpdateVariantClass();
        }
    }

    private void UpdateVariantClass()
    {
        Classes.Remove("primary");
        Classes.Remove("secondary");
        Classes.Remove("danger");
        Classes.Add(Variant switch
        {
            BancoCommandButtonVariant.Primary => "primary",
            BancoCommandButtonVariant.Danger => "danger",
            _ => "secondary"
        });
    }
}

public enum BancoCommandButtonVariant
{
    Primary,
    Secondary,
    Danger
}
