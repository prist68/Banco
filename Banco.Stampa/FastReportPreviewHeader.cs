namespace Banco.Stampa;

public sealed class FastReportPreviewHeader
{
    public int DocumentoOid { get; init; }

    public int Numero { get; init; }

    public int Anno { get; init; }

    public DateTime Data { get; init; }

    public string EtichettaDocumento { get; init; } = string.Empty;

    public string ModelloDocumento { get; init; } = string.Empty;

    public string Operatore { get; init; } = string.Empty;

    public string StatoRuntime { get; init; } = string.Empty;

    public string NumeroCompleto { get; init; } = string.Empty;

    public string DataTesto { get; init; } = string.Empty;

    public string PagamentoLabel { get; init; } = string.Empty;

    public string DocumentoLabel { get; init; } = string.Empty;

    public string AnnoVisuale { get; init; } = string.Empty;

    public string ProgressivoVenditaLabel { get; init; } = string.Empty;
}
