namespace Banco.Vendita.Pos;

public sealed class PosPaymentResult
{
    public bool IsSuccess { get; init; }

    public PosPaymentFailureKind FailureKind { get; init; } = PosPaymentFailureKind.None;

    public string Message { get; init; } = string.Empty;

    public string RequestMessage { get; init; } = string.Empty;

    public string RequestHex { get; init; } = string.Empty;

    public string ResponseMessage { get; init; } = string.Empty;

    public string AuthorizationCode { get; init; } = string.Empty;

    public string CardType { get; init; } = string.Empty;

    public string Stan { get; init; } = string.Empty;

    public IReadOnlyList<string> Frames { get; init; } = [];

    public string TransactionLogFilePath { get; init; } = string.Empty;

    public bool RequiresManualInterventionWarning => FailureKind == PosPaymentFailureKind.FinalResultNotConfirmed;

    public bool IsCancelledByUser => FailureKind == PosPaymentFailureKind.CancelledByUser;
}
