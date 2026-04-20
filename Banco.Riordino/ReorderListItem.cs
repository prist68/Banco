namespace Banco.Riordino;

public sealed class ReorderListItem
{
    public Guid Id { get; set; }

    public Guid ListId { get; set; }

    public int? ArticoloOid { get; set; }

    public string CodiceArticolo { get; set; } = string.Empty;

    public string Descrizione { get; set; } = string.Empty;

    public decimal Quantita { get; set; }

    public decimal QuantitaDaOrdinare { get; set; }

    public string UnitaMisura { get; set; } = string.Empty;

    public int? FornitoreSuggeritoOid { get; set; }

    public string FornitoreSuggeritoNome { get; set; } = string.Empty;

    public int? FornitoreSelezionatoOid { get; set; }

    public string FornitoreSelezionatoNome { get; set; } = string.Empty;

    public decimal? PrezzoSuggerito { get; set; }

    public int IvaOid { get; set; } = 1;

    public ReorderReason Motivo { get; set; } = ReorderReason.Manuale;

    public ReorderItemStatus Stato { get; set; } = ReorderItemStatus.DaOrdinare;

    public string Operatore { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
