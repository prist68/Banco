using System.Collections.ObjectModel;

namespace Banco.UI.Avalonia.Lab.ViewModels;

public sealed class DesktopPrototypeViewModel
{
    public DesktopPrototypeViewModel()
    {
        NavigationItems =
        [
            new("Dashboard", DashboardIcon, "#5B8DEF"),
            new("Banco", CartIcon, "#4F86DC"),
            new("Magazzino", FolderIcon, "#5B7D91"),
            new("Documenti", DocumentIcon, "#D49327"),
            new("Anagrafiche", StarIcon, "#42A873"),
            new("Contabilita", CashIcon, "#C75C5C"),
            new("Impostazioni", GearIcon, "#7B61D1")
        ];

        QuickActions =
        [
            new("Nuova vendita", "Banco operativo", "#4F86DC", CartIcon),
            new("Documenti", "Lista vendite", "#D49327", DocumentIcon),
            new("Gestione articolo", "Scheda FM-like", "#5B7D91", ArticleIcon),
            new("Lista riordino", "Fabbisogni aperti", "#5B7D91", FolderIcon),
            new("Clienti e punti", "Fidelity", "#42A873", StarIcon),
            new("Stampa", "FastReport POS80", "#7B61D1", PrintIcon)
        ];

        StatusTiles =
        [
            new("Vendite oggi", "-", "In attesa del riepilogo reale", "Dati", "#4F86DC"),
            new("Cortesie aperte", "-", "Documenti non fiscalizzati", "Controllo", "#D49327"),
            new("Sospesi", "-", "Da verificare in Documenti", "Banco", "#C75C5C"),
            new("DB", "db_diltech", "Connessione configurata", "Legacy", "#42A873"),
            new("POS", "Nexi", "Stato operativo sintetico", "Pagamenti", "#5B7D91"),
            new("Fiscale", "WinEcr", "Registratore separato", "Fiscale", "#7B61D1")
        ];

        Alerts =
        [
            new("F.E. disponibili", "Slot pronto per segnalare fatture elettroniche o notifiche future.", "Predisposto", "#7B61D1"),
            new("Documenti non scontrinati", "Apri Documenti per controllare Cortesia, Salva e sospesi recuperabili.", "Da controllare", "#D49327"),
            new("Backup", "Verifica ultima copia e pianificazione archivio.", "Archivio", "#42A873")
        ];

        RecentItems =
        [
            new("--:--", "Ultimi documenti", "Area pronta per mostrare aperture recenti reali.", "Predisposto"),
            new("--:--", "Ultimi articoli", "Area pronta per gli ultimi articoli aperti o modificati.", "Predisposto"),
            new("--:--", "Ultimi clienti", "Area pronta per richiami cliente e fidelity.", "Predisposto")
        ];
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ObservableCollection<QuickActionItem> QuickActions { get; }

    public ObservableCollection<StatusTileItem> StatusTiles { get; }

    public ObservableCollection<AlertItem> Alerts { get; }

    public ObservableCollection<RecentItem> RecentItems { get; }

    private const string DashboardIcon = "M4,4H10V12H4V4M14,4H20V9H14V4M14,13H20V20H14V13M4,16H10V20H4V16Z";
    private const string CartIcon = "M17,18A2,2 0 0,1 19,20A2,2 0 0,1 17,22A2,2 0 0,1 15,20A2,2 0 0,1 17,18M1,2H4.27L5.21,4H20A1,1 0 0,1 21,5L17.3,11.97C16.96,12.58 16.3,13 15.55,13H8.1L7.2,14.63A0.25,0.25 0 0,0 7.42,15H19V17H7C5.89,17 5,16.1 5,15C5,14.65 5.09,14.32 5.24,14.04L6.6,11.59L3,4H1V2Z";
    private const string FolderIcon = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z";
    private const string DocumentIcon = "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M13,9V3.5L18.5,9H13M8,13H16V15H8V13M8,17H13V19H8V17Z";
    private const string StarIcon = "M12,2L15,8H22L16.5,12L18.8,19L12,14.8L5.2,19L7.5,12L2,8H9Z";
    private const string CashIcon = "M5,6H23V18H5V6M14,9A3,3 0 0,1 17,12A3,3 0 0,1 14,15A3,3 0 0,1 11,12A3,3 0 0,1 14,9Z";
    private const string GearIcon = "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12L21.54,9.37L19.66,5.27L16.56,6.05L14.87,5.07L14.5,2.42H9.5L9.13,5.07L7.44,6.05L4.34,5.27L2.34,8.73L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63L4.34,18.73L7.44,17.94L9.13,18.93L9.5,21.58H14.5L14.87,18.93L16.56,17.94L19.66,18.73L21.66,15.27L19.43,12.97Z";
    private const string ArticleIcon = "M4,2H20A2,2 0 0,1 22,4V16A2,2 0 0,1 20,18H16L12,22L8,18H4A2,2 0 0,1 2,16V4A2,2 0 0,1 4,2M6,7H18V9H6V7M6,11H16V13H6V11Z";
    private const string PrintIcon = "M18,3H6V7H18M19,12A1,1 0 0,1 18,11A1,1 0 0,1 19,10A1,1 0 0,1 20,11A1,1 0 0,1 19,12M16,19H8V14H16M19,8H5A3,3 0 0,0 2,11V17H6V21H18V17H22V11A3,3 0 0,0 19,8Z";
}

public sealed record NavigationItem(string Title, string IconData, string AccentColor);

public sealed record QuickActionItem(string Title, string Subtitle, string AccentColor, string IconData);

public sealed record StatusTileItem(string Title, string Value, string Footer, string Badge, string AccentColor);

public sealed record AlertItem(string Title, string Subtitle, string Badge, string AccentColor);

public sealed record RecentItem(string Time, string Title, string Subtitle, string Badge);
