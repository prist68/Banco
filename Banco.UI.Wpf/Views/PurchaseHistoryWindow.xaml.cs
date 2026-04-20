using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Articles;
using Banco.Vendita.Customers;

namespace Banco.UI.Wpf.Views;

public partial class PurchaseHistoryWindow : Window
{
    public PurchaseHistoryWindow(PurchaseHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.ForceRefreshAsync();
    }

    private void ArticleLookupGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchaseHistoryViewModel viewModel
            && ArticleLookupGrid.SelectedItem is GestionaleArticleSearchResult articolo)
        {
            viewModel.SelectArticleFromLookup(articolo);
            ArticleFilterTextBox.Focus();
            ArticleFilterTextBox.SelectAll();
        }
    }

    private void ArticleLookupGrid_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && DataContext is PurchaseHistoryViewModel viewModel
            && ArticleLookupGrid.SelectedItem is GestionaleArticleSearchResult articolo)
        {
            e.Handled = true;
            viewModel.SelectArticleFromLookup(articolo);
            ArticleFilterTextBox.Focus();
            ArticleFilterTextBox.SelectAll();
        }
    }

    private void ArticleFilterTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PurchaseHistoryViewModel viewModel
            || !viewModel.IsArticlePopupOpen
            || ArticleLookupGrid.Items.Count == 0)
        {
            return;
        }

        if (e.Key != Key.Down && e.Key != Key.Up)
        {
            return;
        }

        e.Handled = true;

        var targetIndex = e.Key == Key.Down
            ? 0
            : ArticleLookupGrid.Items.Count - 1;

        ArticleLookupGrid.SelectedIndex = targetIndex;
        ArticleLookupGrid.UpdateLayout();
        ArticleLookupGrid.ScrollIntoView(ArticleLookupGrid.SelectedItem);
        ArticleLookupGrid.Focus();

        if (ArticleLookupGrid.ItemContainerGenerator.ContainerFromIndex(targetIndex) is DataGridRow row)
        {
            row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    private void SupplierLookupList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PurchaseHistoryViewModel viewModel
            && SupplierLookupList.SelectedItem is GestionaleCustomerSummary fornitore)
        {
            viewModel.SelectSupplierFromLookup(fornitore);
            SupplierFilterTextBox.Focus();
            SupplierFilterTextBox.SelectAll();
        }
    }

    private void SupplierLookupList_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter
            && DataContext is PurchaseHistoryViewModel viewModel
            && SupplierLookupList.SelectedItem is GestionaleCustomerSummary fornitore)
        {
            e.Handled = true;
            viewModel.SelectSupplierFromLookup(fornitore);
            SupplierFilterTextBox.Focus();
            SupplierFilterTextBox.SelectAll();
        }
    }
}
