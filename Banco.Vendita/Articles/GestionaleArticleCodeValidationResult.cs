namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleCodeValidationResult
{
    public string CodiceArticolo { get; init; } = string.Empty;

    public string DescrizioneArticolo { get; init; } = string.Empty;

    public int MatchCount { get; init; }

    public IReadOnlyList<GestionaleArticleCodeValidationMatch> Matches { get; init; } = [];

    public bool IsUnique => MatchCount == 1;

    public bool IsDuplicate => MatchCount > 1;
}

public sealed class GestionaleArticleCodeValidationMatch
{
    public int ArticoloOid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string DescrizioneArticolo { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = string.Empty;

    public string? VarianteLabel { get; init; }

    public string DisplayLabel
    {
        get
        {
            var variant = string.IsNullOrWhiteSpace(VarianteLabel)
                ? string.Empty
                : $" [{VarianteLabel}]";

            var source = string.IsNullOrWhiteSpace(SourceLabel)
                ? string.Empty
                : $" - {SourceLabel}";

            return $"{CodiceArticolo} - {DescrizioneArticolo}{variant}{source}";
        }
    }
}
