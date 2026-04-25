namespace Banco.Vendita.Articles;

public sealed class ArticleImageAddRequest
{
    public required int ArticoloOid { get; init; }

    public required string CodiceArticolo { get; init; }

    /// <summary>Percorso completo del file sorgente da copiare nella cartella immagini.</summary>
    public required string SourceFilePath { get; init; }

    public int? VariantedettaglioOid { get; init; }
}
