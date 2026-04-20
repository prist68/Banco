namespace Banco.Stampa;

public sealed class FastReportPreviewPayment
{
    public string Tipo { get; init; } = string.Empty;

    public decimal Importo { get; init; }

    public string ImportoVisuale { get; init; } = string.Empty;
}
