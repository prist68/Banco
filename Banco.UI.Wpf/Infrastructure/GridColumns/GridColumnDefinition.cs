namespace Banco.UI.Wpf.Infrastructure.GridColumns;

public sealed class GridColumnDefinition
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    public required bool IsVisibleByDefault { get; init; }

    public required double DefaultWidth { get; init; }

    public required int DefaultDisplayIndex { get; init; }

    public bool IsNumeric { get; init; }

    public bool IsPresetUnscontrinati { get; init; }
}
