using System.Windows.Controls;

namespace Banco.UI.Shared.Grid;

/// <summary>
/// Adapter temporaneo per i chiamanti legacy che non sono nel perimetro del refactor corrente.
/// </summary>
public static class DataGridColumnManagerContextMenuCompatExtensions
{
    public static Task PopulateContextMenuAsync(this DataGridColumnManager manager, ContextMenu menu)
    {
        menu.Items.Clear();
        menu.Items.Add(manager.CreateColumnSelectionMenuItem());
        return Task.CompletedTask;
    }
}
