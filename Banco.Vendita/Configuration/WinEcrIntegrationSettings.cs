namespace Banco.Vendita.Configuration;

public sealed class WinEcrIntegrationSettings
{
    public bool Enabled { get; set; } = true;

    public string DriverName { get; set; } = "Ditron";

    public string DeviceSerialNumber { get; set; } = "2CMSG003710";

    public string ReceiptXmlPath { get; set; } = @"C:\tmp\scontrino.xml";

    public string AutoRunCommandFilePath { get; set; } = @"C:\tmp\AutoRun.txt";

    public string AutoRunErrorFilePath { get; set; } = @"C:\tmp\AutoRunErr.txt";

    public string DitronType { get; set; } = "Upgrade";

    public int DriverType { get; set; } = 1;

    public int StandardRowCharacters { get; set; } = 25;

    public int PrecontoRowCharacters { get; set; } = 25;

    public int AutoRunPollingMilliseconds { get; set; } = 300;

    public string ReceiptFooterText { get; set; } = string.Empty;

    public string Notes { get; set; } =
        "Configurazione allineata alla Fiscal Suite Driver e a WinEcrCom 3.0 con file AutoRun.";
}
