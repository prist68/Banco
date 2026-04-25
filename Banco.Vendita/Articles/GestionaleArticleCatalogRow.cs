namespace Banco.Vendita.Articles;

public sealed class GestionaleArticleCatalogRow
{
    public int Oid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public string Descrizione { get; init; } = string.Empty;

    public string CategoryPath { get; init; } = string.Empty;

    public string? BarcodePrincipale { get; init; }

    public decimal Giacenza { get; init; }

    public decimal PrezzoVendita { get; init; }

    public bool HasImage { get; init; }
}
