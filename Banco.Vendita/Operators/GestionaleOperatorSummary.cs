namespace Banco.Vendita.Operators;

public sealed class GestionaleOperatorSummary
{
    public string Nome { get; init; } = string.Empty;

    public IReadOnlyCollection<string> MatchTokens { get; init; } = Array.Empty<string>();

    public string DisplayLabel => Nome;

    public bool Matches(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (Nome.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return MatchTokens.Any(token => token.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
