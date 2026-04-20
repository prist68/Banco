namespace Banco.Vendita.Configuration;

public sealed class DocumentListLayoutSettings
{
    public double StatusWidth { get; set; } = 48;

    public double OidWidth { get; set; } = 80;

    public double DocumentoWidth { get; set; } = 100;

    public double DataWidth { get; set; } = 110;

    public double NominativoWidth { get; set; } = 220;

    public double OraWidth { get; set; } = 90;

    public double TotaleWidth { get; set; } = 100;

    public double StatoWidth { get; set; } = 120;

    public double OperatoreWidth { get; set; } = 120;

    public double ImponibileWidth { get; set; } = 100;

    public double IvaWidth { get; set; } = 90;

    public double ScontrinoWidth { get; set; } = 100;

    public bool ShowStatus { get; set; } = true;

    public bool ShowOid { get; set; } = true;

    public bool ShowDocumento { get; set; } = true;

    public bool ShowData { get; set; } = true;

    public bool ShowOra { get; set; } = true;

    public bool ShowNominativo { get; set; } = true;

    public bool ShowTotale { get; set; } = true;

    public bool ShowStato { get; set; } = true;

    public bool ShowOperatore { get; set; } = false;

    public bool ShowImponibile { get; set; } = false;

    public bool ShowIva { get; set; } = false;

    public bool ShowScontrino { get; set; } = false;
}
