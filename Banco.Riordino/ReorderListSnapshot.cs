namespace Banco.Riordino;

public sealed class ReorderListSnapshot
{
    public ReorderList List { get; init; } = new();

    public IReadOnlyList<ReorderListItem> Items { get; init; } = [];
}
