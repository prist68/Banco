using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Banco.UI.Grid.Core.Grid;

namespace Banco.UI.Avalonia.Controls.Controls;

public sealed partial class BancoDataGrid : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<BancoDataGrid, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IReadOnlyList<BancoGridColumnDefinition>?> ColumnsSourceProperty =
        AvaloniaProperty.Register<BancoDataGrid, IReadOnlyList<BancoGridColumnDefinition>?>(nameof(ColumnsSource));

    public static readonly StyledProperty<string> LayoutKeyProperty =
        AvaloniaProperty.Register<BancoDataGrid, string>(nameof(LayoutKey), string.Empty);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<BancoDataGrid, bool>(nameof(IsReadOnly), true);

    public static readonly StyledProperty<IDataTemplate?> RowDetailsTemplateProperty =
        AvaloniaProperty.Register<BancoDataGrid, IDataTemplate?>(nameof(RowDetailsTemplate));

    public static readonly StyledProperty<DataGridRowDetailsVisibilityMode> RowDetailsVisibilityModeProperty =
        AvaloniaProperty.Register<BancoDataGrid, DataGridRowDetailsVisibilityMode>(
            nameof(RowDetailsVisibilityMode),
            DataGridRowDetailsVisibilityMode.Collapsed);

    private readonly HashSet<string> _hiddenColumns = [];
    private BancoGridDensity _density = BancoGridDensity.Compact;
    private BancoGridColorRole _rowColorRole = BancoGridColorRole.None;
    private BancoGridColorRole _headerColorRole = BancoGridColorRole.None;
    private bool _showGridLines = true;

    public BancoDataGrid()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IReadOnlyList<BancoGridColumnDefinition>? ColumnsSource
    {
        get => GetValue(ColumnsSourceProperty);
        set => SetValue(ColumnsSourceProperty, value);
    }

    public string LayoutKey
    {
        get => GetValue(LayoutKeyProperty);
        set => SetValue(LayoutKeyProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public IDataTemplate? RowDetailsTemplate
    {
        get => GetValue(RowDetailsTemplateProperty);
        set => SetValue(RowDetailsTemplateProperty, value);
    }

    public DataGridRowDetailsVisibilityMode RowDetailsVisibilityMode
    {
        get => GetValue(RowDetailsVisibilityModeProperty);
        set => SetValue(RowDetailsVisibilityModeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColumnsSourceProperty)
        {
            RebuildColumns();
        }
    }

    private void RebuildColumns()
    {
        PART_Grid.Columns.Clear();

        if (ColumnsSource is null)
        {
            return;
        }

        foreach (var definition in ColumnsSource)
        {
            if (!definition.IsVisibleByDefault || _hiddenColumns.Contains(definition.Key))
            {
                continue;
            }

            var column = new DataGridTextColumn
            {
                Header = definition.Header,
                Binding = BuildBinding(definition),
                SortMemberPath = definition.SortMemberPath ?? definition.BindingPath,
                IsReadOnly = definition.IsReadOnly,
                Width = new DataGridLength(definition.Width),
                MinWidth = definition.MinWidth
            };

            if (definition.MaxWidth.HasValue)
            {
                column.MaxWidth = definition.MaxWidth.Value;
            }

            PART_Grid.Columns.Add(column);
        }

        PART_Grid.FrozenColumnCount = ColumnsSource.Count(column => column.IsFrozen && !_hiddenColumns.Contains(column.Key));
        ApplyVisualOptions();
        PART_Grid.ContextMenu = BuildHeaderMenu();
    }

    private static Binding BuildBinding(BancoGridColumnDefinition definition)
    {
        var binding = new Binding(definition.BindingPath ?? definition.Key);
        if (!string.IsNullOrWhiteSpace(definition.Format))
        {
            binding.StringFormat = "{0:" + definition.Format + "}";
        }

        return binding;
    }

    private ContextMenu BuildHeaderMenu()
    {
        var menu = new ContextMenu();
        var columnsMenu = new MenuItem { Header = "Colonne" };

        foreach (var definition in ColumnsSource ?? [])
        {
            var item = new MenuItem
            {
                Header = definition.Header,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = !_hiddenColumns.Contains(definition.Key),
                IsEnabled = definition.CanHide
            };

            item.Click += (_, _) =>
            {
                if (_hiddenColumns.Contains(definition.Key))
                {
                    _hiddenColumns.Remove(definition.Key);
                }
                else
                {
                    _hiddenColumns.Add(definition.Key);
                }

                RebuildColumns();
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

        var detailsMenu = new MenuItem { Header = "Dettagli riga" };
        detailsMenu.Items.Add(BuildRowDetailsItem("Nascosti", DataGridRowDetailsVisibilityMode.Collapsed));
        detailsMenu.Items.Add(BuildRowDetailsItem("Riga selezionata", DataGridRowDetailsVisibilityMode.VisibleWhenSelected));
        detailsMenu.Items.Add(BuildRowDetailsItem("Sempre visibili", DataGridRowDetailsVisibilityMode.Visible));

        var resetItem = new MenuItem { Header = "Ripristina colonne" };
        resetItem.Click += (_, _) =>
        {
            _hiddenColumns.Clear();
            _density = BancoGridDensity.Compact;
            _rowColorRole = BancoGridColorRole.None;
            _headerColorRole = BancoGridColorRole.None;
            _showGridLines = true;
            RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
            RebuildColumns();
        };

        menu.Items.Add(columnsMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(densityMenu);
        menu.Items.Add(rowColorMenu);
        menu.Items.Add(headerColorMenu);
        menu.Items.Add(gridLinesItem);
        menu.Items.Add(detailsMenu);
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

    private MenuItem BuildRowDetailsItem(string header, DataGridRowDetailsVisibilityMode visibilityMode)
    {
        var item = new MenuItem
        {
            Header = header,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = RowDetailsVisibilityMode == visibilityMode
        };
        item.Click += (_, _) =>
        {
            RowDetailsVisibilityMode = visibilityMode;
            PART_Grid.RowDetailsVisibilityMode = visibilityMode;
        };

        return item;
    }

    private void ApplyVisualOptions()
    {
        PART_Grid.RowHeight = _density == BancoGridDensity.Compact ? 32 : 40;
        PART_Grid.GridLinesVisibility = _showGridLines
            ? DataGridGridLinesVisibility.Horizontal
            : DataGridGridLinesVisibility.None;
        PART_Grid.RowBackground = ResolveSoftBrush(_rowColorRole);

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
            PART_Grid.Classes.Add(className);
            return;
        }

        PART_Grid.Classes.Remove(className);
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
