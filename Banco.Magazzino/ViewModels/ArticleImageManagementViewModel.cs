using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;

namespace Banco.Magazzino.ViewModels;

public sealed class ArticleImageItemViewModel : ViewModelBase
{
    private bool _predefinita;
    private bool _isSelected;

    public int Oid { get; init; }
    public int ArticoloOid { get; init; }
    public int? VariantedettaglioOid { get; init; }
    public string VariantedettaglioLabel { get; init; } = string.Empty;
    public int Posizione { get; set; }
    public string Descrizione { get; init; } = string.Empty;
    public string Fonteimmagine { get; init; } = string.Empty;
    public string? LocalPath { get; init; }

    public bool Predefinita
    {
        get => _predefinita;
        set => SetProperty(ref _predefinita, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool HasLocalFile =>
        !string.IsNullOrWhiteSpace(LocalPath) &&
        File.Exists(LocalPath);

    public bool IsRemoteSource => false;

    public string BadgeText =>
        string.IsNullOrWhiteSpace(VariantedettaglioLabel) ? "Tutte" : VariantedettaglioLabel;

    public string DisplayFileName =>
        !string.IsNullOrWhiteSpace(LocalPath)
            ? Path.GetFileName(LocalPath)
            : $"immagine {Oid}";

    public string DisplayDescription =>
        string.IsNullOrWhiteSpace(Descrizione)
            ? DisplayFileName
            : Descrizione;

    public string DisplayLocation =>
        !string.IsNullOrWhiteSpace(LocalPath)
            ? LocalPath
            : "File non trovato nella cartella immagini FM.";
}

public sealed class ArticleImageManagementViewModel : ViewModelBase
{
    private readonly IGestionaleArticleReadService _readService;
    private readonly IGestionaleArticleWriteService _writeService;
    private readonly List<ArticleImageRecord> _allImageRecords = [];
    private readonly List<GestionaleLookupOption> _allVarianteScopeOptions = [];

    private int _articoloOid;
    private string _codiceArticolo = string.Empty;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private ArticleImageItemViewModel? _selectedImage;
    private GestionaleLookupOption? _selectedVarianteScopeOption;
    private GestionaleLookupOption? _selectedMoveTargetOption;
    private bool _isVarianteScopePickerOpen;
    private bool _isMoveTargetPickerOpen;
    private string _varianteScopePickerSearchText = string.Empty;
    private string _moveTargetPickerSearchText = string.Empty;

    public ArticleImageManagementViewModel(
        IGestionaleArticleReadService readService,
        IGestionaleArticleWriteService writeService)
    {
        _readService = readService;
        _writeService = writeService;

        Images = [];
        VarianteScopeOptions = [];
        FilteredVarianteScopeOptions = [];

        SetPredefinitaCommand = new RelayCommand(
            async () => await ExecuteSetPredefinitaAsync(),
            () => SelectedImage is not null && !SelectedImage.Predefinita && !IsLoading);

        DeleteImageCommand = new RelayCommand(
            async () => await ExecuteDeleteAsync(),
            () => SelectedImage is not null && !IsLoading);

        MoveUpCommand = new RelayCommand(
            async () => await ExecuteMoveAsync(-1),
            () => SelectedImage is not null && Images.IndexOf(SelectedImage) > 0 && !IsLoading);

        MoveDownCommand = new RelayCommand(
            async () => await ExecuteMoveAsync(+1),
            () => SelectedImage is not null && Images.IndexOf(SelectedImage) < Images.Count - 1 && !IsLoading);

        MoveToVariantCommand = new RelayCommand(
            async () => await ExecuteMoveToVariantAsync(),
            () => CanMoveSelectedImageToTarget);
    }

    public ObservableCollection<ArticleImageItemViewModel> Images { get; }

    public ObservableCollection<GestionaleLookupOption> VarianteScopeOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredVarianteScopeOptions { get; }

    public ObservableCollection<GestionaleLookupOption> FilteredMoveTargetOptions { get; } = [];

    public ArticleImageItemViewModel? SelectedImage
    {
        get => _selectedImage;
        set
        {
            if (SetProperty(ref _selectedImage, value))
            {
                NotifyPropertyChanged(nameof(HasSelectedImage));
                NotifyPropertyChanged(nameof(CanSetPredefinita));
                CoerceSelectedMoveTargetOption();
                NotifyPropertyChanged(nameof(CanMoveSelectedImageToTarget));
                NotifyPropertyChanged(nameof(SelectedMoveTargetLabel));
            }
        }
    }

    public GestionaleLookupOption? SelectedVarianteScopeOption
    {
        get => _selectedVarianteScopeOption;
        set
        {
            if (!SetProperty(ref _selectedVarianteScopeOption, value))
            {
                return;
            }

            if (value is not null)
            {
                IsVarianteScopePickerOpen = false;
                if (!string.IsNullOrWhiteSpace(_varianteScopePickerSearchText))
                {
                    _varianteScopePickerSearchText = string.Empty;
                    NotifyPropertyChanged(nameof(VarianteScopePickerSearchText));
                    RefreshFilteredVarianteScopeOptions();
                }
            }

            ApplyImageFilter();
            NotifyPropertyChanged(nameof(SelectedVarianteScopeLabel));
            NotifyPropertyChanged(nameof(InsertTargetDescription));
        }
    }

    public bool IsVarianteScopePickerOpen
    {
        get => _isVarianteScopePickerOpen;
        set => SetProperty(ref _isVarianteScopePickerOpen, value);
    }

    public bool IsMoveTargetPickerOpen
    {
        get => _isMoveTargetPickerOpen;
        set => SetProperty(ref _isMoveTargetPickerOpen, value);
    }

    public string VarianteScopePickerSearchText
    {
        get => _varianteScopePickerSearchText;
        set
        {
            if (SetProperty(ref _varianteScopePickerSearchText, value))
            {
                RefreshFilteredVarianteScopeOptions();
            }
        }
    }

    public string MoveTargetPickerSearchText
    {
        get => _moveTargetPickerSearchText;
        set
        {
            if (SetProperty(ref _moveTargetPickerSearchText, value))
            {
                RefreshFilteredMoveTargetOptions();
            }
        }
    }

    public GestionaleLookupOption? SelectedMoveTargetOption
    {
        get => _selectedMoveTargetOption;
        set
        {
            if (!SetProperty(ref _selectedMoveTargetOption, value))
            {
                return;
            }

            if (value is not null)
            {
                IsMoveTargetPickerOpen = false;
                if (!string.IsNullOrWhiteSpace(_moveTargetPickerSearchText))
                {
                    _moveTargetPickerSearchText = string.Empty;
                    NotifyPropertyChanged(nameof(MoveTargetPickerSearchText));
                    RefreshFilteredMoveTargetOptions();
                }
            }

            NotifyPropertyChanged(nameof(CanMoveSelectedImageToTarget));
            NotifyPropertyChanged(nameof(SelectedMoveTargetLabel));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasSelectedImage => SelectedImage is not null;

    public bool CanSetPredefinita => SelectedImage is not null && !SelectedImage.Predefinita;

    public bool HasVarianteScopePicker => VarianteScopeOptions.Count > 1;

    public string SelectedVarianteScopeLabel => SelectedVarianteScopeOption?.Label ?? "Tutte";

    public string InsertTargetDescription =>
        SelectedVarianteScopeOption?.Oid > 0
            ? $"Nuove immagini su: {SelectedVarianteScopeOption.Label}"
            : "Nuove immagini su: Tutte";

    public string SelectedMoveTargetLabel => SelectedMoveTargetOption?.Label ?? "Tutte";

    public bool CanMoveSelectedImageToTarget =>
        SelectedImage is not null &&
        SelectedMoveTargetOption is not null &&
        NormalizeVariantOid(SelectedImage.VariantedettaglioOid) != NormalizeVariantOid(SelectedMoveTargetOption.Oid) &&
        !IsLoading;

    public ICommand SetPredefinitaCommand { get; }
    public ICommand DeleteImageCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand MoveToVariantCommand { get; }

    public async Task LoadAsync(int articoloOid, string codiceArticolo, CancellationToken cancellationToken = default)
    {
        _articoloOid = articoloOid;
        _codiceArticolo = codiceArticolo;
        IsLoading = true;
        StatusMessage = "Caricamento immagini in corso…";

        try
        {
            var varianteTask = _readService.GetArticleVariantedettaglioOptionsAsync(articoloOid, cancellationToken);
            var imagesTask = _readService.GetArticleImagesAsync(articoloOid, cancellationToken);

            await Task.WhenAll(varianteTask, imagesTask);

            RebuildVarianteScopeOptions(varianteTask.Result);
            ReplaceAllImageRecords(imagesTask.Result);
            ApplyImageFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante il caricamento: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddImageFromPathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusMessage = "File non trovato o percorso non valido.";
            return;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!IsAllowedImageExtension(ext))
        {
            StatusMessage = $"Formato non supportato: {ext}. Usare JPG, PNG, BMP o WEBP.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Aggiunta immagine in corso…";

        try
        {
            var selectedVariante = SelectedVarianteScopeOption?.Oid > 0
                ? SelectedVarianteScopeOption
                : null;
            var request = new ArticleImageAddRequest
            {
                ArticoloOid = _articoloOid,
                CodiceArticolo = _codiceArticolo,
                SourceFilePath = filePath,
                VariantedettaglioOid = selectedVariante?.Oid
            };

            var record = await _writeService.AddArticleImageAsync(request, cancellationToken);
            await ReloadImagesAsync(record.Oid, cancellationToken);
            StatusMessage = $"Immagine '{record.Fonteimmagine}' aggiunta.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante l'aggiunta: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task SetPredefinitaInternalAsync() => ExecuteSetPredefinitaAsync();

    public Task DeleteImageInternalAsync() => ExecuteDeleteAsync();

    public Task MoveImageAsync(bool up) => ExecuteMoveAsync(up ? -1 : +1);

    public Task MoveSelectedImageToTargetAsync() => ExecuteMoveToVariantAsync();

    private async Task ExecuteSetPredefinitaAsync()
    {
        if (SelectedImage is null)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Impostazione copertina in corso…";

        try
        {
            await _writeService.SetArticleImageAsPredefinitaAsync(
                SelectedImage.Oid, _articoloOid);
            await ReloadImagesAsync(SelectedImage.Oid);

            StatusMessage = $"'{SelectedImage.Fonteimmagine}' impostata come copertina.";
            NotifyPropertyChanged(nameof(CanSetPredefinita));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteDeleteAsync()
    {
        if (SelectedImage is null)
        {
            return;
        }

        var toDelete = SelectedImage;
        IsLoading = true;
        StatusMessage = "Rimozione in corso…";

        try
        {
            await _writeService.DeleteArticleImageAsync(toDelete.Oid);
            var fallbackSelectionOid = Images
                .Where(img => img.Oid != toDelete.Oid)
                .Select(img => (int?)img.Oid)
                .FirstOrDefault();
            await ReloadImagesAsync(fallbackSelectionOid);

            StatusMessage = $"Immagine '{toDelete.Fonteimmagine}' rimossa dal database.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante la rimozione: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteMoveAsync(int direction)
    {
        if (SelectedImage is null)
        {
            return;
        }

        var idx = Images.IndexOf(SelectedImage);
        var newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= Images.Count)
        {
            return;
        }

        var visibleRecords = GetVisibleImageRecords();
        var currentRecord = visibleRecords[idx];
        var targetRecord = visibleRecords[newIdx];

        var currentAllIndex = _allImageRecords.FindIndex(img => img.Oid == currentRecord.Oid);
        var targetAllIndex = _allImageRecords.FindIndex(img => img.Oid == targetRecord.Oid);
        if (currentAllIndex < 0 || targetAllIndex < 0)
        {
            return;
        }

        _allImageRecords.RemoveAt(currentAllIndex);
        if (currentAllIndex < targetAllIndex)
        {
            targetAllIndex--;
        }

        _allImageRecords.Insert(targetAllIndex, currentRecord);

        var updates = _allImageRecords
            .Select((img, i) => (img.Oid, Posizione: i + 1))
            .ToList();

        try
        {
            await _writeService.UpdateArticleImagePositionsAsync(updates);
            await ReloadImagesAsync(SelectedImage.Oid);
            StatusMessage = "Ordine immagini aggiornato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante il riordinamento: {ex.Message}";
        }
    }

    private async Task ExecuteMoveToVariantAsync()
    {
        if (SelectedImage is null || SelectedMoveTargetOption is null)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Spostamento immagine in corso…";

        var selectedImageOid = SelectedImage.Oid;
        var targetLabel = SelectedMoveTargetOption.Label;
        var targetOid = NormalizeVariantOid(SelectedMoveTargetOption.Oid);

        try
        {
            await _writeService.UpdateArticleImageVariantAsync(selectedImageOid, targetOid);
            await ReloadImagesAsync(selectedImageOid);
            StatusMessage = targetOid.HasValue
                ? $"Immagine spostata sulla variante '{targetLabel}'."
                : "Immagine spostata su 'Tutte'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante lo spostamento: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyPropertyChanged(nameof(CanMoveSelectedImageToTarget));
        }
    }

    private void ReplaceAllImageRecords(IReadOnlyList<ArticleImageRecord> records)
    {
        _allImageRecords.Clear();
        _allImageRecords.AddRange(records);
    }

    private static ArticleImageItemViewModel BuildItem(ArticleImageRecord rec) =>
        new()
        {
            Oid = rec.Oid,
            ArticoloOid = rec.ArticoloOid,
            VariantedettaglioOid = rec.VariantedettaglioOid,
            VariantedettaglioLabel = rec.VariantedettaglioLabel,
            Predefinita = rec.Predefinita,
            Posizione = rec.Posizione,
            Descrizione = rec.Descrizione,
            Fonteimmagine = rec.Fonteimmagine,
            LocalPath = rec.LocalPath
        };

    private async Task ReloadImagesAsync(int? selectedImageOid, CancellationToken cancellationToken = default)
    {
        var records = await _readService.GetArticleImagesAsync(_articoloOid, cancellationToken);
        ReplaceAllImageRecords(records);
        ApplyImageFilter(selectedImageOid);
    }

    private void RebuildVarianteScopeOptions(IReadOnlyList<GestionaleLookupOption> variantOptions)
    {
        _allVarianteScopeOptions.Clear();
        _allVarianteScopeOptions.Add(new GestionaleLookupOption { Oid = 0, Label = "Tutte" });
        _allVarianteScopeOptions.AddRange(variantOptions.OrderBy(static opt => opt.Label, StringComparer.OrdinalIgnoreCase));

        ReplaceCollection(VarianteScopeOptions, _allVarianteScopeOptions);
        RefreshFilteredVarianteScopeOptions();
        RefreshFilteredMoveTargetOptions();

        var currentOid = SelectedVarianteScopeOption?.Oid ?? 0;
        SelectedVarianteScopeOption = VarianteScopeOptions.FirstOrDefault(opt => opt.Oid == currentOid)
            ?? VarianteScopeOptions.FirstOrDefault();

        CoerceSelectedMoveTargetOption();

        NotifyPropertyChanged(nameof(HasVarianteScopePicker));
    }

    private void RefreshFilteredVarianteScopeOptions()
    {
        ReplaceCollection(
            FilteredVarianteScopeOptions,
            FilterLookupOptions(_allVarianteScopeOptions, _varianteScopePickerSearchText));
    }

    private void RefreshFilteredMoveTargetOptions()
    {
        ReplaceCollection(
            FilteredMoveTargetOptions,
            FilterLookupOptions(_allVarianteScopeOptions, _moveTargetPickerSearchText));
    }

    private void ApplyImageFilter(int? selectedImageOid = null)
    {
        var selectedVariantOid = SelectedVarianteScopeOption?.Oid > 0
            ? SelectedVarianteScopeOption.Oid
            : (int?)null;

        var filteredRecords = selectedVariantOid.HasValue
            ? _allImageRecords.Where(img => img.VariantedettaglioOid == selectedVariantOid.Value).ToList()
            : _allImageRecords.ToList();

        Images.Clear();
        foreach (var rec in filteredRecords)
        {
            Images.Add(BuildItem(rec));
        }

        if (selectedImageOid.HasValue)
        {
            SelectedImage = Images.FirstOrDefault(img => img.Oid == selectedImageOid.Value);
        }

        SelectedImage ??= Images.FirstOrDefault();
        CoerceSelectedMoveTargetOption();
        UpdateStatusMessage();
    }

    private List<ArticleImageRecord> GetVisibleImageRecords()
    {
        var selectedVariantOid = SelectedVarianteScopeOption?.Oid > 0
            ? SelectedVarianteScopeOption.Oid
            : (int?)null;

        return selectedVariantOid.HasValue
            ? _allImageRecords.Where(img => img.VariantedettaglioOid == selectedVariantOid.Value).ToList()
            : _allImageRecords.ToList();
    }

    private void UpdateStatusMessage()
    {
        if (Images.Count == 0)
        {
            StatusMessage = SelectedVarianteScopeOption?.Oid > 0
                ? $"Nessuna immagine collegata alla variante '{SelectedVarianteScopeOption.Label}'."
                : "Nessuna immagine collegata all'articolo.";
            return;
        }

        StatusMessage = SelectedVarianteScopeOption?.Oid > 0
            ? $"{Images.Count} immagine/i mostrate per la variante '{SelectedVarianteScopeOption.Label}'."
            : $"{Images.Count} immagine/i caricate.";
    }

    private static IReadOnlyList<GestionaleLookupOption> FilterLookupOptions(
        IEnumerable<GestionaleLookupOption> source,
        string searchText)
    {
        var normalizedSearch = (searchText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return source.ToList();
        }

        return source
            .Where(item => item.Label.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void ReplaceCollection<T>(
        ObservableCollection<T> target,
        IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void CoerceSelectedMoveTargetOption()
    {
        var desiredOid = NormalizeVariantOid(SelectedImage?.VariantedettaglioOid) ?? 0;
        SelectedMoveTargetOption = VarianteScopeOptions.FirstOrDefault(opt => NormalizeVariantOid(opt.Oid) == desiredOid)
            ?? VarianteScopeOptions.FirstOrDefault();
    }

    private static int? NormalizeVariantOid(int? value) =>
        value.HasValue && value.Value > 0 ? value.Value : null;

    private static bool IsAllowedImageExtension(string ext) =>
        ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp";
}
