using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Banco.Sidebar.ViewModels;

namespace Banco.Sidebar.Views;

public partial class SidebarRailControl : UserControl
{
    public SidebarRailControl()
    {
        InitializeComponent();
    }

    private void MacroCategoryButton_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { Tag: string macroCategoryKey } button)
        {
            return;
        }

        viewModel.OpenContextPanelForMacro(macroCategoryKey, ResolveContextPanelOffset(button, viewModel, macroCategoryKey));
    }

    private void MacroCategoryButton_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { Tag: string macroCategoryKey } button)
        {
            return;
        }

        viewModel.OpenContextPanelForMacro(macroCategoryKey, ResolveContextPanelOffset(button, viewModel, macroCategoryKey));
    }

    private double ResolveContextPanelOffset(
        FrameworkElement anchorElement,
        SidebarHostViewModel viewModel,
        string macroCategoryKey)
    {
        var position = anchorElement.TranslatePoint(new Point(0, 0), this);
        var requestedTop = Math.Max(14, position.Y - 10);
        var panelHeight = viewModel.EstimateContextPanelHeight(macroCategoryKey);
        var availableHeight = ActualHeight > 0 ? ActualHeight : RenderSize.Height;
        if (availableHeight <= 0)
        {
            return requestedTop;
        }

        var maxTop = Math.Max(14, availableHeight - panelHeight - 14);
        return Math.Min(requestedTop, maxTop);
    }
}
