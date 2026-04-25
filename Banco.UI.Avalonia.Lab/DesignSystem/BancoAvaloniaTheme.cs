using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Banco.UI.Avalonia.Lab.DesignSystem;

public enum BancoAvaloniaThemeKind
{
    Light,
    Dark
}

public static class BancoAvaloniaTheme
{
    public static string GetToggleLabel(BancoAvaloniaThemeKind theme)
    {
        return theme == BancoAvaloniaThemeKind.Dark ? "Light" : "Dark";
    }

    public static void Apply(Window window, BancoAvaloniaThemeKind theme)
    {
        var isDarkTheme = theme == BancoAvaloniaThemeKind.Dark;
        window.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;

        SetBrush(window, "PageBackground", isDarkTheme ? "#0D1420" : "#EAF1F8");
        SetBrush(window, "TopBarBackground", isDarkTheme ? "#111B2A" : "#FFFFFF");
        SetBrush(window, "RailBackground", isDarkTheme ? "#0B1220" : "#1E3554");
        SetBrush(window, "HeroSurface", isDarkTheme ? "#162236" : "#F8FBFF");
        SetBrush(window, "PanelSurface", isDarkTheme ? "#121D2D" : "#F7FAFE");
        SetBrush(window, "ElevatedSurface", isDarkTheme ? "#18263A" : "#FFFFFF");
        SetBrush(window, "SoftSurface", isDarkTheme ? "#1A2A40" : "#F3F7FC");
        SetBrush(window, "GlassSurface", isDarkTheme ? "#17283F" : "#EEF5FC");
        SetBrush(window, "InputSurface", isDarkTheme ? "#0F1928" : "#FFFFFF");
        SetBrush(window, "Stroke", isDarkTheme ? "#263A55" : "#D6E2EF");
        SetBrush(window, "StrokeStrong", isDarkTheme ? "#385273" : "#BFD1E6");
        SetBrush(window, "MainText", isDarkTheme ? "#E7EEF8" : "#142238");
        SetBrush(window, "MutedText", isDarkTheme ? "#94A8C2" : "#536A85");
        SetBrush(window, "RailText", "#FFFFFF");
        SetBrush(window, "RailMuted", isDarkTheme ? "#6F86A5" : "#AFC2D9");
        SetBrush(window, "AccentSoft", isDarkTheme ? "#213A60" : "#EAF2FF");
        SetBrush(window, "AccentText", isDarkTheme ? "#9FC5FF" : "#2E6CBD");
        SetBrush(window, "BrandTile", isDarkTheme ? "#0C8B6A" : "#0FA978");
    }

    private static void SetBrush(Window window, string key, string color)
    {
        window.Resources[key] = SolidColorBrush.Parse(color);
    }
}
