namespace Banco.Core.Domain.Entities;

public sealed class PagamentoLocale
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string TipoPagamento { get; set; } = string.Empty;

    public decimal Importo { get; set; }

    public string StatoPagamentoLocale { get; set; } = "Registrato";

    public DateTimeOffset DataOra { get; set; } = DateTimeOffset.Now;
}
