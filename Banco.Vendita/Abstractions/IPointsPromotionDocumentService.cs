using Banco.Core.Domain.Entities;
using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPointsPromotionDocumentService
{
    PromotionEventRecord ApplyReward(
        DocumentoLocale document,
        int customerOid,
        GestionalePointsCampaignSummary campaign,
        PointsRewardRule rewardRule,
        PromotionEvaluationResult evaluationResult);

    PromotionEventRecord? ReverseReward(
        DocumentoLocale document,
        PromotionEvaluationResult evaluationResult);
}
