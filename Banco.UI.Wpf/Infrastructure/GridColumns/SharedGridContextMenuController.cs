using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Banco.UI.Wpf.Infrastructure.GridColumns;

public enum SharedGridContextSurface
{
    Header,
    Body,
    Empty
}

public sealed class SharedGridContextActionContext
{
    public required SharedGridContextSurface Surface { get; init; }

    public required DataGrid Grid { get; init; }

    public required IReadOnlyList<object> SelectedItems { get; init; }

    public object? Item { get; init; }

    public DataGridColumn? Column { get; init; }

    public DependencyObject? OriginalSource { get; init; }
}

public sealed class SharedGridContextAction
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    public string? InputGestureText { get; init; }

    public required IReadOnlyList<SharedGridContextSurface> Surfaces { get; init; }

    public bool BeginGroup { get; init; }

    public Func<SharedGridContextActionContext, bool>? IsVisible { get; init; }

    public Func<SharedGridContextActionContext, bool>? IsEnabled { get; init; }

    public required Func<SharedGridContextActionContext, Task> ExecuteAsync { get; init; }
}

public sealed class SharedGridContextMenuOptions
{
    public required DataGrid Grid { get; init; }

    public required string GridKey { get; init; }

    public required DataGridColumnManager ColumnManager { get; init; }

    public required IReadOnlyList<SharedGridContextAction> Actions { get; init; }

    public bool IncludeAppearanceMenuOnHeader { get; init; } = true;

    public bool IncludeAppearanceMenuOnBody { get; init; }

    public Func<DependencyObject?, object?>? ResolveItemOverride { get; init; }

    public bool SyncSelectionOnRightClick { get; init; } = true;
}

/// <summary>
/// Controller condiviso della pipeline menu contestuale per DataGrid.
/// Centralizza click destro, composizione menu e sincronizzazione del contesto UI.
/// </summary>
public sealed class SharedGridContextMenuController : IDisposable
{
    private const string HeaderStyleMarkerKey = "__sharedGridHeaderContextMenuStyle";

    private readonly SharedGridContextMenuOptions _options;
    private readonly ContextMenu _headerMenu;
    private readonly ContextMenu _bodyMenu;
    private bool _isAttached;
    private bool _isDisposed;
    private SharedGridContextActionContext? _pendingContext;
    private Style? _appliedHeaderStyle;

    public SharedGridContextMenuController(SharedGridContextMenuOptions options)
    {
        _options = options;
        _headerMenu = new ContextMenu();
        _bodyMenu = new ContextMenu();
        ApplyMenuTheme(_headerMenu);
        ApplyMenuTheme(_bodyMenu);
        Attach();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Detach();
        _isDisposed = true;
    }

