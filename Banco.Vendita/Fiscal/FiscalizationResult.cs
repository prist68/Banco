namespace Banco.Vendita.Fiscal;

public sealed class FiscalizationResult
{
    public int DocumentoGestionaleOid { get; init; }

    public long NumeroDocumentoGestionale { get; init; }

    public int AnnoDocumentoGestionale { get; init; }

    public DateTime DataDocumentoGestionale { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool WinEcrExecuted { get; init; }

    public string? WinEcrMessage { get; init; }

    public string? WinEcrErrorDetails { get; init; }

    public int? WinEcrErrorCode { get; init; }

    public LegacyPublishOutcomeKind OutcomeKind { get; init; } = LegacyPublishOutcomeKind.LegacyPublished;

    public string? TechnicalWarningMessage { get; init; }
}
