using Banco.Core.Domain.Entities;
using Banco.Core.Domain.Enums;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Points;

namespace Banco.Punti.Services;

public sealed class PointsPromotionDocumentService : IPointsPromotionDocumentService
{
    public PromotionEventRecord ApplyReward(
        DocumentoLocale document,
        int customerOid,
        GestionalePointsCampaignSummary campaign,
        PointsRewardRule rewardRule,
        PromotionEvaluationResult evaluationResult)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(rewardRule);
        ArgumentNullException.ThrowIfNull(evaluationResult);

        var existingPromoRow = document.Righe.FirstOrDefault(riga => riga.IsPromoRow);
        if (existingPromoRow is not null)
        {
            return CreateRecord(PromotionEventType.Applied, campaign.Oid, rewardRule, customerOid, document.Id, existingPromoRow.Id, evaluationResult);
        }

        var eventId = Guid.NewGuid();
        var row = rewardRule.RewardType switch
        {
            PointsRewardType.ScontoFisso => CreateFixedDiscountRow(document, campaign, rewardRule, eventId),
            PointsRewardType.ScontoPercentuale => CreatePercentageDiscountRow(document, campaign, rewardRule, eventId),
            _ => CreateRewardArticleRow(document, campaign, rewardRule, eventId)
        };

        document.AggiungiRiga(row);

        return CreateRecord(PromotionEventType.Applied, campaign.Oid, rewardRule, customerOid, document.Id, row.Id, evaluationResult, eventId);
    }

    public PromotionEventRecord? ReverseReward(
        DocumentoLocale document,
        PromotionEvaluationResult evaluationResult)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(evaluationResult);

        var rewardRule = evaluationResult.RewardRule;
        if (rewardRule is null)
        {
            return null;
        }

        var rewardRow = document.Righe
            .OrderByDescending(riga => riga.OrdineRiga)
            .FirstOrDefault(riga => riga.IsPromoRow &&
                                   string.Equals(riga.PromoRuleId, rewardRule.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));

        rewardRow ??= document.Righe
            .OrderByDescending(riga => riga.OrdineRiga)
            .FirstOrDefault(riga => riga.IsPromoRow &&
                                   riga.PromoCampaignOid == rewardRule.CampaignOid);

        if (rewardRow is null)
        {
            return null;
        }

        document.RimuoviRiga(rewardRow.Id);
        return new PromotionEventRecord
        {
            CampaignOid = rewardRule.CampaignOid,
            RuleId = rewardRule.Id,
            CustomerOid = null,
            LocalDocumentId = document.Id,
            EventType = PromotionEventType.Reversed,
            RewardType = rewardRule.RewardType,
            AppliedRowId = rewardRow.Id,
            AvailablePoints = evaluationResult.Summary.TotalAvailablePoints,
            RequiredPoints = evaluationResult.Summary.RequiredPoints,
            Title = "Premio reversato",
            Message = "Il documento e` tornato sotto soglia oppure il premio e` stato annullato."
        };
    }

    private static RigaDocumentoLocale CreateFixedDiscountRow(
        DocumentoLocale document,
        GestionalePointsCampaignSummary campaign,
        PointsRewardRule rewardRule,
        Guid eventId)
    {
        return CreateDiscountRow(
            document,
            campaign,
            rewardRule,
            eventId,
            -Math.Abs(rewardRule.DiscountAmount.GetValueOrDefault()),
            $"Premio punti - {rewardRule.RuleName}");
    }

    private static RigaDocumentoLocale CreatePercentageDiscountRow(
        DocumentoLocale document,
        GestionalePointsCampaignSummary campaign,
        PointsRewardRule rewardRule,
        Guid eventId)
    {
        var baseAmount = document.Righe.Where(riga => !riga.IsPromoRow).Sum(riga => riga.ImportoRiga);
        var percentage = Math.Max(0, rewardRule.DiscountPercent.GetValueOrDefault());
        var discountAmount = Math.Round(baseAmount * percentage / 100m, 2, MidpointRounding.AwayFromZero);

        return CreateDiscountRow(
            document,
            campaign,
            rewardRule,
            eventId,
            -Math.Abs(discountAmount),
            $"Premio punti - {rewardRule.RuleName} ({percentage:N2}%)");
    }

    private static RigaDocumentoLocale CreateDiscountRow(
        DocumentoLocale document,
        GestionalePointsCampaignSummary campaign,
        PointsRewardRule rewardRule,
        Guid eventId,
        decimal amount,
        string description)
    {
        return new RigaDocumentoLocale
        {
            OrdineRiga = document.Righe.Count + 1,
            TipoRiga = TipoRigaDocumento.PremioSconto,
            Descrizione = description,
            Quantita = 1,
            PrezzoUnitario = amount,
            IvaOid = 1,
            AliquotaIva = 0,
            FlagManuale = false,
            PromoCampaignOid = campaign.Oid,
            PromoRuleId = rewardRule.Id.ToString("D"),
            PromoEventId = eventId.ToString("D")
        };
    }

    private static RigaDocumentoLocale CreateRewardArticleRow(
        DocumentoLocale document,
        GestionalePointsCampaignSummary campaign,
        PointsRewardRule rewardRule,
        Guid eventId)
    {
        var prezzoUnitario = rewardRule.RewardArticleUsesNegativeAmount
            ? -Math.Abs(rewardRule.RewardArticlePrezzoVendita.GetValueOrDefault())
            : 0;

        return new RigaDocumentoLocale
        {
            OrdineRiga = document.Righe.Count + 1,
            TipoRiga = TipoRigaDocumento.PremioArticolo,
            ArticoloOid = rewardRule.RewardArticleOid,
            CodiceArticolo = rewardRule.RewardArticleCode,
            Descrizione = $"Premio punti - {rewardRule.RewardArticleDescription ?? rewardRule.RuleName ?? campaign.NomeOperazione}",
            Quantita = rewardRule.RewardQuantity <= 0 ? 1 : rewardRule.RewardQuantity,
            PrezzoUnitario = prezzoUnitario,
            IvaOid = rewardRule.RewardArticleIvaOid ?? 1,
            AliquotaIva = rewardRule.RewardArticleAliquotaIva ?? 0,
            FlagManuale = false,
            PromoCampaignOid = campaign.Oid,
            PromoRuleId = rewardRule.Id.ToString("D"),
            PromoEventId = eventId.ToString("D")
        };
    }

    private static PromotionEventRecord CreateRecord(
        PromotionEventType eventType,
        int campaignOid,
        PointsRewardRule rewardRule,
        int customerOid,
        Guid documentId,
        Guid rowId,
        PromotionEvaluationResult evaluationResult,
        Guid? eventId = null)
    {
        return new PromotionEventRecord
        {
            Id = eventId ?? Guid.NewGuid(),
            CampaignOid = campaignOid,
            RuleId = rewardRule.Id,
            CustomerOid = customerOid,
            LocalDocumentId = documentId,
            EventType = eventType,
            RewardType = rewardRule.RewardType,
            AvailablePoints = evaluationResult.Summary.TotalAvailablePoints,
            RequiredPoints = rewardRule.RequiredPoints.GetValueOrDefault(),
            AppliedRowId = rowId,
            Title = eventType == PromotionEventType.Applied ? "Premio applicato" : evaluationResult.Title,
            Message = eventType == PromotionEventType.Applied
                ? $"Applicato premio '{rewardRule.RewardDescription}'."
                : evaluationResult.Message
        };
    }
}
