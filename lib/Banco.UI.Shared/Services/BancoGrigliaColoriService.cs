using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Banco.UI.Shared.Services;

/// <summary>
/// Servizio statico per la gestione dei colori delle righe della griglia banco.
/// Richiamabile da qualsiasi parte dell'applicazione.
/// La palette scelta viene persistita in %LocalAppData%\Banco\griglia_colori.json
/// </summary>
public static class BancoGrigliaColoriService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Banco",
        "griglia_colori.json");

    private const string ChiavePredefinita = "azzurro";

    /// <summary>
    /// Palettes disponibili: chiave -> (colore riga pari, colore riga dispari).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PaletteGriglia> Palettes =
        new Dictionary<string, PaletteGriglia>(StringComparer.OrdinalIgnoreCase)
        {
            ["azzurro"]   = new("#FFFFFFFF", "#FFEEF5FC", "Bianco / Azzurro (predefinito)"),
            ["menta"]     = new("#FFFFFFFF", "#FFEDF8F1", "Bianco / Verde menta"),
            ["lavanda"]   = new("#FFFFFFFF", "#FFF3F0FA", "Bianco / Lavanda"),
            ["pesca"]     = new("#FFFFFFFF", "#FFFDF3EE", "Bianco / Pesca"),
            ["perla"]     = new("#FFFFFFFF", "#FFF4F4F6", "Bianco / Grigio perla"),
        };

    /// <summary>
    /// Applica una palette in base alla chiave e la persiste su disco.
    /// Può essere chiamato da qualsiasi thread UI.
    /// </summary>
    public static void ApplicaEPersisti(string chiave)
    {
        Applica(chiave);
        Persisti(chiave);
    }

    /// <summary>
    /// Applica una palette solo in memoria (senza salvarla).
    /// </summary>
    public static void Applica(string chiave)
    {
        if (!Palettes.TryGetValue(chiave, out var palette))
        {
            palette = Palettes[ChiavePredefinita];
        }

        var resources = Application.Current.Resources;
        resources["GrigliaBancoRigaBrush"]    = new SolidColorBrush(palette.ColoreRiga);
        resources["GrigliaBancoRigaAltBrush"] = new SolidColorBrush(palette.ColoreRigaAlt);
    }

    /// <summary>
    /// Carica la palette salvata e la applica all'avvio.
    /// Se non esiste una preferenza salvata usa la palette predefinita.
    /// </summary>
    public static void CaricaEApplica()
    {
        var chiave = CaricaChiave() ?? ChiavePredefinita;
        Applica(chiave);
    }

    /// <summary>
    /// Crea un MenuItem "Colore righe alternate" con tutte le palette disponibili.
    /// Richiamabile da qualsiasi menu contestuale o punto dell'applicazione.
    /// </summary>
    public static MenuItem CreaMenuColoriRighe()
    {
        var paletteCorrente = CaricaChiave() ?? ChiavePredefinita;
        var menu = new MenuItem { Header = "Colore righe alternate" };

        foreach (var (chiave, palette) in Palettes)
        {
            var anteprima = new Rectangle
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(palette.ColoreRigaAlt),
                Stroke = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                StrokeThickness = 1,
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var item = new MenuItem
            {
                Header = palette.Etichetta,
                Tag = chiave,
                Icon = anteprima,
                IsCheckable = false
            };

            // Segna la palette attiva
            if (string.Equals(chiave, paletteCorrente, StringComparison.OrdinalIgnoreCase))
            {
                item.FontWeight = FontWeights.Bold;
            }

            item.Click += (_, _) =>
            {
                if (item.Tag is string key)
                {
                    ApplicaEPersisti(key);
                }
            };

            menu.Items.Add(item);
        }

        return menu;
    }

    private static void Persisti(string chiave)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(new { Palette = chiave });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Scrittura fallita: non critico, si usa la palette in memoria
        }
    }

    private static string? CaricaChiave()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            using var stream = File.OpenRead(FilePath);
            var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("Palette", out var prop))
            {
                return prop.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Definisce i due colori di una palette di righe alternate.
/// </summary>
public sealed class PaletteGriglia
{
    public Color ColoreRiga { get; }
    public Color ColoreRigaAlt { get; }
    public string Etichetta { get; }

    public PaletteGriglia(string coloreRiga, string coloreRigaAlt, string etichetta)
    {
        ColoreRiga    = (Color)ColorConverter.ConvertFromString(coloreRiga);
        ColoreRigaAlt = (Color)ColorConverter.ConvertFromString(coloreRigaAlt);
        Etichetta     = etichetta;
    }
}
