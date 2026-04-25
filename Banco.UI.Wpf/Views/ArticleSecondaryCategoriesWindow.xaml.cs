using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Banco.Magazzino.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.UI.Wpf.Views;

public partial class ArticleSecondaryCategoriesWindow : Window
{
    private readonly ArticleSecondaryCategoryManagementViewModel _viewModel;
    private bool _saved;

    public ArticleSecondaryCategoriesWindow(
        int articoloOid,
        int? primaryCategoryOid,
        string primaryCategoryLabel)
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<ArticleSecondaryCategoryManagementViewModel>();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            await _viewModel.LoadAsync(articoloOid, primaryCategoryOid, primaryCategoryLabel);
            SyncList();
        };
    }

    public bool Saved => _saved;

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = _saved;
        Close();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        _saved = true;
        SyncList();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ArticleSecondaryCategoryManagementViewModel.StatusMessage) ||
            e.PropertyName == nameof(ArticleSecondaryCategoryManagementViewModel.SelectedCount))
        {
            SyncList();
        }
    }

    private void SyncList()
    {
        CategoriesListBox.ItemsSource = _viewModel.Categories;
        EmptyTextBlock.Visibility = _viewModel.Categories.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
