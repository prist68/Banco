using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Banco.UI.Shared.Views;

namespace Banco.UI.Shared.Services;

/// <summary>
/// Gestione del colore della parte superiore della griglia, per griglia Banco o Documenti.
/// Ogni griglia salva la propria preferenza in modo indipendente.
/// </summary>
public static class BancoGridHeaderColorService
{
    private const string DefaultPaletteKey = "acquerello_cielo";

    private static readonly IReadOnlyDictionary<string, HeaderPalette> Palettes =
        new Dictionary<string, HeaderPalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["acquerello_cielo"] = new(ParseColor("#FFEAF3FF"), ParseColor("#FF1F3250"), "Acquerello cielo"),
            ["acquerello_menta"] = new(ParseColor("#FFEAF9F0"), ParseColor("#FF1F3250"), "Acquerello menta"),
            ["acquerello_lavanda"] = new(ParseColor("#FFF4EDFB"), ParseColor("#FF1F3250"), "Acquerello lavanda"),
            ["acquerello_pesca"] = new(ParseColor("#FFFDF1EA"), ParseColor("#FF1F3250"), "Acquerello pesca"),
            ["acquerello_perla"] = new(ParseColor("#FFF4F7FB"), ParseColor("#FF1F3250"), "Acquerello perla"),
        };

    public static void CaricaEApplica(string gridKey)
    {
        var saved = CaricaValore(gridKey);
        if (string.IsNullOrWhiteSpace(saved))
        {
            ApplicaPaletta(gridKey, DefaultPaletteKey, persist: false);
            return;
        }

        if (Palettes.ContainsKey(saved))
        {
            ApplicaPaletta(gridKey, saved, persist: false);
            return;
        }

        if (TryParseColor(saved, out var color))
        {
            ApplicaColorePersonalizzato(gridKey, color, persist: false);
            return;
        }

        ApplicaPaletta(gridKey, DefaultPaletteKey, persist: false);
    }

    public static void ApplicaPaletta(string gridKey, string paletteKey)
    {
        ApplicaPaletta(gridKey, paletteKey, persist: true);
    }

    public static void ApplicaColorePersonalizzato(string gridKey, Color colore)
    {
        ApplicaColorePersonalizzato(gridKey, colore, persist: true);
    }

    public static MenuItem CreaMenu(string gridKey)
    {
        var current = CaricaValore(gridKey) ?? DefaultPaletteKey;
        var menu = new MenuItem { Header = "Colore parte superiore" };

        foreach (var (paletteKey, palette) in Palettes)
        {
            var swatch = new System.Windows.Shapes.Rectangle
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(palette.Color),
                Stroke = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                StrokeThickness = 1,
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var item = new MenuItem
            {
                Header = palette.Label,
                Tag = paletteKey,
                Icon = swatch
            };

            if (string.Equals(current, paletteKey, StringComparison.OrdinalIgnoreCase))
            {
                item.FontWeight = FontWeights.Bold;
            }

            item.Click += (_, _) => ApplicaPaletta(gridKey, paletteKey);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var custom = new MenuItem
        {
            Header = "A piacere..."
        };
        custom.Click += (_, _) =>
        {
            var initial = GetCurrentColor(gridKey);
            if (!TrySelectCustomColor(initial, out var selected))
            {
                return;
            }

            ApplicaColorePersonalizzato(gridKey, selected);
        };
        menu.Items.Add(custom);

        return menu;
    }

    private static void ApplicaPaletta(string gridKey, string paletteKey, bool persist)
    {
        if (!Palettes.TryGetValue(paletteKey, out var palette))
        {
            palette = Palettes[DefaultPaletteKey];
            paletteKey = DefaultPaletteKey;
        }

        ApplyToResources(gridKey, palette.Color, palette.Foreground);

        if (persist)
        {
            PersistiValore(gridKey, paletteKey);
        }
    }

    private static void ApplicaColorePersonalizzato(string gridKey, Color color, bool persist)
    {
        var foreground = GetContrastingForeground(color);
        ApplyToResources(gridKey, color, foreground);

        if (persist)
        {
            PersistiValore(gridKey, ToHex(color));
        }
    }

    private static void ApplyToResources(string gridKey, Color background, Color foreground)
    {
        var resources = Application.Current.Resources;
        resources[GetBackgroundResourceKey(gridKey)] = new SolidColorBrush(background);
        resources[GetForegroundResourceKey(gridKey)] = new SolidColorBrush(foreground);
    }

    private static string GetBackgroundResourceKey(string gridKey) => gridKey switch
    {
        "Banco" => "BancoGridHeaderBrush",
        "Documenti" => "DocumentListGridHeaderBrush",
        _ => $"GridHeaderBrush_{gridKey}"
    };

    private static string GetForegroundResourceKey(string gridKey) => gridKey switch
    {
        "Banco" => "BancoGridHeaderForegroundBrush",
        "Documenti" => "DocumentListGridHeaderForegroundBrush",
        _ => $"GridHeaderForegroundBrush_{gridKey}"
    };

    private static string GetFilePath(string gridKey)
    {
        var fileName = gridKey switch
        {
            "Banco" => "grid_header_banco.json",
            "Documenti" => "grid_header_documenti.json",
            _ => $"grid_header_{gridKey.ToLowerInvariant()}.json"
        };

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Banco",
            fileName);
    }

    private static string? CaricaValore(string gridKey)
    {
        try
        {
            var filePath = GetFilePath(gridKey);
            if (!File.Exists(filePath))
            {
                return null;
            }

            using var stream = File.OpenRead(filePath);
            var doc = JsonDocument.Parse(stream);
            return doc.RootElement.TryGetProperty("Value", out var prop) ? prop.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void PersistiValore(string gridKey, string value)
    {
        try
        {
            var filePath = GetFilePath(gridKey);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(new { Value = value });
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Preferiamo mantenere il colore in memoria anche se il salvataggio fallisce.
        }
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(value)!;
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }

    private static Color ParseColor(string value)
        => (Color)ColorConverter.ConvertFromString(value)!;

    private static Color GetCurrentColor(string gridKey)
    {
        var saved = CaricaValore(gridKey);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            if (Palettes.TryGetValue(saved, out var palette))
            {
                return palette.Color;
            }

            if (TryParseColor(saved, out var parsed))
            {
                return parsed;
            }
        }

        return Palettes[DefaultPaletteKey].Color;
    }

    private static bool TrySelectCustomColor(Color initialColor, out Color selected)
    {
        var dialog = new GridColorPickerWindow(initialColor)
        {
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            selected = default;
            return false;
        }

        selected = dialog.SelectedColor;
        return true;
    }

    private static Color GetContrastingForeground(Color background)
    {
        var luminance = (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B);
        return luminance < 150 ? Colors.White : Color.FromRgb(31, 50, 80);
    }

    private static string ToHex(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private sealed record HeaderPalette(Color Color, Color Foreground, string Label);
}
