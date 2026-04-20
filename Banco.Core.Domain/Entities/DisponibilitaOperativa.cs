namespace Banco.Core.Domain.Entities;

public sealed class DisponibilitaOperativa
{
    public int ArticoloOid { get; init; }

    public string CodiceArticolo { get; init; } = string.Empty;

    public decimal UltimaDisponibilitaGestionale { get; init; }

    public decimal QuantitaImpegnataLocale { get; init; }

    public decimal DisponibilitaCalcolata => UltimaDisponibilitaGestionale - QuantitaImpegnataLocale;

    public DateTimeOffset DataOraUltimoRefresh { get; init; } = DateTimeOffset.Now;
}
