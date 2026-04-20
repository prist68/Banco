using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Banco.Vendita.Configuration;

namespace Banco.UI.Wpf.Infrastructure.GridColumns;

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
    private readonly Action _applyVisibility;
    private readonly Dictionary<string, DataGridColumn> _columnsByKey = new(StringComparer.OrdinalIgnoreCase);
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
        Action applyVisibility)
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
        _applyVisibility = applyVisibility;
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
                    IsVisible = definition.IsVisibleByDefault
                };
                layout.Columns[definition.Key] = state;
                changed = true;
            }

            column.Width = new DataGridLength(_getWidth(definition.Key));
            column.DisplayIndex = _getDisplayIndex(definition.Key);
            RegisterWidthChange(column, definition.Key);
            RegisterDisplayIndexChange(column, definition.Key);
        }

        _applyVisibility();
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
            Header = "Seleziona colonne"
        };

        foreach (var definition in Definitions.OrderBy(item => item.DefaultDisplayIndex))
        {
            var item = new MenuItem
            {
                Header = definition.Header,
                IsCheckable = true,
                IsChecked = _isColumnVisible(definition.Key),
                StaysOpenOnClick = true,
                Tag = definition.Key
            };

            item.Click += async (_, _) =>
            {
                if (item.Tag is not string key)
                {
                    return;
                }

                await _toggleColumnVisibilityAsync(key);
                _applyVisibility();
            };

            root.Items.Add(item);
        }

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
