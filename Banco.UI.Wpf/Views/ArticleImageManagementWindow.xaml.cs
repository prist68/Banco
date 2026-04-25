using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Banco.Magazzino.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace Banco.UI.Wpf.Views;

public partial class ArticleImageManagementWindow : Window
{
    private static readonly HashSet<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".webp"];

    private readonly ArticleImageManagementViewModel _viewModel;
    private string _tempFile = string.Empty;

    public ArticleImageManagementWindow(
        int articoloOid,
        string codiceArticolo,
        string descrizioneArticolo)
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<ArticleImageManagementViewModel>();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        ArticleSubtitleTextBlock.Text = $"{codiceArticolo} - {descrizioneArticolo}";

        Loaded += async (_, _) =>
        {
            await _viewModel.LoadAsync(articoloOid, codiceArticolo);
            SyncListBox();
            SyncActionButtons();
        };

        Closed += (_, _) => DeleteTempFile();
        KeyDown += Window_OnKeyDown;
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void SyncListBox()
    {
        ImagesListBox.ItemsSource = _viewModel.Images;
        ImagesListBox.SelectedItem = _viewModel.SelectedImage;
        foreach (ArticleImageItemViewModel item in ImagesListBox.Items)
        {
            item.IsSelected = ReferenceEquals(item, ImagesListBox.SelectedItem);
        }

        EmptyPlaceholder.Visibility = _viewModel.Images.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        StatusTextBlock.Text = _viewModel.StatusMessage;
    }

    private void ImagesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (ArticleImageItemViewModel item in ImagesListBox.Items)
        {
            item.IsSelected = ReferenceEquals(item, ImagesListBox.SelectedItem);
        }

        _viewModel.SelectedImage = ImagesListBox.SelectedItem as ArticleImageItemViewModel;
        SyncActionButtons();
    }

    private void ImagesListBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _viewModel.SelectedImage is not null)
        {
            e.Handled = true;
            _ = ExecuteDeleteAsync();
        }
    }

    private void ImageArea_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void ImageArea_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        foreach (var file in files)
        {
            if (AllowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
            {
                await _viewModel.AddImageFromPathAsync(file);
            }
        }

        SyncListBox();
        SyncActionButtons();
    }

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = PasteFromClipboardAsync();
        }
    }

    private void AddFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleziona immagine da aggiungere",
            Filter = "Immagini (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tutti i file|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _ = AddFilesAsync(dialog.FileNames);
    }

    private void PasteButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = PasteFromClipboardAsync();
    }

    private void SetPredefinitaButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ExecuteSetPredefinitaAsync();
    }

    private void MoveUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ExecuteMoveAsync(up: true);
    }

    private void MoveDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ExecuteMoveAsync(up: false);
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ExecuteDeleteAsync();
    }

    private void MoveVariantButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = ExecuteMoveVariantAsync();
    }

    private async Task AddFilesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            await _viewModel.AddImageFromPathAsync(path);
        }

        SyncListBox();
        SyncActionButtons();
    }

    private async Task PasteFromClipboardAsync()
    {
        if (!Clipboard.ContainsImage())
        {
            StatusTextBlock.Text = "Nessuna immagine negli appunti.";
            return;
        }

        var bitmapSource = Clipboard.GetImage();
        if (bitmapSource is null)
        {
            StatusTextBlock.Text = "Impossibile leggere l'immagine dagli appunti.";
            return;
        }

        DeleteTempFile();

        var tempPath = Path.Combine(Path.GetTempPath(), $"banco_paste_{Guid.NewGuid():N}.png");
        _tempFile = tempPath;

        using (var stream = File.OpenWrite(tempPath))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
        }

        await _viewModel.AddImageFromPathAsync(tempPath);
        SyncListBox();
        SyncActionButtons();
    }

    private async Task ExecuteSetPredefinitaAsync()
    {
        if (_viewModel.SelectedImage is null || _viewModel.SelectedImage.Predefinita)
        {
            return;
        }

        await _viewModel.SetPredefinitaInternalAsync();
        SyncActionButtons();
        StatusTextBlock.Text = _viewModel.StatusMessage;
    }

    private async Task ExecuteMoveAsync(bool up)
    {
        if (_viewModel.SelectedImage is null)
        {
            return;
        }

        await _viewModel.MoveImageAsync(up);

        var current = _viewModel.SelectedImage;
        ImagesListBox.SelectedItem = current;
        ImagesListBox.ScrollIntoView(current);

        SyncListBox();
        SyncActionButtons();
    }

    private async Task ExecuteDeleteAsync()
    {
        if (_viewModel.SelectedImage is null)
        {
            return;
        }

        await _viewModel.DeleteImageInternalAsync();
        SyncListBox();
        SyncActionButtons();
    }

    private async Task ExecuteMoveVariantAsync()
    {
        if (!_viewModel.CanMoveSelectedImageToTarget)
        {
            return;
        }

        await _viewModel.MoveSelectedImageToTargetAsync();
        SyncListBox();
        SyncActionButtons();
    }

    private void SyncActionButtons()
    {
        var sel = _viewModel.SelectedImage;
        var idx = sel is not null ? _viewModel.Images.IndexOf(sel) : -1;

        SetPredefinitaButton.IsEnabled = sel is not null && !sel.Predefinita;
        MoveUpButton.IsEnabled = idx > 0;
        MoveDownButton.IsEnabled = idx >= 0 && idx < _viewModel.Images.Count - 1;
        MoveVariantButton.IsEnabled = _viewModel.CanMoveSelectedImageToTarget;
        RemoveButton.IsEnabled = sel is not null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(ArticleImageManagementViewModel.StatusMessage):
                case nameof(ArticleImageManagementViewModel.SelectedVarianteScopeOption):
                case nameof(ArticleImageManagementViewModel.InsertTargetDescription):
                    SyncListBox();
                    SyncActionButtons();
                    break;
            }
        });
    }

    private void DeleteTempFile()
    {
        if (string.IsNullOrWhiteSpace(_tempFile))
        {
            return;
        }

        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch
        {
            // Ignorato
        }

        _tempFile = string.Empty;
    }
}
