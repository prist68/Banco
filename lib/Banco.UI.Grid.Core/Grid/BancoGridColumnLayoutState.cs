namespace Banco.UI.Grid.Core.Grid;

public sealed class BancoGridColumnLayoutState
{
    public required string Key { get; init; }

    public double Width { get; set; }

    public int DisplayIndex { get; set; }

    public bool IsVisible { get; set; }

    public BancoGridAlignment Alignment { get; set; } = BancoGridAlignment.Left;
}
