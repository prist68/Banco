namespace Banco.Vendita.Articles;

public sealed class ArticleImageRecord
{
    public int Oid { get; init; }

    public int ArticoloOid { get; init; }

    public int? VariantedettaglioOid { get; init; }

    public string VariantedettaglioLabel { get; init; } = string.Empty;

    public bool Predefinita { get; init; }

    public int Posizione { get; init; }

    public string Descrizione { get; init; } = string.Empty;

    public string Fonteimmagine { get; init; } = string.Empty;

    /// <summary>Percorso locale risolto del file; null se il file non è accessibile.</summary>
    public string? LocalPath { get; init; }
}
