namespace Banco.Vendita.Configuration;

public sealed class BancoDocumentGridLayoutSettings
{
    public double RigaWidth { get; set; } = 70;

    public double CodiceWidth { get; set; } = 120;

    public double DescrizioneWidth { get; set; } = 420;

    public double QuantitaWidth { get; set; } = 100;

    public double DisponibilitaWidth { get; set; } = 70;

    public double PrezzoWidth { get; set; } = 100;

    public double ScontoWidth { get; set; } = 90;

    public double ImportoWidth { get; set; } = 110;

    public double IvaWidth { get; set; } = 70;

    public double UnitaMisuraWidth { get; set; } = 52;

    public double TipoRigaWidth { get; set; } = 80;

    // Compatibilita` con layout storici che usavano la chiave "Tipo" per la colonna UM.
    public double TipoWidth
    {
        get => UnitaMisuraWidth;
        set => UnitaMisuraWidth = value;
    }

    public double AzioniWidth { get; set; } = 80;

    public bool ShowRiga { get; set; } = true;

    public bool ShowCodice { get; set; } = true;

    public bool ShowDescrizione { get; set; } = true;

    public bool ShowQuantita { get; set; } = true;

    public bool ShowDisponibilita { get; set; } = true;

    public bool ShowPrezzo { get; set; } = true;

    public bool ShowSconto { get; set; } = true;

    public bool ShowImporto { get; set; } = true;

    public bool ShowIva { get; set; } = true;

    public bool ShowUnitaMisura { get; set; } = true;

    public bool ShowTipoRiga { get; set; } = true;

    // Compatibilita` con layout storici che usavano la chiave "Tipo" per la colonna UM.
    public bool ShowTipo
    {
        get => ShowUnitaMisura;
        set => ShowUnitaMisura = value;
    }

    public bool ShowAzioni { get; set; } = true;

    public int RigaDisplayIndex { get; set; } = 0;

    public int CodiceDisplayIndex { get; set; } = 1;

    public int DescrizioneDisplayIndex { get; set; } = 2;

    public int QuantitaDisplayIndex { get; set; } = 3;

    public int DisponibilitaDisplayIndex { get; set; } = 4;

    public int PrezzoDisplayIndex { get; set; } = 5;

    public int ScontoDisplayIndex { get; set; } = 6;

    public int ImportoDisplayIndex { get; set; } = 7;

    public int IvaDisplayIndex { get; set; } = 8;

    public int UnitaMisuraDisplayIndex { get; set; } = 9;

    public int TipoRigaDisplayIndex { get; set; } = 10;

    public int AzioniDisplayIndex { get; set; } = 11;

    // Compatibilita` con layout storici che usavano la chiave "Tipo" per la colonna UM.
    public int TipoDisplayIndex
    {
        get => UnitaMisuraDisplayIndex;
        set => UnitaMisuraDisplayIndex = value;
    }
}
