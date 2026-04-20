using Banco.Vendita.Points;

namespace Banco.Vendita.Abstractions;

public interface IPointsPromotionEvaluationService
{
    PromotionEvaluationResult Evaluate(PromotionEvaluationContext context);
}
