namespace Banco.UI.Grid.Core.Grid;

public sealed class BancoGridActionDefinition
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    public string? InputGestureText { get; init; }

    public BancoGridColorRole ColorRole { get; init; } = BancoGridColorRole.None;

    public bool BeginGroup { get; init; }
}
