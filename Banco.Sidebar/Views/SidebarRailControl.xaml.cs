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
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { Tag: string macroCategoryKey })
        {
            return;
        }

        viewModel.OpenContextPanelForMacro(macroCategoryKey);
    }

    private void MacroCategoryButton_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { Tag: string macroCategoryKey })
        {
            return;
        }

        viewModel.OpenContextPanelForMacro(macroCategoryKey);
    }

    private void SearchResultButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { CommandParameter: SidebarSearchResultViewModel result })
        {
            return;
        }

        viewModel.OpenSearchResult(result);
    }
}
