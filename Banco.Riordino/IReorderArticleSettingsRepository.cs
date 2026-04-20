namespace Banco.Riordino;

public interface IReorderArticleSettingsRepository
{
    Task<ReorderArticleSettings?> GetByArticleAsync(
        int articoloOid,
        int? varianteDettaglioOid1,
        int? varianteDettaglioOid2,
        string? barcodeAlternativo,
        CancellationToken cancellationToken = default);

    Task SaveAsync(ReorderArticleSettings settings, CancellationToken cancellationToken = default);
}
