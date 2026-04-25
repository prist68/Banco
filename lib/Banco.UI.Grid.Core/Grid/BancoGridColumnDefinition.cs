namespace Banco.UI.Grid.Core.Grid;

public sealed class BancoGridColumnDefinition
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    public string? BindingPath { get; init; }

    public string? SortMemberPath { get; init; }

    public string? Group { get; init; }

    public string? Description { get; init; }

    public double Width { get; init; } = 100;

    public double MinWidth { get; init; } = 48;

    public double? MaxWidth { get; init; }

    public bool IsVisibleByDefault { get; init; } = true;

    public bool CanHide { get; init; } = true;

    public bool IsFrozen { get; init; }

    public bool IsReadOnly { get; init; } = true;

    public bool IsNumeric { get; init; }

    public string? Format { get; init; }

    public BancoGridAlignment Alignment { get; init; } = BancoGridAlignment.Left;

    public BancoGridColorRole ColorRole { get; init; } = BancoGridColorRole.None;
}
