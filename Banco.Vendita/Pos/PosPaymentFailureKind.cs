namespace Banco.Vendita.Pos;

public enum PosPaymentFailureKind
{
    None = 0,
    FinalResultNotConfirmed = 1,
    RejectedByTerminal = 2,
    Declined = 3,
    ConnectionUnavailable = 4,
    TechnicalError = 5,
    CancelledByUser = 6
}
