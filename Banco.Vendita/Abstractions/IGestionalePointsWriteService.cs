using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IGestionalePointsWriteService
{
    Task<GestionalePointsCampaignEditModel?> GetCampaignAsync(
        int campaignOid,
        CancellationToken cancellationToken = default);

    Task<int> SaveCampaignAsync(
        GestionalePointsCampaignEditModel campaign,
        CancellationToken cancellationToken = default);

    Task CancelCampaignAsync(
        int campaignOid,
        CancellationToken cancellationToken = default);
}
