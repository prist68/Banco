using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Banco.Vendita.Configuration;

namespace Banco.UI.Shared.Grid;

public sealed class DataGridColumnManager
{
    private readonly Func<Task<GridLayoutSettings>> _loadLayoutAsync;
    private readonly Func<GridLayoutSettings, Task> _saveLayoutAsync;
    private readonly Func<string, bool> _isColumnVisible;
    private readonly Func<string, int> _getDisplayIndex;
    private readonly Func<string, double> _getWidth;
    private readonly Func<string, Task> _toggleColumnVisibilityAsync;
    private readonly Func<string, int, Task> _saveDisplayIndexAsync;
    private readonly Func<string, double, Task> _saveWidthAsync;
    private readonly Func<string, GridColumnContentAlignment> _getContentAlignment;
    private readonly Func<string, GridColumnContentAlignment, Task> _saveContentAlignmentAsync;
    private readonly Action _applyVisibility;
    private readonly Action _applyAlignment;
    private readonly Action<int> _applyFrozenColumnCount;
    private readonly bool _supportsTextAlignment;
    private readonly Dictionary<string, DataGridColumn> _columnsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<DataGridColumn, string> _keysByColumn = [];
    private bool _initialized;

    public DataGridColumnManager(
        IEnumerable<GridColumnDefinition> definitions,
        Func<Task<GridLayoutSettings>> loadLayoutAsync,
        Func<GridLayoutSettings, Task> saveLayoutAsync,
        Func<string, bool> isColumnVisible,
        Func<string, int> getDisplayIndex,
        Func<string, double> getWidth,
        Func<string, Task> toggleColumnVisibilityAsync,
        Func<string, int, Task> saveDisplayIndexAsync,
        Func<string, double, Task> saveWidthAsync,
        Action applyVisibility,
        Func<string, GridColumnContentAlignment>? getContentAlignment = null,
        Func<string, GridColumnContentAlignment, Task>? saveContentAlignmentAsync = null,
        Action? applyAlignment = null,
        Action<int>? applyFrozenColumnCount = null)
    {
        Definitions = definitions.ToList();
        _loadLayoutAsync = loadLayoutAsync;
        _saveLayoutAsync = saveLayoutAsync;
        _isColumnVisible = isColumnVisible;
        _getDisplayIndex = getDisplayIndex;
        _getWidth = getWidth;
        _toggleColumnVisibilityAsync = toggleColumnVisibilityAsync;
        _saveDisplayIndexAsync = saveDisplayIndexAsync;
        _saveWidthAsync = saveWidthAsync;
        _getContentAlignment = getContentAlignment ?? (_ => GridColumnContentAlignment.Center);
        _saveContentAlignmentAsync = saveContentAlignmentAsync ?? ((_, _) => Task.CompletedTask);
        _applyVisibility = applyVisibility;
        _applyAlignment = applyAlignment ?? (() => { });
        _applyFrozenColumnCount = applyFrozenColumnCount ?? (_ => { });
        _supportsTextAlignment = getContentAlignment is not null && saveContentAlignmentAsync is not null && applyAlignment is not null;
    }

    public IReadOnlyList<GridColumnDefinition> Definitions { get; }

    public async Task InitializeAsync(IReadOnlyDictionary<string, DataGridColumn> columns)
    {
        if (_initialized)
        {
            return;
        }

        foreach (var pair in columns)
        {
            _columnsByKey[pair.Key] = pair.Value;
            _keysByColumn[pair.Value] = pair.Key;
        }

        var layout = await _loadLayoutAsync();
        var changed = false;

        foreach (var definition in Definitions)
        {
            if (!_columnsByKey.TryGetValue(definition.Key, out var column))
            {
                continue;
            }

            if (!layout.Columns.TryGetValue(definition.Key, out var state))
            {
                state = new GridColumnLayoutState
                {
                    Width = definition.DefaultWidth,
                    DisplayIndex = definition.DefaultDisplayIndex,
                    IsVisible = definition.IsVisibleByDefault,
                    ContentAlignment = definition.DefaultContentAlignment
                };
                layout.Columns[definition.Key] = state;
                changed = true;
            }

            ApplyColumnGuardRails(column, definition);
            column.Width = new DataGridLength(_getWidth(definition.Key));
            column.DisplayIndex = _getDisplayIndex(definition.Key);
            RegisterWidthChange(column, definition.Key);
            RegisterDisplayIndexChange(column, definition.Key);
        }

        _applyVisibility();
        _applyAlignment();
        ApplyFrozenColumns();
        _initialized = true;

        if (changed)
        {
            await _saveLayoutAsync(layout);
        }
    }

