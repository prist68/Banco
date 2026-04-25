using Avalonia.Controls;
using Avalonia.Interactivity;
using Banco.UI.Avalonia.Lab.DesignSystem;
using Banco.UI.Avalonia.Lab.ViewModels;

namespace Banco.UI.Avalonia.Lab;

public sealed partial class MainWindow : Window
{
    private BancoAvaloniaThemeKind _currentTheme = BancoAvaloniaThemeKind.Light;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new DesktopPrototypeViewModel();
        ApplyTheme(BancoAvaloniaThemeKind.Light);
    }

    private void ThemeToggle_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme == BancoAvaloniaThemeKind.Light
            ? BancoAvaloniaThemeKind.Dark
            : BancoAvaloniaThemeKind.Light;
        ApplyTheme(_currentTheme);
    }

    private void ApplyTheme(BancoAvaloniaThemeKind theme)
    {
        BancoAvaloniaTheme.Apply(this, theme);
        ThemeGlyph.Text = BancoAvaloniaTheme.GetToggleLabel(theme);
    }
}
