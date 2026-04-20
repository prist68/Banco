using Banco.Vendita.Abstractions;
using Banco.Vendita.Points;

namespace Banco.Punti.Services;

public sealed class PointsPromotionHistoryService : IPointsPromotionHistoryService
{
    private readonly IPromotionEventRepository _repository;

    public PointsPromotionHistoryService(IPromotionEventRepository repository)
    {
        _repository = repository;
    }

    public Task RecordAsync(PromotionEventRecord record, CancellationToken cancellationToken = default)
    {
        return _repository.AddAsync(record, cancellationToken);
    }

    public Task<PromotionEventRecord?> GetLastDocumentEventAsync(Guid localDocumentId, CancellationToken cancellationToken = default)
    {
        return _repository.GetLastByDocumentAsync(localDocumentId, cancellationToken);
    }
}