    public MenuItem CreateColumnSelectionMenuItem()
    {
        var root = new MenuItem
        {
            Header = "Colonne"
        };

        var groupedDefinitions = Definitions
            .OrderBy(item => string.IsNullOrWhiteSpace(item.Group) ? " " : item.Group, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.DefaultDisplayIndex)
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Group) ? string.Empty : item.Group!);

        foreach (var group in groupedDefinitions)
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                foreach (var definition in group)
                {
                    root.Items.Add(CreateColumnToggleMenuItem(definition));
                }
                continue;
            }

            var groupItem = new MenuItem
            {
                Header = group.Key,
                StaysOpenOnClick = true
            };

            foreach (var definition in group)
            {
                groupItem.Items.Add(CreateColumnToggleMenuItem(definition));
            }

            root.Items.Add(groupItem);
        }

        return root;
    }

    public MenuItem? CreateColumnAlignmentMenuItem(DataGridColumn? column)
    {
        if (!_supportsTextAlignment)
        {
            return null;
        }

        var key = GetColumnKey(column);
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var root = new MenuItem
        {
            Header = "Allinea testo"
        };

        root.Items.Add(CreateAlignmentChoiceMenuItem("Sinistra", key, GridColumnContentAlignment.Left));
        root.Items.Add(CreateAlignmentChoiceMenuItem("Centro", key, GridColumnContentAlignment.Center));
        root.Items.Add(CreateAlignmentChoiceMenuItem("Destra", key, GridColumnContentAlignment.Right));
        return root;
    }

    public IEnumerable<GridColumnDefinition> GetVisibleDefinitionsOrdered()
    {
        return Definitions
            .Where(item => _isColumnVisible(item.Key))
            .OrderBy(item => _getDisplayIndex(item.Key));
    }

    public double GetActualWidth(string key)
    {
        return _columnsByKey.TryGetValue(key, out var column)
            ? column.ActualWidth
            : 0;
    }

    public string? GetColumnKey(DataGridColumn? column)
    {
        if (column is null)
        {
            return null;
        }

        return _keysByColumn.TryGetValue(column, out var key)
            ? key
            : null;
    }

    private MenuItem CreateColumnToggleMenuItem(GridColumnDefinition definition)
    {
        var canToggle = definition.CanHide && !definition.IsLocked;
        var canHideCurrentColumn = !(_isColumnVisible(definition.Key) && GetVisibleHideableColumnsCount() <= 1);
        var item = new MenuItem
        {
            Header = definition.Header,
            IsCheckable = canToggle,
            IsChecked = _isColumnVisible(definition.Key),
            IsEnabled = canToggle && canHideCurrentColumn,
            StaysOpenOnClick = true,
            Tag = definition.Key,
            ToolTip = BuildColumnTooltip(definition)
        };

        if (!canToggle)
        {
            item.InputGestureText = "bloccata";
            return item;
        }

        item.Click += async (_, _) =>
        {
            if (item.Tag is not string key)
            {
                return;
            }

            await _toggleColumnVisibilityAsync(key);
            _applyVisibility();
            ApplyFrozenColumns();
        };

        return item;
    }

    private int GetVisibleHideableColumnsCount()
    {
        return Definitions.Count(definition =>
            definition.CanHide &&
            !definition.IsLocked &&
            _isColumnVisible(definition.Key));
    }

    private static string BuildColumnTooltip(GridColumnDefinition definition)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(definition.Description))
        {
            details.Add(definition.Description);
        }

        if (!string.IsNullOrWhiteSpace(definition.Format))
        {
            details.Add($"Formato: {definition.Format}");
        }

        if (!string.IsNullOrWhiteSpace(definition.PresetKey))
        {
            details.Add($"Preset: {definition.PresetKey}");
        }

        if (definition.IsFrozen)
        {
            details.Add("Colonna predisposta come fissa a sinistra.");
        }

        if (definition.IsLocked || !definition.CanHide)
        {
            details.Add("Colonna protetta.");
        }

        return string.Join(Environment.NewLine, details);
    }

    private MenuItem CreateAlignmentChoiceMenuItem(string header, string key, GridColumnContentAlignment alignment)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = _getContentAlignment(key) == alignment,
            StaysOpenOnClick = true
        };

        item.Click += async (_, _) =>
        {
            await _saveContentAlignmentAsync(key, alignment);
            _applyAlignment();
        };

        return item;
    }

    private void ApplyColumnGuardRails(DataGridColumn column, GridColumnDefinition definition)
    {
        if (definition.MinWidth is > 0)
        {
            column.MinWidth = definition.MinWidth.Value;
        }

        if (definition.MaxWidth is > 0)
        {
            column.MaxWidth = definition.MaxWidth.Value;
        }

        if (definition.IsLocked)
        {
            column.CanUserReorder = false;
            column.CanUserResize = false;
            return;
        }

        column.CanUserReorder = true;
        column.CanUserResize = true;
    }

    private void ApplyFrozenColumns()
    {
        var frozenCount = Definitions
            .Where(definition => _isColumnVisible(definition.Key))
            .OrderBy(definition => _getDisplayIndex(definition.Key))
            .TakeWhile(definition => definition.IsFrozen)
            .Count();

        _applyFrozenColumnCount(frozenCount);
    }

    private void RegisterWidthChange(DataGridColumn column, string key)
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        descriptor?.AddValueChanged(column, async (_, _) =>
        {
            if (!_initialized)
            {
                return;
            }

            await _saveWidthAsync(key, column.ActualWidth);
        });
    }

    private void RegisterDisplayIndexChange(DataGridColumn column, string key)
    {
        var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
        descriptor?.AddValueChanged(column, async (_, _) =>
        {
            if (!_initialized)
            {
                return;
            }

            await _saveDisplayIndexAsync(key, column.DisplayIndex);
        });
    }
}
