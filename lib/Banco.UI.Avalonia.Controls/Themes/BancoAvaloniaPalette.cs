using Avalonia.Controls;
using Avalonia.Media;

namespace Banco.UI.Avalonia.Controls.Themes;

public static class BancoAvaloniaPalette
{
    public static void ApplyDefault(Control host)
    {
        SetBrush(host, "BancoAccentBrush", "#0FA978");
        SetBrush(host, "BancoDangerBrush", "#D94B4B");
        SetBrush(host, "BancoWarningBrush", "#D49327");
        SetBrush(host, "BancoSuccessBrush", "#42A873");
        SetBrush(host, "BancoMainTextBrush", "#142238");
        SetBrush(host, "BancoMutedTextBrush", "#536A85");
        SetBrush(host, "BancoPanelSurfaceBrush", "#F7FAFE");
        SetBrush(host, "BancoElevatedSurfaceBrush", "#FFFFFF");
        SetBrush(host, "BancoSoftSurfaceBrush", "#F3F7FC");
        SetBrush(host, "BancoInputSurfaceBrush", "#FFFFFF");
        SetBrush(host, "BancoStrokeBrush", "#D6E2EF");
        SetBrush(host, "BancoStrokeStrongBrush", "#BFD1E6");
    }

    private static void SetBrush(Control host, string key, string color)
    {
        host.Resources[key] = SolidColorBrush.Parse(color);
    }
}
