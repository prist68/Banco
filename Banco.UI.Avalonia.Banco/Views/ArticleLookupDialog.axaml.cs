using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Banco.UI.Avalonia.Banco.ViewModels;
using Banco.Vendita.Articles;

namespace Banco.UI.Avalonia.Banco.Views;

public sealed partial class ArticleLookupDialog : Window
{
    private readonly ArticleLookupDialogViewModel _viewModel;
    private GestionaleArticleSearchResult? _confirmedArticle;

    public ArticleLookupDialog()
    {
        InitializeComponent();
        _viewModel = null!;
    }

    public ArticleLookupDialog(ArticleLookupDialogViewModel viewModel)
        : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    public GestionaleArticleSearchResult? SelectedArticle => _confirmedArticle ?? _viewModel.SelectedArticle;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SearchTextBox.Focus();
        SearchTextBox.CaretIndex = SearchTextBox.Text?.Length ?? 0;
        _ = _viewModel.SearchAsync();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await ConfirmCurrentSelectionAsync();
    }

    private async void ResultsListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        await ConfirmCurrentSelectionAsync();
    }

    private void ResultItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: GestionaleArticleSearchResult article })
        {
            ResultsListBox.SelectedItem = article;
            _viewModel.SelectedArticle = article;
        }
    }

    private void ResultsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ResultsListBox.SelectedItem is GestionaleArticleSearchResult article)
        {
            _viewModel.SelectedArticle = article;
        }
    }

    private async void ResultsListBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await ConfirmCurrentSelectionAsync();
        }
    }

    private async void SearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && ResultsListBox.ItemCount > 0)
        {
            e.Handled = true;
            ResultsListBox.SelectedIndex = Math.Max(0, ResultsListBox.SelectedIndex);
            ResultsListBox.Focus();
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await _viewModel.SearchAsync();
        }
    }

    private async Task ConfirmCurrentSelectionAsync()
    {
        if (ResultsListBox.SelectedItem is GestionaleArticleSearchResult article)
        {
            _viewModel.SelectedArticle = article;
        }
        else if (_viewModel.SelectedArticle is null && ResultsListBox.ItemCount > 0)
        {
            ResultsListBox.SelectedIndex = 0;
        }

        if (_viewModel.SelectedArticle is null)
        {
            return;
        }

        if (!await _viewModel.EnsureConfirmableSelectionAsync())
        {
            return;
        }

        _confirmedArticle = _viewModel.SelectedArticle;
        Close(true);
    }
}
