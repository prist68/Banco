namespace Banco.UI.Wpf.ViewModels;

public sealed class DocumentGridDetailViewModel
{
    public string DocumentoLabel { get; init; } = string.Empty;

    public string SourceLabel { get; init; } = string.Empty;

    public string Cliente { get; init; } = string.Empty;

    public string Operatore { get; init; } = string.Empty;

    public string StatoDocumento { get; init; } = string.Empty;

    public string ScontrinoLabel { get; init; } = string.Empty;

    public decimal Totale { get; init; }

    public decimal TotalePagato { get; init; }

    public decimal ResiduoPagamento { get; init; }

    public decimal DaFiscalizzare { get; init; }

    public bool IsLocal { get; init; }
}
