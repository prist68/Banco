namespace Banco.UI.Wpf.ViewModels;

public sealed class DocumentGridDetailRowViewModel
{
    public int OrdineRiga { get; init; }

    public string? CodiceArticolo { get; init; }

    public string Descrizione { get; init; } = string.Empty;

    public decimal Quantita { get; init; }

    public decimal PrezzoUnitario { get; init; }

    public decimal ScontoPercentuale { get; init; }

    public decimal AliquotaIva { get; init; }

    public decimal ImportoRiga { get; init; }
}
