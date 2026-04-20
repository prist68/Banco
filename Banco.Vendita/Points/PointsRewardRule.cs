using Banco.Vendita.Articles;

namespace Banco.Vendita.Points;

public sealed class PointsRewardRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int CampaignOid { get; set; }

    public string RuleName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public decimal? RequiredPoints { get; set; }

    public PointsRewardType RewardType { get; set; } = PointsRewardType.ScontoFisso;

    public decimal? DiscountAmount { get; set; }

    public decimal? DiscountPercent { get; set; }

    public int? RewardArticleOid { get; set; }

    public string? RewardArticleCode { get; set; }

    public string? RewardArticleDescription { get; set; }

    public int? RewardArticleIvaOid { get; set; }

    public decimal? RewardArticleAliquotaIva { get; set; }

    public int? RewardArticleTipoArticoloOid { get; set; }

    public decimal? RewardArticlePrezzoVendita { get; set; }

    public decimal RewardQuantity { get; set; } = 1m;

    public bool EnableSaleCheck { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public bool IsConfigured => CampaignOid > 0 &&
                                RequiredPoints.GetValueOrDefault() > 0 &&
                                (RewardType switch
                                {
                                    PointsRewardType.ScontoFisso => DiscountAmount.GetValueOrDefault() > 0,
                                    PointsRewardType.ScontoPercentuale => DiscountPercent.GetValueOrDefault() > 0,
                                    _ => RewardArticleOid.GetValueOrDefault() > 0 && RewardQuantity > 0
                                });

    public string RewardTypeLabel => RewardType switch
    {
        PointsRewardType.ScontoFisso => "Sconto fisso (EUR)",
        PointsRewardType.ScontoPercentuale => "Sconto percentuale (%)",
        _ => "Articolo premio"
    };

    public string RewardDescription => RewardType switch
    {
        PointsRewardType.ScontoFisso => $"Sconto {DiscountAmount.GetValueOrDefault():N2} EUR",
        PointsRewardType.ScontoPercentuale => $"Sconto {DiscountPercent.GetValueOrDefault():N2}%",
        _ => string.IsNullOrWhiteSpace(RewardArticleDescription)
            ? "Articolo premio non configurato"
            : $"{RewardArticleCode} - {RewardArticleDescription} x {RewardQuantity:N2}"
    };

    public bool RewardArticleUsesNegativeAmount => RewardArticleTipoArticoloOid == 7;

    public string StateLabel => IsActive ? "Attiva" : "Disattiva";

    public void ApplyArticle(GestionaleArticleSearchResult? article)
    {
        RewardArticleOid = article?.Oid;
        RewardArticleCode = article?.CodiceArticolo;
        RewardArticleDescription = article?.Descrizione;
        RewardArticleIvaOid = article?.IvaOid;
        RewardArticleAliquotaIva = article?.AliquotaIva;
        RewardArticleTipoArticoloOid = article?.TipoArticoloOid;
        RewardArticlePrezzoVendita = article?.PrezzoVendita;
    }

    public void ClearArticle()
    {
        RewardArticleOid = null;
        RewardArticleCode = null;
        RewardArticleDescription = null;
        RewardArticleIvaOid = null;
        RewardArticleAliquotaIva = null;
        RewardArticleTipoArticoloOid = null;
        RewardArticlePrezzoVendita = null;
    }

    public PointsRewardRule Clone()
    {
        return new PointsRewardRule
        {
            Id = Id,
            CampaignOid = CampaignOid,
            RuleName = RuleName,
            IsActive = IsActive,
            RequiredPoints = RequiredPoints,
            RewardType = RewardType,
            DiscountAmount = DiscountAmount,
            DiscountPercent = DiscountPercent,
            RewardArticleOid = RewardArticleOid,
            RewardArticleCode = RewardArticleCode,
            RewardArticleDescription = RewardArticleDescription,
            RewardArticleIvaOid = RewardArticleIvaOid,
            RewardArticleAliquotaIva = RewardArticleAliquotaIva,
            RewardArticleTipoArticoloOid = RewardArticleTipoArticoloOid,
            RewardArticlePrezzoVendita = RewardArticlePrezzoVendita,
            RewardQuantity = RewardQuantity,
            EnableSaleCheck = EnableSaleCheck,
            Notes = Notes,
            UpdatedAt = UpdatedAt
        };
    }
}
