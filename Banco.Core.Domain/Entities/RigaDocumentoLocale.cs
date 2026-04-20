using Banco.Core.Domain.Enums;

namespace Banco.Core.Domain.Entities;

public sealed class RigaDocumentoLocale
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int OrdineRiga { get; set; }

    public TipoRigaDocumento TipoRiga { get; set; }

    public int? ArticoloOid { get; set; }

    public string? CodiceArticolo { get; set; }

    public string? BarcodeArticolo { get; set; }

    public int? VarianteDettaglioOid1 { get; set; }

    public int? VarianteDettaglioOid2 { get; set; }

    public string Descrizione { get; set; } = string.Empty;

    public string UnitaMisura { get; set; } = "PZ";

    public decimal Quantita { get; set; }

    public decimal DisponibilitaRiferimento { get; set; }

    public decimal PrezzoUnitario { get; set; }

    public decimal Sconto1 { get; set; }

    public decimal Sconto2 { get; set; }

    public decimal Sconto3 { get; set; }

    public decimal Sconto4 { get; set; }

    public decimal ScontoPercentuale
    {
        get => CalcolaScontoEffettivoPercentuale();
        set
        {
            Sconto1 = Math.Max(0, value);
            Sconto2 = 0;
            Sconto3 = 0;
            Sconto4 = 0;
        }
    }

    public decimal ImportoRiga
        => Math.Round(
            Quantita * PrezzoUnitario * FattoreScontoResiduo(),
            2,
            MidpointRounding.AwayFromZero);

    public int IvaOid { get; set; }

    public decimal AliquotaIva { get; set; }

    public bool FlagManuale { get; set; }

    public bool FlagPropostaNonFiscale { get; set; }

    public bool FlagConfermataFiscale { get; set; }

    public int? PromoCampaignOid { get; set; }

    public string? PromoRuleId { get; set; }

    public string? PromoEventId { get; set; }

    public bool IsPromoRow => TipoRiga is TipoRigaDocumento.PremioSconto or TipoRigaDocumento.PremioArticolo;

    private decimal CalcolaScontoEffettivoPercentuale()
    {
        return Math.Round((1 - FattoreScontoResiduo()) * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private decimal FattoreScontoResiduo()
    {
        var fattore = 1m;
        fattore *= 1 - NormalizzaSconto(Sconto1);
        fattore *= 1 - NormalizzaSconto(Sconto2);
        fattore *= 1 - NormalizzaSconto(Sconto3);
        fattore *= 1 - NormalizzaSconto(Sconto4);
        return Math.Max(0, fattore);
    }

    private static decimal NormalizzaSconto(decimal valore)
    {
        var bounded = Math.Max(0, Math.Min(100, valore));
        return bounded / 100m;
    }
}
