using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Banco.Core.Contracts.Navigation;
using Banco.Sidebar.ViewModels;

namespace Banco.Sidebar.Views;

public partial class SidebarContextPanelControl : UserControl
{
    private Point _dragStartPoint;
    private bool _suppressNextClick;

    public SidebarContextPanelControl()
    {
        InitializeComponent();
    }

    private void ShortcutButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
    }

    private void ShortcutButton_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not Button button || button.DataContext is not SidebarShortcutItemViewModel item)
        {
            return;
        }

        if (!item.IsEnabled || item.IsInformational || string.IsNullOrWhiteSpace(item.DestinationKey))
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        if (Math.Abs(currentPoint.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPoint.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(ShellDragDropFormats.DesktopShortcutEntryKey, item.EntryKey);
        _suppressNextClick = true;
        DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
    }

    private void ShortcutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_suppressNextClick)
        {
            return;
        }

        _suppressNextClick = false;
        e.Handled = true;
    }
}
