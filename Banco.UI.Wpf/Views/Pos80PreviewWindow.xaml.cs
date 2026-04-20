using System.IO;
using System.Windows;

namespace Banco.UI.Wpf.Views;

public partial class Pos80PreviewWindow : Window
{
    public Pos80PreviewWindow(string previewPath)
    {
        InitializeComponent();
        PreviewPath = previewPath;
        DataContext = this;
        Loaded += OnLoaded;
    }

    public string PreviewPath { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigateToPreview();
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateToPreview();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NavigateToPreview()
    {
        if (string.IsNullOrWhiteSpace(PreviewPath) || !File.Exists(PreviewPath))
        {
            MessageBox.Show(
                "Il file di anteprima POS80 non esiste o non e` piu` disponibile.",
                "Anteprima non disponibile",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PreviewBrowser.Navigate(new Uri(PreviewPath));
    }
}
