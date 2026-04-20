namespace Banco.Vendita.Configuration;

public sealed class GestionaleDatabaseSettings
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 3306;

    public string Database { get; set; } = "db_diltech";

    public string Username { get; set; } = "root";

    public string Password { get; set; } = "Root2000$$";

    public string CharacterSet { get; set; } = string.Empty;
}
