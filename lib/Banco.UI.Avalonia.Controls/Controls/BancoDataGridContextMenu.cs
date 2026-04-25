using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Banco.UI.Grid.Core.Grid;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed class BancoDataGridContextMenu
{
    private readonly DataGrid _grid;
    private BancoGridDensity _density = BancoGridDensity.Compact;
    private BancoGridColorRole _rowColorRole = BancoGridColorRole.None;
    private BancoGridColorRole _headerColorRole = BancoGridColorRole.None;
    private bool _showGridLines = true;

    private BancoDataGridContextMenu(DataGrid grid)
    {
        _grid = grid;
        _grid.ContextRequested += Grid_OnContextRequested;
        _grid.PointerReleased += Grid_OnPointerReleased;
        ApplyVisualOptions();
    }

    public static BancoDataGridContextMenu Attach(DataGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new BancoDataGridContextMenu(grid);
    }

    private void Grid_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        OpenMenu();
        e.Handled = true;
    }

    private void Grid_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right)
        {
            return;
        }

        OpenMenu();
        e.Handled = true;
    }

    private void OpenMenu()
    {
        var menu = BuildMenu();
        _grid.ContextMenu = menu;
        menu.Open(_grid);
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var columnsMenu = new MenuItem { Header = "Colonne" };

        foreach (var column in _grid.Columns.OrderBy(column => column.DisplayIndex))
        {
            var item = new MenuItem
            {
                Header = ResolveColumnHeader(column),
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = column.IsVisible,
                IsEnabled = _grid.Columns.Count(c => c.IsVisible) > 1 || !column.IsVisible
            };

            item.Click += (_, _) =>
            {
                column.IsVisible = !column.IsVisible;
            };

            columnsMenu.Items.Add(item);
        }

        var densityMenu = new MenuItem { Header = "Densita righe" };
        densityMenu.Items.Add(BuildDensityItem("Compatta", BancoGridDensity.Compact));
        densityMenu.Items.Add(BuildDensityItem("Comoda", BancoGridDensity.Comfortable));

        var rowColorMenu = new MenuItem { Header = "Colorazione righe" };
        rowColorMenu.Items.Add(BuildRowColorItem("Standard", BancoGridColorRole.None));
        rowColorMenu.Items.Add(BuildRowColorItem("Tenue", BancoGridColorRole.Muted));
        rowColorMenu.Items.Add(BuildRowColorItem("Info", BancoGridColorRole.Info));
        rowColorMenu.Items.Add(BuildRowColorItem("Successo", BancoGridColorRole.Success));
        rowColorMenu.Items.Add(BuildRowColorItem("Avviso", BancoGridColorRole.Warning));
        rowColorMenu.Items.Add(BuildRowColorItem("Errore", BancoGridColorRole.Danger));

        var headerColorMenu = new MenuItem { Header = "Colorazione intestazione" };
        headerColorMenu.Items.Add(BuildHeaderColorItem("Standard", BancoGridColorRole.None));
        headerColorMenu.Items.Add(BuildHeaderColorItem("Accent", BancoGridColorRole.Accent));
        headerColorMenu.Items.Add(BuildHeaderColorItem("Info", BancoGridColorRole.Info));
        headerColorMenu.Items.Add(BuildHeaderColorItem("Successo", BancoGridColorRole.Success));
        headerColorMenu.Items.Add(BuildHeaderColorItem("Avviso", BancoGridColorRole.Warning));
        headerColorMenu.Items.Add(BuildHeaderColorItem("Errore", BancoGridColorRole.Danger));

        var gridLinesItem = new MenuItem
        {
            Header = "Linee griglia",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _showGridLines
        };
        gridLinesItem.Click += (_, _) =>
        {
            _showGridLines = !_showGridLines;
            ApplyVisualOptions();
        };

        var resetItem = new MenuItem { Header = "Ripristina layout" };
        resetItem.Click += (_, _) =>
        {
            foreach (var column in _grid.Columns)
            {
                column.IsVisible = true;
            }

            _density = BancoGridDensity.Compact;
            _rowColorRole = BancoGridColorRole.None;
            _headerColorRole = BancoGridColorRole.None;
            _showGridLines = true;
            ApplyVisualOptions();
        };

        menu.Items.Add(columnsMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(densityMenu);
        menu.Items.Add(rowColorMenu);
        menu.Items.Add(headerColorMenu);
        menu.Items.Add(gridLinesItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(resetItem);
        return menu;
    }

    private MenuItem BuildDensityItem(string header, BancoGridDensity density)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = _density == density
        };
        item.Click += (_, _) =>
        {
            _density = density;
            ApplyVisualOptions();
        };

        return item;
    }

    private MenuItem BuildRowColorItem(string header, BancoGridColorRole colorRole)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = _rowColorRole == colorRole
        };
        item.Click += (_, _) =>
        {
            _rowColorRole = colorRole;
            ApplyVisualOptions();
        };

        return item;
    }

    private MenuItem BuildHeaderColorItem(string header, BancoGridColorRole colorRole)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = _headerColorRole == colorRole
        };
        item.Click += (_, _) =>
        {
            _headerColorRole = colorRole;
            ApplyVisualOptions();
        };

        return item;
    }

    private void ApplyVisualOptions()
    {
        _grid.RowHeight = _density == BancoGridDensity.Compact ? 32 : 40;
        _grid.GridLinesVisibility = _showGridLines
            ? DataGridGridLinesVisibility.Horizontal
            : DataGridGridLinesVisibility.None;
        _grid.RowBackground = ResolveSoftBrush(_rowColorRole);

        SetHeaderColorClass("headerAccent", _headerColorRole == BancoGridColorRole.Accent);
        SetHeaderColorClass("headerInfo", _headerColorRole == BancoGridColorRole.Info);
        SetHeaderColorClass("headerSuccess", _headerColorRole == BancoGridColorRole.Success);
        SetHeaderColorClass("headerWarning", _headerColorRole == BancoGridColorRole.Warning);
        SetHeaderColorClass("headerDanger", _headerColorRole == BancoGridColorRole.Danger);
    }

    private void SetHeaderColorClass(string className, bool isEnabled)
    {
        if (isEnabled)
        {
            _grid.Classes.Add(className);
            return;
        }

        _grid.Classes.Remove(className);
    }

    private static string ResolveColumnHeader(DataGridColumn column)
    {
        var header = column.Header?.ToString();
        return string.IsNullOrWhiteSpace(header) ? "(senza titolo)" : header;
    }

    private static IBrush? ResolveSoftBrush(BancoGridColorRole colorRole)
    {
        var color = colorRole switch
        {
            BancoGridColorRole.Info => "#EDF5FF",
            BancoGridColorRole.Success => "#ECF8F2",
            BancoGridColorRole.Warning => "#FFF7E8",
            BancoGridColorRole.Danger => "#FFF0F0",
            BancoGridColorRole.Muted => "#F3F7FC",
            BancoGridColorRole.Accent => "#E6F7F1",
            _ => null
        };

        return color is null ? null : SolidColorBrush.Parse(color);
    }
}
