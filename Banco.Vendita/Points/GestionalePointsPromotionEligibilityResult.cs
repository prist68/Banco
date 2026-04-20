namespace Banco.Vendita.Points;

public sealed class GestionalePointsPromotionEligibilityResult
{
    public bool CanApply { get; init; }

    public bool CustomerIsGeneric { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public static GestionalePointsPromotionEligibilityResult Blocked(
        string title,
        string message,
        bool customerIsGeneric = false)
    {
        return new GestionalePointsPromotionEligibilityResult
        {
            CanApply = false,
            CustomerIsGeneric = customerIsGeneric,
            Title = title,
            Message = message
        };
    }

    public static GestionalePointsPromotionEligibilityResult Allowed(
        string title,
        string message)
    {
        return new GestionalePointsPromotionEligibilityResult
        {
            CanApply = true,
            Title = title,
            Message = message
        };
    }
}
