namespace Banco.Vendita.Configuration;

public sealed class AppSettings
{
    public GestionaleDatabaseSettings GestionaleDatabase { get; set; } = new();

    public LocalStoreSettings LocalStore { get; set; } = new();

    public ShellUiSettings ShellUi { get; set; } = new();

    public PosIntegrationSettings PosIntegration { get; set; } = new();

    public WinEcrIntegrationSettings WinEcrIntegration { get; set; } = new();

    public FmContentSettings FmContent { get; set; } = new();

    public BackupConfigurationSettings BackupConfiguration { get; set; } = new();

    public RestoreConfigurationSettings RestoreConfiguration { get; set; } = new();

    public Dictionary<string, GridLayoutSettings> GridLayouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DocumentListLayoutSettings DocumentListLayout { get; set; } = new();

    public BancoDocumentGridLayoutSettings BancoDocumentGridLayout { get; set; } = new();

    public bool DocumentListIncludeLocalDocuments { get; set; }

    public bool DocumentListUnscontrinatiExpandedMode { get; set; }
}
