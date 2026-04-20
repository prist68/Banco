using Banco.Riordino;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ReorderSupplierDraftViewModel
{
    public Guid ListId { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string CounterLabel { get; init; } = string.Empty;

    public string DateLabel { get; init; } = string.Empty;

    public int RowCount { get; init; }

    public decimal TotalQuantityToOrder { get; init; }

    public ReorderSupplierDraftStatus Status { get; init; }

    public int? FmDocumentOid { get; init; }

    public long? FmDocumentNumber { get; init; }

    public int? FmDocumentYear { get; init; }

    public string StatusLabel => Status switch
    {
        ReorderSupplierDraftStatus.Ordinata => "Ordine fatto",
        ReorderSupplierDraftStatus.RegistrataSuFm => "Creato su FM",
        ReorderSupplierDraftStatus.Chiusa => "Chiusa",
        _ => "Aperta"
    };

    public string FmDocumentLabel => FmDocumentNumber.HasValue && FmDocumentYear.HasValue
        ? $"{FmDocumentNumber}/{FmDocumentYear}"
        : string.Empty;

    public IReadOnlyList<ReorderGridRowViewModel> Rows { get; init; } = [];

    public string SummaryLabel =>
        $"{RowCount} righe, q.ta da ordinare {TotalQuantityToOrder:N2}";
}
