using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPointsPromotionHistoryService
{
    Task RecordAsync(PromotionEventRecord record, CancellationToken cancellationToken = default);

    Task<PromotionEventRecord?> GetLastDocumentEventAsync(Guid localDocumentId, CancellationToken cancellationToken = default);
}
