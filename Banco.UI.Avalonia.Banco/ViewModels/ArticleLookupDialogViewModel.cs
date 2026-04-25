using System.Collections.ObjectModel;
using Banco.UI.Avalonia.Banco.Services;
using Banco.Vendita.Articles;

namespace Banco.UI.Avalonia.Banco.ViewModels;

public sealed class ArticleLookupDialogViewModel : ViewModelBase
{
    private readonly BancoSaleDataFacade _dataFacade;
    private readonly int? _selectedPriceListOid;
    private string _searchText;
    private string _statusMessage = "Digita e premi Invio per cercare.";
    private string _detailStatus = "Seleziona un articolo.";
    private GestionaleArticleSearchResult? _selectedArticle;
    private GestionaleArticlePricingDetail? _selectedPricingDetail;
    private bool _isShowingVariants;
    private GestionaleArticleSearchResult? _variantParent;

    public ArticleLookupDialogViewModel(
        BancoSaleDataFacade dataFacade,
        string searchText,
        int? selectedPriceListOid = null)
    {
        _dataFacade = dataFacade;
        _selectedPriceListOid = selectedPriceListOid;
        _searchText = searchText;
        Results = [];
    }

    public ObservableCollection<GestionaleArticleSearchResult> Results { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DetailStatus
    {
        get => _detailStatus;
        set => SetProperty(ref _detailStatus, value);
    }

    public GestionaleArticleSearchResult? SelectedArticle
    {
        get => _selectedArticle;
        set
        {
            if (SetProperty(ref _selectedArticle, value))
            {
                _ = LoadSelectedPricingDetailAsync(value);
                OnPropertyChanged(nameof(CanConfirmSelectedArticle));
                OnPropertyChanged(nameof(VariantHint));
            }
        }
    }

    public GestionaleArticlePricingDetail? SelectedPricingDetail
    {
        get => _selectedPricingDetail;
        set
        {
            if (SetProperty(ref _selectedPricingDetail, value))
            {
                OnPropertyChanged(nameof(SelectedUnitLabel));
                OnPropertyChanged(nameof(SelectedMinimumQuantityLabel));
                OnPropertyChanged(nameof(SelectedMultipleQuantityLabel));
                OnPropertyChanged(nameof(SelectedTierCountLabel));
            }
        }
    }

    public bool CanConfirmSelectedArticle => SelectedArticle is not null &&
                                            (SelectedArticle.IsVariante || !SelectedArticle.HasVariantChildren);

    public string VariantHint => SelectedArticle?.HasVariantChildren == true && !SelectedArticle.IsVariante
        ? "Seleziona una variante prima di confermare."
        : string.Empty;

    public string SelectedUnitLabel => SelectedPricingDetail?.UnitaMisuraPrincipale ?? "PZ";

    public string SelectedMinimumQuantityLabel => SelectedPricingDetail is null
        ? "1"
        : $"{Math.Max(1, SelectedPricingDetail.QuantitaMinimaVendita):N2}";

    public string SelectedMultipleQuantityLabel => SelectedPricingDetail is null
        ? "1"
        : $"{Math.Max(1, SelectedPricingDetail.QuantitaMultiplaVendita):N2}";

    public string SelectedTierCountLabel => SelectedPricingDetail is null
        ? "0"
        : SelectedPricingDetail.FascePrezzoQuantita.Count.ToString();

    public async Task SearchAsync()
    {
        _isShowingVariants = false;
        _variantParent = null;
        var result = await _dataFacade.SearchArticlesAsync(SearchText, _selectedPriceListOid);
        Results.Clear();
        foreach (var article in result.Articles)
        {
            Results.Add(article);
        }

        SelectedArticle = null;
        StatusMessage = result.Message;
    }

    public async Task<bool> EnsureConfirmableSelectionAsync()
    {
        if (SelectedArticle is null)
        {
            return false;
        }

        if (CanConfirmSelectedArticle)
        {
            return true;
        }

        await ShowVariantsAsync(SelectedArticle);
        return false;
    }

    private async Task ShowVariantsAsync(GestionaleArticleSearchResult parent)
    {
        var result = await _dataFacade.GetArticleVariantsAsync(parent, _selectedPriceListOid);
        Results.Clear();
        foreach (var variant in result.Articles)
        {
            Results.Add(variant);
        }

        _isShowingVariants = true;
        _variantParent = parent;
        SelectedArticle = null;
        StatusMessage = result.Articles.Count == 0
            ? $"Nessuna variante trovata per {parent.CodiceArticolo}."
            : $"Varianti di {parent.CodiceArticolo}: seleziona la riga corretta.";
    }

    private async Task LoadSelectedPricingDetailAsync(GestionaleArticleSearchResult? article)
    {
        if (article is null)
        {
            SelectedPricingDetail = null;
            DetailStatus = _isShowingVariants && _variantParent is not null
                ? $"Varianti di {_variantParent.CodiceArticolo}"
                : "Seleziona un articolo.";
            return;
        }

        var result = await _dataFacade.GetArticlePricingDetailAsync(article, _selectedPriceListOid);
        SelectedPricingDetail = result.Detail;
        DetailStatus = article.HasVariantChildren && !article.IsVariante
            ? "Articolo con varianti: scegli una variante."
            : result.Message;
    }
}
