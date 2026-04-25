using Banco.Magazzino.ViewModels;
using Banco.UI.Wpf.ViewModels;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Banco.UI.Wpf.Views;

public partial class ArticleManagementView
{
    private bool _isInitialized;
    private MagazzinoArticleView? _embeddedMagazzinoView;
    private MagazzinoArticleViewModel? _embeddedMagazzinoViewModel;
    private ArticleManagementViewModel? _subscribedViewModel;
    private readonly DispatcherTimer _manualArticleLookupTimer;
    private readonly StringBuilder _articleSearchInputBuffer = new();
    private DateTime _articleSearchFirstInputAtUtc;
    private DateTime _articleSearchLastInputAtUtc;
    private bool _isArticleLookupDialogOpen;
    private bool _suppressManualArticleLookupScheduling;

    public ArticleManagementView()
    {
        InitializeComponent();
        _manualArticleLookupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _manualArticleLookupTimer.Tick += ManualArticleLookupTimer_OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ArticleManagementViewModel viewModel)
        {
            return;
        }

        if (!ReferenceEquals(_subscribedViewModel, viewModel))
        {
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.PropertyChanged -= OnArticleManagementViewModelPropertyChanged;
            }

            _subscribedViewModel = viewModel;
            _subscribedViewModel.PropertyChanged += OnArticleManagementViewModelPropertyChanged;
        }

        EnsureEmbeddedMagazzinoHost();

        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await viewModel.InitializeAsync();
        PushCurrentArticleIntoEmbeddedMagazzino(viewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        ResetArticleLookupInputTracking();
    }

    private void ManageImagesButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ArticleManagementViewModel viewModel || viewModel.SelectedSearchResult is null)
        {
            return;
        }

        var win = new ArticleImageManagementWindow(
            viewModel.SelectedSearchResult.Oid,
            viewModel.ArticleCode,
            viewModel.ArticleDescription)
        {
            Owner = Window.GetWindow(this)
        };

        win.ShowDialog();

        if (viewModel.RefreshCurrentArticleCommand.CanExecute(null))
        {
            viewModel.RefreshCurrentArticleCommand.Execute(null);
        }
    }

    private async void ManualArticleLookupTimer_OnTick(object? sender, EventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        if (_suppressManualArticleLookupScheduling ||
            _isArticleLookupDialogOpen ||
            DataContext is not ArticleManagementViewModel viewModel)
        {
            return;
        }

        var searchText = viewModel.ArticleCodeInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText) || !ArticleLookupLauncher.IsSearchKeyboardFocusWithin)
        {
            return;
        }

        if (LooksLikeDirectArticleCodeOrBarcode(searchText))
        {
            return;
        }

        await OpenArticleLookupAsync(searchText);
    }

    private async void ArticleLookupLauncher_OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ArticleManagementViewModel)
        {
            return;
        }

        if (e.Key == Key.F1)
        {
            _manualArticleLookupTimer.Stop();
            ResetArticleLookupInputTracking();
            e.Handled = true;
            await OpenArticleLookupAsync();
            return;
        }

        if (e.Key == Key.Escape)
        {
            _manualArticleLookupTimer.Stop();
            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        _manualArticleLookupTimer.Stop();
        var isScannerSubmission = IsProbableArticleScannerSubmission();
        ResetArticleLookupInputTracking();
        e.Handled = true;

        if (isScannerSubmission)
        {
            await TryLoadArticleFromCodeOrBarcodeAsync();
            return;
        }

        await OpenArticleLookupAsync();
    }

    private void ArticleLookupLauncher_OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressManualArticleLookupScheduling ||
            _isArticleLookupDialogOpen ||
            DataContext is not ArticleManagementViewModel viewModel)
        {
            return;
        }

        var searchText = viewModel.ArticleCodeInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText) || !ArticleLookupLauncher.IsSearchKeyboardFocusWithin)
        {
            _manualArticleLookupTimer.Stop();
            return;
        }

        if (LooksLikeDirectArticleCodeOrBarcode(searchText))
        {
            _manualArticleLookupTimer.Stop();
            return;
        }

        _manualArticleLookupTimer.Stop();
        _manualArticleLookupTimer.Start();
    }

    private void ArticleLookupLauncher_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text) || Keyboard.Modifiers != ModifierKeys.None)
        {
            ResetArticleLookupInputTracking();
            return;
        }

        var now = DateTime.UtcNow;
        if (_articleSearchInputBuffer.Length == 0)
        {
            _articleSearchFirstInputAtUtc = now;
        }

        _articleSearchLastInputAtUtc = now;
        _articleSearchInputBuffer.Append(e.Text);
    }

    private void ArticleLookupLauncher_OnSearchLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _manualArticleLookupTimer.Stop();
        ResetArticleLookupInputTracking();
    }

    private async Task TryLoadArticleFromCodeOrBarcodeAsync()
    {
        if (DataContext is not ArticleManagementViewModel viewModel)
        {
            return;
        }

        var codeOrBarcode = viewModel.ArticleCodeInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(codeOrBarcode))
        {
            return;
        }

        try
        {
            _suppressManualArticleLookupScheduling = true;
            await viewModel.TryLoadArticleMasterByCodeOrBarcodeAsync(codeOrBarcode);
        }
        finally
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                _suppressManualArticleLookupScheduling = false;
                ArticleLookupLauncher.FocusSearchBox();
                ArticleLookupLauncher.SelectAllSearchText();
            }, DispatcherPriority.Input);
        }
    }

    private async Task OpenArticleLookupAsync(string? initialSearchText = null)
    {
        if (DataContext is not ArticleManagementViewModel viewModel)
        {
            return;
        }

        var searchText = (initialSearchText ?? viewModel.ArticleCodeInput)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var lookupViewModel = ActivatorUtilities.CreateInstance<ArticleLookupViewModel>(
            App.Services,
            new ArticleLookupRequest
            {
                SearchText = searchText,
                Title = "Ricerca articolo Gestione",
                Subtitle = "Digita per cercare nel catalogo legacy. In Gestione Articolo il barcode variante apre comunque la scheda padre."
            });

        var window = new ArticleLookupWindow(lookupViewModel)
        {
            Owner = Window.GetWindow(this)
        };

        _isArticleLookupDialogOpen = true;
        _manualArticleLookupTimer.Stop();

        try
        {
            var dialogResult = window.ShowDialog();
            if (dialogResult != true || window.SelectedArticle is null)
            {
                return;
            }

            var masterArticle = await viewModel.ResolveLookupSelectionToManagementMasterAsync(window.SelectedArticle);
            if (masterArticle is null)
            {
                MessageBox.Show(
                    "Non e` stato possibile risalire alla scheda padre dell'articolo selezionato.",
                    "Lookup articolo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _suppressManualArticleLookupScheduling = true;
            await viewModel.LoadArticleForManagementAsync(masterArticle);
        }
        finally
        {
            _isArticleLookupDialogOpen = false;
            _ = Dispatcher.BeginInvoke(() =>
            {
                _suppressManualArticleLookupScheduling = false;
                ArticleLookupLauncher.FocusSearchBox();
                ArticleLookupLauncher.SelectAllSearchText();
            }, DispatcherPriority.Input);
        }
    }

    private void ResetArticleLookupInputTracking()
    {
        _articleSearchInputBuffer.Clear();
        _articleSearchFirstInputAtUtc = default;
        _articleSearchLastInputAtUtc = default;
    }

    private bool IsProbableArticleScannerSubmission()
    {
        if (_articleSearchInputBuffer.Length < 8 || _articleSearchInputBuffer.Length > 18)
        {
            return false;
        }

        if (_articleSearchFirstInputAtUtc == default || _articleSearchLastInputAtUtc == default)
        {
            return false;
        }

        var elapsed = _articleSearchLastInputAtUtc - _articleSearchFirstInputAtUtc;
        return elapsed.TotalMilliseconds <= 180;
    }

    private static bool LooksLikeDirectArticleCodeOrBarcode(string value)
    {
        var normalized = value.Trim();
        return normalized.Length >= 6 &&
               normalized.All(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or '/');
    }

    private void ManageSecondaryCategoriesButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ArticleManagementViewModel viewModel)
        {
            MessageBox.Show(
                "La scheda articolo non e` ancora disponibile.",
                "Categorie secondarie",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var articoloOid = viewModel.CurrentArticleOid ?? viewModel.SelectedSearchResult?.Oid;
        if (!articoloOid.HasValue || articoloOid.Value <= 0)
        {
            MessageBox.Show(
                "Carica prima una scheda articolo valida, poi riapri le categorie secondarie.",
                "Categorie secondarie",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var window = new ArticleSecondaryCategoriesWindow(
                articoloOid.Value,
                viewModel.SelectedCategoryOid,
                viewModel.SelectedCategoryLabel)
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();

            if (window.Saved && viewModel.RefreshCurrentArticleCommand.CanExecute(null))
            {
                viewModel.RefreshCurrentArticleCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Apertura categorie secondarie non riuscita:{Environment.NewLine}{ex.Message}",
                "Categorie secondarie",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void EnsureEmbeddedMagazzinoHost()
    {
        if (_embeddedMagazzinoView is not null)
        {
            return;
        }

        _embeddedMagazzinoViewModel = App.Services.GetRequiredService<MagazzinoArticleViewModel>();
        _embeddedMagazzinoView = new MagazzinoArticleView
        {
            HideSearchPane = true,
            DataContext = _embeddedMagazzinoViewModel
        };

        EmbeddedMagazzinoArticleHost.Content = _embeddedMagazzinoView;
    }

    private void OnArticleManagementViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ArticleManagementViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(ArticleManagementViewModel.ArticleCode) ||
            e.PropertyName == nameof(ArticleManagementViewModel.SelectedSearchResult))
        {
            PushCurrentArticleIntoEmbeddedMagazzino(viewModel);
        }
    }

    private void PushCurrentArticleIntoEmbeddedMagazzino(ArticleManagementViewModel viewModel)
    {
        if (_embeddedMagazzinoViewModel is null || string.IsNullOrWhiteSpace(viewModel.ArticleCode) || viewModel.ArticleCode == "-")
        {
            return;
        }

        _embeddedMagazzinoViewModel.SearchText = viewModel.ArticleCode;
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void ArticleLookupLauncher_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
