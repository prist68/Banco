using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Articles;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ArticleLookupViewModel : ViewModelBase
{
    private readonly IGestionaleArticleReadService _articleReadService;
    private readonly int? _selectedPriceListOid;
    private readonly bool _preferVariantResults;
    private readonly List<GestionaleArticleSearchResult> _allResults = [];
    private CancellationTokenSource? _searchCts;
    private bool _isLoading;
    private string _searchText = string.Empty;
    private string _additionalFilterText = string.Empty;
    private string _statusMessage = "Digita una ricerca multi-termine per filtrare il catalogo legacy.";
    private GestionaleArticleSearchResult? _selectedArticle;
    private GestionaleArticleLookupDetail? _selectedDetail;
    private bool _isDescriptionExpanded;
    private bool _isShortDescriptionExpanded;

    public ArticleLookupViewModel(
        ArticleLookupRequest request,
        IGestionaleArticleReadService articleReadService)
    {
        ArgumentNullException.ThrowIfNull(request);

        _articleReadService = articleReadService;
        _selectedPriceListOid = request.SelectedPriceListOid;
        _preferVariantResults = request.PreferVariantResults;
        WindowTitle = string.IsNullOrWhiteSpace(request.Title) ? "Ricerca articoli" : request.Title;
        WindowSubtitle = string.IsNullOrWhiteSpace(request.Subtitle)
            ? "Cerca dal catalogo legacy e seleziona l'articolo corretto."
            : request.Subtitle;

        Results = [];
        ToggleDescriptionCommand = new RelayCommand(ToggleDescription, () => HasLongDescription);
        ToggleShortDescriptionCommand = new RelayCommand(ToggleShortDescription, () => HasShortDescriptionExpandToggle);
        SearchText = request.SearchText?.Trim() ?? string.Empty;
    }

    public ObservableCollection<GestionaleArticleSearchResult> Results { get; }

    public RelayCommand ToggleDescriptionCommand { get; }

    public RelayCommand ToggleShortDescriptionCommand { get; }

    public string WindowTitle { get; }

    public string WindowSubtitle { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ScheduleSearch();
            }
        }
    }

    public string AdditionalFilterText
    {
        get => _additionalFilterText;
        set
        {
            if (SetProperty(ref _additionalFilterText, value))
            {
                ApplyAdditionalFilter();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public GestionaleArticleSearchResult? SelectedArticle
    {
        get => _selectedArticle;
        set
        {
            if (SetProperty(ref _selectedArticle, value))
            {
                _ = LoadSelectedDetailAsync(value);
            }
        }
    }

    public GestionaleArticleLookupDetail? SelectedDetail
    {
        get => _selectedDetail;
        private set
        {
            if (SetProperty(ref _selectedDetail, value))
            {
                NotifyPropertyChanged(nameof(HasSelection));
                NotifyPropertyChanged(nameof(HasImage));
                NotifyPropertyChanged(nameof(HasTags));
                NotifyPropertyChanged(nameof(HasSpecifications));
                NotifyPropertyChanged(nameof(HasPriceTiers));
                NotifyPropertyChanged(nameof(HasBrand));
                NotifyPropertyChanged(nameof(HasExcise));
                NotifyPropertyChanged(nameof(HasLastSaleDate));
                NotifyPropertyChanged(nameof(HasLongDescription));
                NotifyPropertyChanged(nameof(HasShortDescription));
                NotifyPropertyChanged(nameof(HasShortDescriptionExpandToggle));
                NotifyPropertyChanged(nameof(CategoryPath));
                NotifyPropertyChanged(nameof(LastSaleDateLabel));
                NotifyPropertyChanged(nameof(ShortDescriptionText));
                NotifyPropertyChanged(nameof(ShortDescriptionHtmlDocument));
                NotifyPropertyChanged(nameof(LongDescriptionHtmlDocument));
                NotifyPropertyChanged(nameof(DescriptionViewportHeight));
                NotifyPropertyChanged(nameof(ShortDescriptionViewportHeight));
                NotifyPropertyChanged(nameof(ShortDescriptionToggleButtonText));
                ToggleDescriptionCommand.RaiseCanExecuteChanged();
                ToggleShortDescriptionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedArticle is not null && SelectedDetail is not null;

    public bool CanConfirmSelectedArticle =>
        SelectedArticle is not null &&
        (!_preferVariantResults || SelectedArticle.IsVariante || !SelectedArticle.HasVariantChildren);

    public bool HasImage => !string.IsNullOrWhiteSpace(SelectedDetail?.ImageUrl);

    public bool HasTags => SelectedDetail?.Tags.Count > 0;

    public bool HasSpecifications => SelectedDetail?.Specifications.Count > 0;

    public bool HasPriceTiers => SelectedDetail?.FascePrezzoQuantita.Count > 0;

    public bool HasBrand => !string.IsNullOrWhiteSpace(SelectedDetail?.Brand);

    public bool HasExcise => !string.IsNullOrWhiteSpace(SelectedDetail?.ExciseLabel);

    public bool HasLastSaleDate => SelectedDetail?.LastSaleDate is not null;

    public bool HasLongDescription => !string.IsNullOrWhiteSpace(SelectedDetail?.DescrizioneLungaHtml);

    public bool HasShortDescription => !string.IsNullOrWhiteSpace(SelectedDetail?.DescrizioneBreveHtml);

    public bool HasShortDescriptionExpandToggle => ShortDescriptionText.Length > 110 || ShortDescriptionText.Contains(Environment.NewLine, StringComparison.Ordinal);

    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        private set
        {
            if (SetProperty(ref _isDescriptionExpanded, value))
            {
                NotifyPropertyChanged(nameof(DescriptionViewportHeight));
                NotifyPropertyChanged(nameof(ToggleDescriptionButtonText));
            }
        }
    }

    public bool IsShortDescriptionExpanded
    {
        get => _isShortDescriptionExpanded;
        private set
        {
            if (SetProperty(ref _isShortDescriptionExpanded, value))
            {
                NotifyPropertyChanged(nameof(ShortDescriptionViewportHeight));
                NotifyPropertyChanged(nameof(ShortDescriptionToggleButtonText));
            }
        }
    }

    public double DescriptionViewportHeight => IsDescriptionExpanded ? 360 : 170;

    public string ToggleDescriptionButtonText => IsDescriptionExpanded ? "Comprimi descrizione" : "Espandi descrizione";

    public double ShortDescriptionViewportHeight => IsShortDescriptionExpanded ? 88 : 22;

    public string ShortDescriptionToggleButtonText => IsShortDescriptionExpanded ? "Comprimi testo" : "Espandi testo";

    public string LastSaleDateLabel => SelectedDetail?.LastSaleDate?.ToString("dd/MM/yyyy") ?? string.Empty;

    public string CategoryPath
    {
        get
        {
            if (SelectedDetail is null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(SelectedDetail.SottoCategoria))
            {
                return SelectedDetail.Categoria;
            }

            return $"{SelectedDetail.SottoCategoria} / {SelectedDetail.Categoria}";
        }
    }

    public string ShortDescriptionText => HtmlToPlainText(SelectedDetail?.DescrizioneBreveHtml);

    public string ShortDescriptionHtmlDocument => BuildHtmlDocument(
        SelectedDetail?.DescrizioneBreveHtml,
        "<p>Nessuna descrizione breve presente nel legacy.</p>");

    public string LongDescriptionHtmlDocument => BuildHtmlDocument(
        SelectedDetail?.DescrizioneLungaHtml,
        "<p>Nessuna descrizione lunga presente nel legacy.</p>");

    private void ToggleDescription()
    {
        if (!HasLongDescription)
        {
            return;
        }

        IsDescriptionExpanded = !IsDescriptionExpanded;
    }

    private void ToggleShortDescription()
    {
        if (!HasShortDescriptionExpandToggle)
        {
            return;
        }

        IsShortDescriptionExpanded = !IsShortDescriptionExpanded;
    }

    private void ScheduleSearch()
    {
        _searchCts?.Cancel();

        var searchText = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _allResults.Clear();
            Results.Clear();
            SelectedArticle = null;
            SelectedDetail = null;
            StatusMessage = "Digita una ricerca multi-termine per filtrare il catalogo legacy.";
            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = SearchCoreAsync(searchText, cts.Token);
    }

    private async Task SearchCoreAsync(string searchText, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(220, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            IsLoading = true;
            StatusMessage = "Ricerca articoli legacy in corso...";
            var results = await _articleReadService.SearchArticlesAsync(searchText, _selectedPriceListOid, 60, cancellationToken);
            if (_preferVariantResults)
            {
                results = await ExpandVariantResultsAsync(results, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || !string.Equals(SearchText.Trim(), searchText, StringComparison.Ordinal))
            {
                return;
            }

            _allResults.Clear();
            _allResults.AddRange(results);
            ApplyAdditionalFilter();
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante la ricerca articoli: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void NotifyVariantSelectionRequired()
    {
        StatusMessage = "Questo articolo ha varianti: seleziona una variante reale dalla lista per inserirla in vendita.";
    }

    private async Task<IReadOnlyList<GestionaleArticleSearchResult>> ExpandVariantResultsAsync(
        IReadOnlyList<GestionaleArticleSearchResult> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return results;
        }

        var expandedResults = new List<GestionaleArticleSearchResult>();
        var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (result.IsVariante)
            {
                AddDistinctVariantResult(expandedResults, addedKeys, result);
                continue;
            }

            if (!result.HasVariantChildren)
            {
                expandedResults.Add(result);
                continue;
            }

            var variants = await _articleReadService.GetArticleVariantsAsync(result.Oid, _selectedPriceListOid, cancellationToken);
            if (variants.Count == 0)
            {
                continue;
            }

            foreach (var variant in variants)
            {
                AddDistinctVariantResult(expandedResults, addedKeys, variant);
            }
        }

        return expandedResults;
    }

    private static void AddDistinctVariantResult(
        ICollection<GestionaleArticleSearchResult> results,
        ISet<string> addedKeys,
        GestionaleArticleSearchResult result)
    {
        var key = string.Join('|',
            result.Oid,
            result.VarianteDettaglioOid1.GetValueOrDefault(),
            result.VarianteDettaglioOid2.GetValueOrDefault(),
            result.VarianteNome ?? string.Empty,
            result.VarianteDescrizione ?? string.Empty);

        if (addedKeys.Add(key))
        {
            results.Add(result);
        }
    }

    private async Task LoadSelectedDetailAsync(GestionaleArticleSearchResult? article)
    {
        if (article is null)
        {
            SelectedDetail = null;
            return;
        }

        try
        {
            var detail = await _articleReadService.GetArticleLookupDetailAsync(article, _selectedPriceListOid);
            if (!ReferenceEquals(SelectedArticle, article))
            {
                return;
            }

            SelectedDetail = detail;
        }
        catch (Exception ex)
        {
            SelectedDetail = null;
            StatusMessage = $"Errore durante il caricamento del dettaglio articolo: {ex.Message}";
        }
    }

    private void ApplyAdditionalFilter()
    {
        var filter = (_additionalFilterText ?? string.Empty).Trim();
        var filteredResults = string.IsNullOrWhiteSpace(filter)
            ? _allResults
            : _allResults.Where(result => MatchesAdditionalFilter(result, filter)).ToList();

        var previouslySelected = SelectedArticle;

        Results.Clear();
        foreach (var result in filteredResults)
        {
            Results.Add(result);
        }

        if (previouslySelected is not null && Results.Contains(previouslySelected))
        {
            SelectedArticle = previouslySelected;
        }
        else
        {
            SelectedArticle = Results.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = "Digita una ricerca multi-termine per filtrare il catalogo legacy.";
            return;
        }

        if (Results.Count == 0)
        {
            StatusMessage = string.IsNullOrWhiteSpace(filter)
                ? "Nessun articolo trovato con i criteri inseriti."
                : "Nessun articolo compatibile con il filtro extra inserito.";
            return;
        }

        StatusMessage = string.IsNullOrWhiteSpace(filter)
            ? $"{Results.Count} risultati trovati. Seleziona l'articolo corretto per vedere il dettaglio completo."
            : $"{Results.Count} risultati filtrati. Seleziona l'articolo corretto per vedere il dettaglio completo.";
    }

    private static bool MatchesAdditionalFilter(GestionaleArticleSearchResult result, string filter)
    {
        var normalizedFilter = filter.Trim().ToUpperInvariant();
        var normalizedCode = (result.CodiceArticolo ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizedCode.StartsWith(normalizedFilter, StringComparison.Ordinal))
        {
            return true;
        }

        var searchableText = string.Join(' ',
            result.CodiceArticolo ?? string.Empty,
            result.Descrizione ?? string.Empty,
            result.VarianteLabel ?? string.Empty,
            result.VarianteDescrizione ?? string.Empty).ToUpperInvariant();

        return searchableText.Contains(normalizedFilter, StringComparison.Ordinal);
    }

    private static string BuildHtmlDocument(string? html, string fallbackHtml)
    {
        var body = string.IsNullOrWhiteSpace(html)
            ? fallbackHtml
            : SanitizeHtml(html);

        return
            $$"""
              <html>
              <head>
                <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                <meta charset="utf-8" />
                <style>
                  body {
                    font-family: 'Segoe UI', sans-serif;
                    font-size: 13px;
                    color: #23344f;
                    margin: 0;
                    padding: 0 4px 0 0;
                    background: #ffffff;
                  }
                  p, li {
                    line-height: 1.45;
                  }
                  ul, ol {
                    margin: 0 0 8px 18px;
                    padding: 0;
                  }
                  strong {
                    color: #1d3557;
                  }
                  hr {
                    border: 0;
                    border-top: 1px solid #d8e4f0;
                    margin: 10px 0;
                  }
                  img {
                    max-width: 100%;
                    height: auto;
                  }
                  a {
                    color: #2f6ebd;
                    text-decoration: none;
                  }
                </style>
              </head>
              <body>{{body}}</body>
              </html>
              """;
    }

    private static string SanitizeHtml(string html)
    {
        var sanitized = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, @"<iframe[\s\S]*?</iframe>", string.Empty, RegexOptions.IgnoreCase);
        return sanitized;
    }

    private static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var normalized = SanitizeHtml(html);
        normalized = Regex.Replace(normalized, @"<(br|/p|/div|/li|/h[1-6])\b[^>]*>", Environment.NewLine, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<li\b[^>]*>", "• ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<[^>]+>", string.Empty, RegexOptions.IgnoreCase);
        normalized = WebUtility.HtmlDecode(normalized);
        normalized = Regex.Replace(normalized, @"[ \t]+\r?\n", Environment.NewLine);
        normalized = Regex.Replace(normalized, @"\r?\n\s*\r?\n+", Environment.NewLine);
        return normalized.Trim();
    }
}
