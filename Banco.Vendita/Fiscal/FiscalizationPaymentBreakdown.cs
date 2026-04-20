namespace Banco.Vendita.Fiscal;

public sealed class FiscalizationPaymentBreakdown
{
    public decimal Contanti { get; init; }

    public decimal Carta { get; init; }

    public decimal Web { get; init; }

    public decimal Buoni { get; init; }

    public decimal Sospeso { get; init; }
}
