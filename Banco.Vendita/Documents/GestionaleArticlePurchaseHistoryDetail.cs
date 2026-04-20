namespace Banco.Vendita.Documents;

public sealed class GestionaleArticlePurchaseHistoryDetail
{
    public IReadOnlyList<GestionaleArticlePurchaseHistoryItem> Items { get; init; } = [];

    public GestionaleArticlePurchaseHistorySummary Summary { get; init; } = new();
}
