namespace Banco.UI.Wpf.ViewModels;

public sealed class ReorderSupplierOptionViewModel
{
    public int Oid { get; init; }

    public string Nome { get; init; } = string.Empty;

    public decimal PrezzoRiferimento { get; init; }

    public DateTime? DataUltimoAcquisto { get; init; }

    public string DisplayLabel => PrezzoRiferimento > 0
        ? $"{Nome} ({PrezzoRiferimento:N2})"
        : Nome;
}
