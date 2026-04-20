namespace Banco.Vendita.Configuration;

public sealed class LocalStoreSettings
{
    public string BaseDirectory { get; set; } = string.Empty;

    public string DatabaseFileName { get; set; } = "banco-local.db";

    public string DatabasePath => Path.Combine(BaseDirectory, DatabaseFileName);
}
