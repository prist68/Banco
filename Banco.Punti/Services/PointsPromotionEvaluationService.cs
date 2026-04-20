using Banco.Vendita.Abstractions;
using Banco.Vendita.Points;

namespace Banco.Punti.Services;

public sealed class PointsPromotionEvaluationService : IPointsPromotionEvaluationService
{
    private readonly IPointsCustomerBalanceService _balanceService;

    public PointsPromotionEvaluationService(IPointsCustomerBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    public PromotionEvaluationResult Evaluate(PromotionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidateRules = context.RewardRules
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.RequiredPoints ?? decimal.MaxValue)
            .ThenBy(rule => rule.RuleName)
            .ToList();

        var referenceSummary = _balanceService.BuildSummary(context.Customer, context.Campaign, candidateRules, context.Document);

        if (context.Customer is null)
        {
            return Build(PromotionEventType.NotEligible, "Cliente non selezionato", "Seleziona un cliente per valutare il premio.", referenceSummary, null);
        }

        if (context.Customer.IsClienteGenerico)
        {
            return new PromotionEvaluationResult
            {
                EventType = PromotionEventType.NotEligible,
                IsGenericCustomer = true,
                Title = "Cliente generico: promo non applicabile",
                Message = "La promo resta bloccata finche' il cliente non viene identificato.",
                Summary = referenceSummary,
                RewardRule = null
            };
        }

        if (context.Customer.HaRaccoltaPunti != true)
        {
            return new PromotionEvaluationResult
            {
                EventType = PromotionEventType.NotEligible,
                IsConfigured = false,
                Title = "Carta fedeltà non attiva",
                Message = "Questo cliente non è agganciato alla raccolta punti: inserisci o verifica il codice carta fedeltà.",
                Summary = referenceSummary,
                RewardRule = null
            };
        }

        if (context.Campaign?.Attiva != true)
        {
            return Build(PromotionEventType.NotEligible, "Campagna punti non attiva", "Attiva una campagna punti prima di usare il premio.", referenceSummary, null);
        }

        if (candidateRules.Count == 0)
        {
            return new PromotionEvaluationResult
            {
                EventType = PromotionEventType.NotEligible,
                IsConfigured = false,
                Title = "Nessuna promo configurata",
                Message = "Aggiungi una regola premio attiva nel modulo Punti prima di usarla in vendita.",
                Summary = referenceSummary,
                RewardRule = null
            };
        }

        var configuredRules = candidateRules.Where(rule => rule.IsConfigured).ToList();
        if (configuredRules.Count == 0)
        {
            var incompleteRule = candidateRules.FirstOrDefault();
            var incompleteRuleLabel = string.IsNullOrWhiteSpace(incompleteRule?.RuleName)
                ? "la regola premio attiva"
                : $"la regola '{incompleteRule.RuleName}'";

            return new PromotionEvaluationResult
            {
                EventType = PromotionEventType.NotEligible,
                IsConfigured = false,
                Title = "Regole premio incomplete",
                Message = incompleteRule is null
                    ? "Completa almeno una regola premio attiva prima di usarla in vendita."
                    : $"Completa {incompleteRuleLabel}: {BuildIncompleteRuleReason(incompleteRule)}.",
                Summary = referenceSummary,
                RewardRule = null
            };
        }

        var reachedRules = configuredRules
            .Select(rule => new
            {
                Rule = rule,
                Summary = _balanceService.BuildSummary(context.Customer, context.Campaign, [rule], context.Document)
            })
            .Where(item => item.Summary.RequiredPoints > 0 && item.Summary.TotalAvailablePoints >= item.Summary.RequiredPoints)
            .OrderByDescending(item => item.Summary.RequiredPoints)
            .ThenBy(item => item.Rule.RuleName)
            .ToList();

        var bestReachedRule = reachedRules.FirstOrDefault();
        var eligibleRewardRules = reachedRules.Select(item => item.Rule).ToList();

        if (context.RewardAlreadyApplied)
        {
            var appliedSummary = bestReachedRule?.Summary ?? referenceSummary;
            return new PromotionEvaluationResult
            {
                EventType = PromotionEventType.Applied,
                IsConfigured = true,
                Title = "Premio gia` applicato",
                Message = $"Il premio '{bestReachedRule?.Rule.RewardDescription ?? referenceSummary.RewardDescription}' e` gia` presente sul documento.",
                Summary = appliedSummary,
                RewardRule = bestReachedRule?.Rule,
                EligibleRewardRules = eligibleRewardRules
            };
        }

        if (bestReachedRule is null)
        {
            var nextRule = configuredRules
                .Select(rule => new
                {
                    Rule = rule,
                    Summary = _balanceService.BuildSummary(context.Customer, context.Campaign, [rule], context.Document)
                })
                .OrderBy(item => item.Summary.RequiredPoints <= 0 ? decimal.MaxValue : item.Summary.RequiredPoints)
                .First();

            return new PromotionEvaluationResult
            {
                EventType = PromotionEventType.NotEligible,
                IsConfigured = true,
                Title = $"Mancano {nextRule.Summary.MissingPoints:N2} punti",
                Message = $"La prossima regola disponibile e` '{nextRule.Rule.RuleName}'.",
                Summary = nextRule.Summary,
                RewardRule = nextRule.Rule
            };
        }

        var sameRewardRuleAsLastEvent = context.LastEventRuleId.HasValue &&
                                        context.LastEventRuleId.Value == bestReachedRule.Rule.Id;
        var sameRewardThresholdAsLastEvent = sameRewardRuleAsLastEvent &&
                                             context.LastEventRequiredPoints.GetValueOrDefault() == bestReachedRule.Summary.RequiredPoints;
        var rewardAlreadyHandledForCurrentThreshold =
            sameRewardThresholdAsLastEvent &&
            context.LastEventType is PromotionEventType.Applied or PromotionEventType.Rejected;
        var shouldShowPopup = bestReachedRule.Rule.EnableSaleCheck &&
                              !rewardAlreadyHandledForCurrentThreshold;

        return new PromotionEvaluationResult
        {
            EventType = PromotionEventType.Eligible,
            IsConfigured = true,
            ShouldShowPopup = shouldShowPopup,
            Title = "Premio disponibile",
            Message = $"Il cliente ha raggiunto la soglia per '{bestReachedRule.Rule.RuleName}'.",
            Summary = bestReachedRule.Summary,
            RewardRule = bestReachedRule.Rule,
            EligibleRewardRules = eligibleRewardRules
        };
    }

