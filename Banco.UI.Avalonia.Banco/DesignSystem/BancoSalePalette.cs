using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Banco.UI.Avalonia.Banco.DesignSystem;

public enum BancoSaleThemeKind
{
    Light,
    Dark
}

public static class BancoSalePalette
{
    public static string GetToggleLabel(BancoSaleThemeKind theme)
    {
        return theme == BancoSaleThemeKind.Dark ? "Light" : "Dark";
    }

    public static void Apply(Window window, BancoSaleThemeKind theme)
    {
        var isDark = theme == BancoSaleThemeKind.Dark;
        window.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;

        SetBrush(window, "PageBackground", isDark ? "#0D1420" : "#EAF1F8");
        SetBrush(window, "TopBarBackground", isDark ? "#111B2A" : "#FFFFFF");
        SetBrush(window, "PanelSurface", isDark ? "#121D2D" : "#F7FAFE");
        SetBrush(window, "ElevatedSurface", isDark ? "#18263A" : "#FFFFFF");
        SetBrush(window, "SoftSurface", isDark ? "#1A2A40" : "#F3F7FC");
        SetBrush(window, "InputSurface", isDark ? "#0F1928" : "#FFFFFF");
        SetBrush(window, "Stroke", isDark ? "#263A55" : "#D6E2EF");
        SetBrush(window, "StrokeStrong", isDark ? "#385273" : "#BFD1E6");
        SetBrush(window, "MainText", isDark ? "#E7EEF8" : "#142238");
        SetBrush(window, "MutedText", isDark ? "#94A8C2" : "#536A85");
        SetBrush(window, "Accent", isDark ? "#0C8B6A" : "#0FA978");
        SetBrush(window, "AccentSoft", isDark ? "#173A32" : "#E6F7F1");
        SetBrush(window, "Info", isDark ? "#5F9EF4" : "#4F86DC");
        SetBrush(window, "Warning", isDark ? "#D99A35" : "#D49327");
        SetBrush(window, "Danger", isDark ? "#E06161" : "#D94B4B");
        SetBrush(window, "Success", isDark ? "#49B983" : "#42A873");

        SetBrush(window, "BancoAccentBrush", isDark ? "#0C8B6A" : "#0FA978");
        SetBrush(window, "BancoDangerBrush", isDark ? "#E06161" : "#D94B4B");
        SetBrush(window, "BancoWarningBrush", isDark ? "#D99A35" : "#D49327");
        SetBrush(window, "BancoSuccessBrush", isDark ? "#49B983" : "#42A873");
        SetBrush(window, "BancoMainTextBrush", isDark ? "#E7EEF8" : "#142238");
        SetBrush(window, "BancoMutedTextBrush", isDark ? "#94A8C2" : "#536A85");
        SetBrush(window, "BancoPanelSurfaceBrush", isDark ? "#121D2D" : "#F7FAFE");
        SetBrush(window, "BancoElevatedSurfaceBrush", isDark ? "#18263A" : "#FFFFFF");
        SetBrush(window, "BancoSoftSurfaceBrush", isDark ? "#1A2A40" : "#F3F7FC");
        SetBrush(window, "BancoInputSurfaceBrush", isDark ? "#0F1928" : "#FFFFFF");
        SetBrush(window, "BancoStrokeBrush", isDark ? "#263A55" : "#D6E2EF");
        SetBrush(window, "BancoStrokeStrongBrush", isDark ? "#385273" : "#BFD1E6");
    }

    private static void SetBrush(Window window, string key, string color)
    {
        window.Resources[key] = SolidColorBrush.Parse(color);
    }
}
