namespace Banco.Vendita.Abstractions;

public interface ILocalArticleTagRepository
{
    Task<IReadOnlyList<string>?> GetTagsAsync(
        int articoloOid,
        CancellationToken cancellationToken = default);

    Task SaveTagsAsync(
        int articoloOid,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSuggestedTagsAsync(
        string? searchText = null,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
