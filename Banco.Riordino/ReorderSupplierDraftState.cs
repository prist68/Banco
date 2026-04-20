namespace Banco.Riordino;

public sealed class ReorderSupplierDraftState
{
    public Guid Id { get; set; }

    public Guid ListId { get; set; }

    public string SupplierName { get; set; } = string.Empty;

    public int LocalCounter { get; set; }

    public DateTimeOffset DraftDate { get; set; } = DateTimeOffset.Now;

    public ReorderSupplierDraftStatus Status { get; set; } = ReorderSupplierDraftStatus.Aperta;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? OrderedAt { get; set; }

    public DateTimeOffset? RegisteredOnFmAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public int? FmDocumentOid { get; set; }

    public long? FmDocumentNumber { get; set; }

    public int? FmDocumentYear { get; set; }
}
