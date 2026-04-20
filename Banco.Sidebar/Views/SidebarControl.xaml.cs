using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Banco.Sidebar.ViewModels;

namespace Banco.Sidebar.Views;

public partial class SidebarControl : UserControl
{
    // Percorso file persistenza larghezza rail
    private static readonly string LayoutFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Banco",
        "sidebar_layout.json");

    private const double DefaultRailWidth = 116;
    private const double MinRailWidth = 90;
    private const double MaxRailWidth = 200;

    public SidebarControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var larghezza = CaricaLarghezzaRail();
        RailColumn.Width = new GridLength(larghezza);
    }

    private void SearchResultButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel)
            return;

        if (sender is Button { CommandParameter: SidebarSearchResultViewModel result })
            viewModel.OpenSearchResult(result);
    }

    // Salva la larghezza del rail al termine del trascinamento dello splitter
    private void RailSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        SalvaLarghezzaRail(RailColumn.ActualWidth);
    }

    private static double CaricaLarghezzaRail()
    {
        try
        {
            if (!File.Exists(LayoutFilePath))
                return DefaultRailWidth;

            var json = File.ReadAllText(LayoutFilePath);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("railWidth", out var prop))
            {
                var valore = prop.GetDouble();
                return Math.Clamp(valore, MinRailWidth, MaxRailWidth);
            }
        }
        catch
        {
            // in caso di file corrotto usa il default
        }

        return DefaultRailWidth;
    }

    private static void SalvaLarghezzaRail(double larghezza)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutFilePath)!);
            var payload = new { railWidth = Math.Round(larghezza, 1) };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LayoutFilePath, json);
        }
        catch
        {
            // errore di scrittura non bloccante
        }
    }
}
