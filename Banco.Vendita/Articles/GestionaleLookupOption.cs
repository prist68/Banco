namespace Banco.Vendita.Articles;

public sealed class GestionaleLookupOption
{
    public int Oid { get; init; }

    public string Label { get; init; } = string.Empty;

    public override string ToString() => Label;
}
