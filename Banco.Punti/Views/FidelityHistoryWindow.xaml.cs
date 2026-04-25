using System.Windows;
using System.Windows.Controls;
using Banco.Punti.ViewModels;
using Banco.Vendita.Points;

namespace Banco.Punti.Views;

public partial class FidelityHistoryWindow : Window
{
    public FidelityHistoryWindow()
    {
        InitializeComponent();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (HistoryEntriesListView.SelectedItem is null && HistoryEntriesListView.Items.Count > 0)
        {
            HistoryEntriesListView.SelectedIndex = HistoryEntriesListView.Items.Count - 1;
        }

        HistoryEntriesListView.ScrollIntoView(HistoryEntriesListView.SelectedItem);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void HistoryEntriesListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PuntiViewModel viewModel &&
            sender is ListView listView &&
            listView.SelectedItem is FidelityHistoryEntry entry)
        {
            viewModel.SelectedFidelityHistoryEntry = entry;
        }
    }

    private void OpenDocumentInBancoButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PuntiViewModel viewModel &&
            sender is FrameworkElement element &&
            element.Tag is FidelityHistoryEntry entry)
        {
            viewModel.OpenSelectedFidelityDocumentInBanco(entry);
            Close();
        }
    }
}
