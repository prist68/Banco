namespace Banco.Vendita.Configuration;

public sealed class AiIntegrationSettings
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "DeepSeek";

    public string ApiBaseUrl { get; set; } = "https://api.deepseek.com";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "deepseek-v4-pro";

    public string Notes { get; set; } = string.Empty;
}
