namespace Banco.UI.Grid.Core.Grid;

public sealed class BancoGridRowState
{
    public BancoGridColorRole ColorRole { get; init; } = BancoGridColorRole.None;

    public string? Label { get; init; }

    public bool IsExpandable { get; init; }

    public bool IsLocked { get; init; }
}
