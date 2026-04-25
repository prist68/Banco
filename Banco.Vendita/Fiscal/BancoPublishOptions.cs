namespace Banco.Vendita.Fiscal;

public sealed class BancoPublishOptions
{
    public static BancoPublishOptions Default { get; } = new();

    public bool SkipWinEcr { get; init; }

    public bool ForceNewLegacyDocument { get; init; }

    public bool DeleteExistingNonFiscalizedLegacyDocument { get; init; }
}
