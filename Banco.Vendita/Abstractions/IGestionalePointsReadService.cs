using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IGestionalePointsReadService
{
    Task<IReadOnlyList<GestionalePointsCampaignSummary>> GetCampaignsAsync(
        CancellationToken cancellationToken = default);
}
