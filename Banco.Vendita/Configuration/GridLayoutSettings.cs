namespace Banco.Vendita.Configuration;

public sealed class GridLayoutSettings
{
    public Dictionary<string, GridColumnLayoutState> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, bool> Flags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, double> SplitterWidths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
