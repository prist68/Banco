using System.Collections.ObjectModel;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;

namespace Banco.Magazzino.ViewModels;

public sealed class ArticleSecondaryCategoryItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public int Oid { get; init; }

    public string Label { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ArticleSecondaryCategoryManagementViewModel : ViewModelBase
{
    private readonly IGestionaleArticleReadService _readService;
    private readonly IGestionaleArticleWriteService _writeService;

    private int _articoloOid;
    private int? _primaryCategoryOid;
    private bool _isLoading;
    private bool _isSaving;
    private string _primaryCategoryLabel = string.Empty;
    private string _statusMessage = string.Empty;

    public ArticleSecondaryCategoryManagementViewModel(
        IGestionaleArticleReadService readService,
        IGestionaleArticleWriteService writeService)
    {
        _readService = readService;
        _writeService = writeService;

        Categories = [];
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsLoading && !IsSaving);
    }

    public ObservableCollection<ArticleSecondaryCategoryItemViewModel> Categories { get; }

    public RelayCommand SaveCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PrimaryCategoryLabel
    {
        get => _primaryCategoryLabel;
        private set => SetProperty(ref _primaryCategoryLabel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int SelectedCount => Categories.Count(item => item.IsSelected);

    public async Task LoadAsync(
        int articoloOid,
        int? primaryCategoryOid,
        string primaryCategoryLabel,
        CancellationToken cancellationToken = default)
    {
        _articoloOid = articoloOid;
        _primaryCategoryOid = primaryCategoryOid;
        PrimaryCategoryLabel = string.IsNullOrWhiteSpace(primaryCategoryLabel) ? "-" : primaryCategoryLabel;

        try
        {
            IsLoading = true;
            StatusMessage = "Lettura categorie secondarie legacy in corso...";

            var allCategories = await _readService.GetArticleSecondaryCategoryOptionsAsync(cancellationToken);
            var selectedCategoryOids = await _readService.GetArticleSecondaryCategoryOidsAsync(articoloOid, cancellationToken);
            var selectedSet = selectedCategoryOids.ToHashSet();

            Categories.Clear();
            foreach (var option in allCategories)
            {
                if (_primaryCategoryOid.HasValue && option.Oid == _primaryCategoryOid.Value)
                {
                    continue;
                }

                var item = new ArticleSecondaryCategoryItemViewModel
                {
                    Oid = option.Oid,
                    Label = option.Label,
                    IsSelected = selectedSet.Contains(option.Oid)
                };

                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ArticleSecondaryCategoryItemViewModel.IsSelected))
                    {
                        NotifyPropertyChanged(nameof(SelectedCount));
                    }
                };

                Categories.Add(item);
            }

            StatusMessage = SelectedCount == 0
                ? "Nessuna categoria secondaria agganciata."
                : $"{SelectedCount} categorie secondarie agganciate.";
            NotifyPropertyChanged(nameof(SelectedCount));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_articoloOid <= 0)
        {
            return;
        }

        try
        {
            IsSaving = true;
            StatusMessage = "Salvataggio categorie secondarie legacy in corso...";

            var selectedCategoryOids = Categories
                .Where(item => item.IsSelected)
                .Select(item => item.Oid)
                .Distinct()
                .OrderBy(item => item)
                .ToList();

            await _writeService.SaveArticleSecondaryCategoriesAsync(_articoloOid, selectedCategoryOids, cancellationToken);

            StatusMessage = selectedCategoryOids.Count == 0
                ? "Categorie secondarie rimosse."
                : $"{selectedCategoryOids.Count} categorie secondarie salvate.";
            NotifyPropertyChanged(nameof(SelectedCount));
        }
        finally
        {
            IsSaving = false;
        }
    }
}
