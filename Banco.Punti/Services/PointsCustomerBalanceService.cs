using Banco.Core.Domain.Entities;
using Banco.Vendita.Abstractions;
using Banco.Vendita.Customers;
using Banco.Vendita.Points;

namespace Banco.Punti.Services;

public sealed class PointsCustomerBalanceService : IPointsCustomerBalanceService
{
    public PointsCustomerRewardSummary BuildSummary(
        GestionaleCustomerSummary? customer,
        GestionalePointsCampaignSummary? campaign,
        IReadOnlyList<PointsRewardRule> rewardRules,
        DocumentoLocale? document)
    {
        var hasLoyaltyCard = customer?.HaRaccoltaPunti == true;
        if (!hasLoyaltyCard)
        {
            return new PointsCustomerRewardSummary
            {
                HistoricalPoints = 0m,
                CurrentDocumentPoints = 0m,
                RequiredPoints = 0m,
                RewardDescription = "Cliente non agganciato alla carta fedeltà",
                StatusLabel = "Cliente non agganciato alla carta fedeltà"
            };
        }

        var historicalPoints = customer?.PuntiDisponibili ?? 0m;
        var currentDocumentPoints = CalculateCurrentDocumentPoints(campaign, document);
        var totalAvailablePoints = historicalPoints + currentDocumentPoints;

        var eligibleRules = rewardRules
            .Where(rule => rule.IsActive && rule.IsConfigured)
            .OrderBy(rule => rule.RequiredPoints ?? decimal.MaxValue)
            .ToList();

        var selectedRule = ResolveReferenceRule(eligibleRules, totalAvailablePoints);
        var requiredPoints = selectedRule?.RequiredPoints ?? 0m;
        var missingPoints = requiredPoints <= 0 ? 0m : Math.Max(0m, requiredPoints - totalAvailablePoints);

        return new PointsCustomerRewardSummary
        {
            HistoricalPoints = historicalPoints,
            CurrentDocumentPoints = currentDocumentPoints,
            RequiredPoints = requiredPoints,
            RuleName = selectedRule?.RuleName ?? string.Empty,
            RewardDescription = selectedRule?.RewardDescription ?? "Nessun premio configurato",
            StatusLabel = BuildStatusLabel(selectedRule, totalAvailablePoints, missingPoints)
        };
    }

    private static PointsRewardRule? ResolveReferenceRule(
        IReadOnlyList<PointsRewardRule> rules,
        decimal totalAvailablePoints)
    {
        if (rules.Count == 0)
        {
            return null;
        }

        var reachedRule = rules
            .Where(rule => rule.RequiredPoints.GetValueOrDefault() > 0 &&
                           totalAvailablePoints >= rule.RequiredPoints.GetValueOrDefault())
            .OrderByDescending(rule => rule.RequiredPoints)
            .ThenBy(rule => rule.RuleName)
            .FirstOrDefault();

        return reachedRule ?? rules
            .Where(rule => rule.RequiredPoints.GetValueOrDefault() > 0)
            .OrderBy(rule => rule.RequiredPoints)
            .ThenBy(rule => rule.RuleName)
            .FirstOrDefault();
    }

    private static string BuildStatusLabel(
        PointsRewardRule? selectedRule,
        decimal totalAvailablePoints,
        decimal missingPoints)
    {
        if (selectedRule is null)
        {
            return "Nessuna promo configurata";
        }

        if (selectedRule.RequiredPoints.GetValueOrDefault() <= 0)
        {
            return "Soglia premio non configurata";
        }

        if (totalAvailablePoints >= selectedRule.RequiredPoints.GetValueOrDefault())
        {
            return "Premio disponibile";
        }

        return $"Mancano {missingPoints:N2} punti";
    }

    private static decimal CalculateCurrentDocumentPoints(
        GestionalePointsCampaignSummary? campaign,
        DocumentoLocale? document)
    {
        if (campaign?.Attiva != true || document is null)
        {
            return 0m;
        }

        var euroPerPunto = campaign.EuroPerPunto.GetValueOrDefault();
        if (euroPerPunto <= 0)
        {
            return 0m;
        }

        var importoMinimo = campaign.ImportoMinimo.GetValueOrDefault();
        var baseAmount = document.Righe
            .Where(riga => !riga.IsPromoRow)
            .Sum(riga => riga.ImportoRiga);

        if (baseAmount < importoMinimo)
        {
            return 0m;
        }

        return Math.Floor(baseAmount / euroPerPunto);
    }
}
