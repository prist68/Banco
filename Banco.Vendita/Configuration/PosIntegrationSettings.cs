namespace Banco.Vendita.Configuration;

public sealed class PosIntegrationSettings
{
    public bool Enabled { get; set; } = true;

    public string DeviceName { get; set; } = "Nexi SmartPOS P61B";

    public string ManagementMode { get; set; } = "Client";

    public string ProtocolType { get; set; } = "Generico protocollo 17";

    public string ConnectionType { get; set; } = "ETH";

    public string PosIpAddress { get; set; } = "192.168.1.233";

    public int PosPort { get; set; } = 8081;

    public string TerminalId { get; set; } = string.Empty;

    public string CashRegisterId { get; set; } = "00000001";

    public string CashRegisterIpAddress { get; set; } = "192.168.1.231";

    public int CashRegisterPort { get; set; } = 1470;

    public bool AmountExchangeRequired { get; set; } = true;

    public bool ConfirmAmountFromEcr { get; set; } = true;

    public bool PrintTicketOnEcr { get; set; } = true;

    public bool CheckTerminalId { get; set; }

    public string ReceiptFooterText { get; set; } = string.Empty;

    public string Notes { get; set; } =
        "Porta POS confermata su app Scambio Importo: 8081. Il Codice cassa deve essere un ID a 8 cifre, non l'IP del PC.";
}
