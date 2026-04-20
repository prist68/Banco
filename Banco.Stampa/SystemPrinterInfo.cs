namespace Banco.Stampa;

public sealed class SystemPrinterInfo
{
    public string Name { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public bool IsAvailable { get; init; }
}
