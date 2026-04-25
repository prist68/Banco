namespace Banco.UI.Grid.Core.Grid;

public sealed class BancoGridLayoutState
{
    public string LayoutKey { get; init; } = string.Empty;

    public BancoGridDensity Density { get; set; } = BancoGridDensity.Compact;

    public BancoGridColorRole RowColorRole { get; set; } = BancoGridColorRole.None;

    public BancoGridColorRole HeaderColorRole { get; set; } = BancoGridColorRole.None;

    public bool ShowGridLines { get; set; } = true;

    public List<BancoGridColumnLayoutState> Columns { get; } = [];
}
