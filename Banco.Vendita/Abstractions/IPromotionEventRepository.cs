using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPromotionEventRepository
{
    Task AddAsync(PromotionEventRecord record, CancellationToken cancellationToken = default);

    Task<PromotionEventRecord?> GetLastByDocumentAsync(Guid localDocumentId, CancellationToken cancellationToken = default);

    Task<PromotionEventRecord?> GetLastByCampaignAndCustomerAsync(int campaignOid, int customerOid, CancellationToken cancellationToken = default);
}
