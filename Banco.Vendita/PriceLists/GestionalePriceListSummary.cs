namespace Banco.Vendita.PriceLists;

public sealed class GestionalePriceListSummary
{
    public int Oid { get; init; }

    public string Nome { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public bool IsVisibleInSearch { get; init; }

    public bool IsVisibleInDocuments { get; init; }

    public bool IsWeb { get; init; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Nome) ? $"Listino {Oid}" : Nome;
}
