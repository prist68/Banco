namespace Banco.Vendita.Fiscal;

public enum CashRegisterOptionAction
{
    DailyJournal = 0,
    CloseCashAndTransmit = 1,
    ReceiptReprint = 2,
    ReceiptCancellation = 3
}

public enum CashJournalMode
{
    Short = 0,
    Medium = 1,
    Long = 2
}

public sealed class CashRegisterOptionSelection
{
    public CashRegisterOptionAction Action { get; init; }

    public CashJournalMode JournalMode { get; init; } = CashJournalMode.Short;

    public string ReceiptDocumentPrefix { get; init; } = string.Empty;

    public int? ReceiptNumber { get; init; }

    public DateTime? ReceiptDate { get; init; }

    public string ReceiptMachineId { get; init; } = "ND";

    public string JournalModeLabel => JournalMode switch
    {
        CashJournalMode.Long => "lunga",
        CashJournalMode.Medium => "media",
        _ => "corta"
    };

    public string ReceiptReferenceLabel
    {
        get
        {
            var prefix = string.IsNullOrWhiteSpace(ReceiptDocumentPrefix)
                ? "????"
                : ReceiptDocumentPrefix.Trim();
            var number = ReceiptNumber.HasValue
                ? ReceiptNumber.Value.ToString("D4")
                : "????";
            var numeroLabel = $"{prefix}-{number}";
            var dataLabel = ReceiptDate.HasValue ? ReceiptDate.Value.ToString("dd/MM/yyyy") : "data non impostata";
            return $"{numeroLabel} del {dataLabel}";
        }
    }
}
