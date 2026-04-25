namespace Banco.Vendita.Configuration;

public sealed class RestoreConfigurationSettings
{
    public string DefaultRestoreDirectory { get; set; } = @"C:\Facile Manager";

    public bool AutoSelectLatestBackup { get; set; } = true;
}