    private void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        _options.Grid.PreviewMouseRightButtonDown += Grid_OnPreviewMouseRightButtonDown;
        _options.Grid.ContextMenuOpening += Grid_OnContextMenuOpening;
        _bodyMenu.Opened += BodyMenu_OnOpened;
        _bodyMenu.Closed += Menu_OnClosed;
        _headerMenu.Opened += HeaderMenu_OnOpened;
        _headerMenu.Closed += Menu_OnClosed;
        _options.Grid.ContextMenu = _bodyMenu;
        ApplyHeaderContextMenuStyle();
        _isAttached = true;
    }

    private void Detach()
    {
        if (!_isAttached)
        {
            return;
        }

        _options.Grid.PreviewMouseRightButtonDown -= Grid_OnPreviewMouseRightButtonDown;
        _options.Grid.ContextMenuOpening -= Grid_OnContextMenuOpening;
        _bodyMenu.Opened -= BodyMenu_OnOpened;
        _bodyMenu.Closed -= Menu_OnClosed;
        _headerMenu.Opened -= HeaderMenu_OnOpened;
        _headerMenu.Closed -= Menu_OnClosed;

        if (ReferenceEquals(_options.Grid.ContextMenu, _bodyMenu))
        {
            _options.Grid.ClearValue(FrameworkElement.ContextMenuProperty);
        }

        if (ReferenceEquals(_options.Grid.ColumnHeaderStyle, _appliedHeaderStyle))
        {
            _options.Grid.ColumnHeaderStyle = _appliedHeaderStyle?.BasedOn;
        }

        _pendingContext = null;
        _isAttached = false;
    }

    private void ApplyHeaderContextMenuStyle()
    {
        if (_appliedHeaderStyle is not null && ReferenceEquals(_options.Grid.ColumnHeaderStyle, _appliedHeaderStyle))
        {
            return;
        }

        var baseStyle = _options.Grid.ColumnHeaderStyle;
        if (baseStyle is not null && baseStyle.Resources.Contains(HeaderStyleMarkerKey))
        {
            _appliedHeaderStyle = baseStyle;
            return;
        }

        var style = baseStyle is null
            ? new Style(typeof(DataGridColumnHeader))
            : new Style(typeof(DataGridColumnHeader), baseStyle);

        style.Resources[HeaderStyleMarkerKey] = true;
        style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, _headerMenu));
        _options.Grid.ColumnHeaderStyle = style;
        _appliedHeaderStyle = style;
    }

    private void Grid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as DependencyObject;
        var surface = ResolveSurface(originalSource);
        var item = ResolveItem(originalSource);
        var column = ResolveColumn(originalSource);

        if (surface == SharedGridContextSurface.Body && item is not null && _options.SyncSelectionOnRightClick)
        {
            SyncSelectionForBody(item, column);
        }

        if (surface == SharedGridContextSurface.Empty)
        {
            _pendingContext = null;
            return;
        }

        _pendingContext = new SharedGridContextActionContext
        {
            Surface = surface,
            Grid = _options.Grid,
            Item = item,
            SelectedItems = CaptureSelectionSnapshot(),
            Column = surface == SharedGridContextSurface.Header ? column : ResolveBodyColumn(originalSource, column),
            OriginalSource = originalSource
        };
    }

    private void Grid_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_pendingContext is null)
        {
            e.Handled = true;
            return;
        }

        switch (_pendingContext.Surface)
        {
            case SharedGridContextSurface.Header:
                RebuildHeaderMenu(_pendingContext);
                e.Handled = _headerMenu.Items.Count == 0;
                break;
            case SharedGridContextSurface.Body:
                RebuildBodyMenu(_pendingContext);
                e.Handled = _bodyMenu.Items.Count == 0;
                break;
            default:
                e.Handled = true;
                break;
        }
    }

    private void HeaderMenu_OnOpened(object? sender, RoutedEventArgs e)
    {
        if (_pendingContext is null || _pendingContext.Surface != SharedGridContextSurface.Header)
        {
            _headerMenu.IsOpen = false;
            return;
        }
    }

    private void BodyMenu_OnOpened(object? sender, RoutedEventArgs e)
    {
        if (_pendingContext is null || _pendingContext.Surface != SharedGridContextSurface.Body)
        {
            _bodyMenu.IsOpen = false;
            return;
        }
    }

    private void Menu_OnClosed(object? sender, RoutedEventArgs e)
    {
        _pendingContext = null;
    }

    private void RebuildHeaderMenu(SharedGridContextActionContext context)
    {
        _headerMenu.Items.Clear();
        _headerMenu.Items.Add(_options.ColumnManager.CreateColumnSelectionMenuItem());

        if (_options.IncludeAppearanceMenuOnHeader)
        {
            AppendMenuItems(_headerMenu, Banco.UI.Wpf.Services.BancoGridMenuService.CreateAppearanceMenuItems(_options.GridKey), prependSeparator: true);
        }

        AppendActionItems(_headerMenu, context, SharedGridContextSurface.Header);
        ApplyMenuTheme(_headerMenu);
    }

    private void RebuildBodyMenu(SharedGridContextActionContext context)
    {
        _bodyMenu.Items.Clear();
        AppendActionItems(_bodyMenu, context, SharedGridContextSurface.Body);

        if (_options.IncludeAppearanceMenuOnBody)
        {
            AppendMenuItems(_bodyMenu, Banco.UI.Wpf.Services.BancoGridMenuService.CreateAppearanceMenuItems(_options.GridKey), prependSeparator: _bodyMenu.Items.Count > 0);
        }

        if (_bodyMenu.Items.Count == 0)
        {
            _bodyMenu.IsOpen = false;
            return;
        }

        ApplyMenuTheme(_bodyMenu);
    }

    private void AppendActionItems(ContextMenu menu, SharedGridContextActionContext context, SharedGridContextSurface surface)
    {
        var actions = _options.Actions
            .Where(action => action.Surfaces.Contains(surface))
            .Where(action => action.IsVisible?.Invoke(context) ?? true)
            .ToList();

        var hasItems = menu.Items.Count > 0;
        foreach (var action in actions)
        {
            if (action.BeginGroup && hasItems)
            {
                menu.Items.Add(new Separator());
            }

            var item = new MenuItem
            {
                Header = action.Header,
                InputGestureText = action.InputGestureText,
                IsEnabled = action.IsEnabled?.Invoke(context) ?? true,
                Tag = action.Key
            };

            item.Click += async (_, _) => await action.ExecuteAsync(context);
            menu.Items.Add(item);
            hasItems = true;
        }
    }

    private static void AppendMenuItems(ContextMenu menu, IReadOnlyList<MenuItem> items, bool prependSeparator)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (prependSeparator)
        {
            menu.Items.Add(new Separator());
        }

        foreach (var item in items)
        {
            menu.Items.Add(item);
        }
    }

    private static void ApplyMenuTheme(ContextMenu menu)
    {
        if (Application.Current.TryFindResource("ThemedContextMenuStyle") is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        if (Application.Current.TryFindResource("ThemedContextMenuItemStyle") is Style menuItemStyle)
        {
            menu.Resources[typeof(MenuItem)] = menuItemStyle;

            foreach (var item in menu.Items)
            {
                switch (item)
                {
                    case MenuItem menuItem:
                        ApplyMenuItemTheme(menuItem, menuItemStyle);
                        break;
                    case Separator separator when Application.Current.TryFindResource("ThemedContextMenuSeparatorStyle") is Style themedSeparatorStyle:
                        separator.Style = themedSeparatorStyle;
                        break;
                }
            }
        }

        if (Application.Current.TryFindResource("ThemedContextMenuSeparatorStyle") is Style separatorStyle)
        {
            menu.Resources[typeof(Separator)] = separatorStyle;
        }
    }

    private static void ApplyMenuItemTheme(MenuItem item, Style menuItemStyle)
    {
        item.Style = menuItemStyle;
        item.Resources[typeof(MenuItem)] = menuItemStyle;

        foreach (var childItem in item.Items.OfType<MenuItem>())
        {
            ApplyMenuItemTheme(childItem, menuItemStyle);
        }
    }

    private IReadOnlyList<object> CaptureSelectionSnapshot()
    {
        return _options.Grid.SelectedItems.Cast<object>().ToList();
    }

    private SharedGridContextSurface ResolveSurface(DependencyObject? originalSource)
    {
        if (originalSource is null)
        {
            return SharedGridContextSurface.Empty;
        }

        if (FindAncestor<DataGridColumnHeader>(originalSource) is not null)
        {
            return SharedGridContextSurface.Header;
        }

        if (FindAncestor<ScrollBar>(originalSource) is not null ||
            FindAncestor<DataGridRowHeader>(originalSource) is not null)
        {
            return SharedGridContextSurface.Empty;
        }

        return FindAncestor<DataGridRow>(originalSource) is not null
            ? SharedGridContextSurface.Body
            : SharedGridContextSurface.Empty;
    }

    private object? ResolveItem(DependencyObject? originalSource)
    {
        if (_options.ResolveItemOverride is not null)
        {
            var resolved = _options.ResolveItemOverride(originalSource);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return FindAncestor<DataGridRow>(originalSource)?.Item;
    }

    private static DataGridColumn? ResolveColumn(DependencyObject? originalSource)
    {
        if (FindAncestor<DataGridCell>(originalSource) is DataGridCell cell)
        {
            return cell.Column;
        }

        if (FindAncestor<DataGridColumnHeader>(originalSource) is DataGridColumnHeader header)
        {
            return header.Column;
        }

        return null;
    }

    private DataGridColumn? ResolveBodyColumn(DependencyObject? originalSource, DataGridColumn? clickedColumn)
    {
        if (clickedColumn is not null && clickedColumn.Visibility == Visibility.Visible)
        {
            return clickedColumn;
        }

        var currentColumn = _options.Grid.CurrentCell.Column;
        if (currentColumn is not null && currentColumn.Visibility == Visibility.Visible)
        {
            return currentColumn;
        }

        return _options.Grid.Columns
            .Where(column => column.Visibility == Visibility.Visible)
            .OrderBy(column => column.DisplayIndex)
            .FirstOrDefault();
    }

    private void SyncSelectionForBody(object item, DataGridColumn? clickedColumn)
    {
        _options.Grid.Focus();
        var isAlreadySelected = _options.Grid.SelectedItems.Contains(item);
        if (!isAlreadySelected)
        {
            _options.Grid.SelectedItems.Clear();
            _options.Grid.SelectedItem = item;
        }
        else
        {
            _options.Grid.SelectedItem = item;
        }

        if (!_options.Grid.SelectedItems.Contains(item))
        {
            _options.Grid.SelectedItems.Add(item);
        }

        var targetColumn = ResolveBodyColumn(null, clickedColumn);
        if (targetColumn is not null)
        {
            _options.Grid.CurrentCell = new DataGridCellInfo(item, targetColumn);
            _options.Grid.ScrollIntoView(item, targetColumn);
        }
        else
        {
            _options.Grid.ScrollIntoView(item);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
