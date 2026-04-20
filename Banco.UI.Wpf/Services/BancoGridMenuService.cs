using System.Windows.Controls;

namespace Banco.UI.Wpf.Services;

/// <summary>
/// Routine condivisa per i menu delle griglie Banco.
/// Centralizza le voci comuni senza duplicare la logica nelle singole view.
/// </summary>
public static class BancoGridMenuService
{
    public static IReadOnlyList<MenuItem> CreateAppearanceMenuItems(string gridKey)
    {
        var coloriMenu = BancoGrigliaColoriService.CreaMenuColoriRighe();
        var headerMenu = BancoGridHeaderColorService.CreaMenu(gridKey);
        return [coloriMenu, headerMenu];
    }

    /// <summary>
    /// Adapter temporaneo per i chiamanti legacy fuori dal perimetro del refactor corrente.
    /// </summary>
    public static void AppendColorMenu(ContextMenu menu, string gridKey)
    {
        var items = CreateAppearanceMenuItems(gridKey);
        if (items.Count == 0)
        {
            return;
        }

        menu.Items.Add(new Separator());
        foreach (var item in items)
        {
            menu.Items.Add(item);
        }
    }
}