    private static PromotionEvaluationResult Build(
        PromotionEventType eventType,
        string title,
        string message,
        PointsCustomerRewardSummary summary,
        PointsRewardRule? rewardRule)
    {
        return new PromotionEvaluationResult
        {
            EventType = eventType,
            IsConfigured = rewardRule?.IsConfigured == true,
            Title = title,
            Message = message,
            Summary = summary,
            RewardRule = rewardRule
        };
    }

    private static string BuildIncompleteRuleReason(PointsRewardRule rule)
    {
        if (rule.RequiredPoints.GetValueOrDefault() <= 0)
        {
            return "mancano i punti richiesti";
        }

        return rule.RewardType switch
        {
            PointsRewardType.ScontoFisso when rule.DiscountAmount.GetValueOrDefault() <= 0
                => "manca l'importo dello sconto fisso",
            PointsRewardType.ScontoPercentuale when rule.DiscountPercent.GetValueOrDefault() <= 0
                => "manca la percentuale di sconto",
            PointsRewardType.ArticoloPremio when rule.RewardArticleOid.GetValueOrDefault() <= 0
                => "manca l'articolo premio",
            PointsRewardType.ArticoloPremio when rule.RewardQuantity <= 0
                => "manca la quantita` premio",
            _ => "verifica i campi obbligatori della regola premio"
        };
    }
}
