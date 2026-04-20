namespace Banco.Stampa;

public sealed record LegacyRepxBindingReference
{
    public string Band { get; init; } = string.Empty;

    public string ControlName { get; init; } = string.Empty;

    public string Expression { get; init; } = string.Empty;

    public string? Notes { get; init; }
}
