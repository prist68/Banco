namespace Banco.Stampa;

public sealed record LegacyRepxParameterReference
{
    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Notes { get; init; }
}
