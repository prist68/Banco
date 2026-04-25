namespace Banco.Vendita.Abstractions;

public interface ILegacyReceiptAlignmentService
{
    Task<int> AlignHistoricalReceiptsAsync(CancellationToken cancellationToken = default);
}
