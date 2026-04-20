using System.Windows;
using System.Windows.Controls;
using Banco.Sidebar.ViewModels;

namespace Banco.Sidebar.Views;

public partial class SidebarCustomizationWindow : Window
{
    public SidebarCustomizationWindow()
    {
        InitializeComponent();
    }

    private void MacroMoveUp_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { CommandParameter: SidebarMacroCategoryViewModel macroCategory })
        {
            return;
        }

        viewModel.MoveMacroCategory(macroCategory, -1);
    }

    private void MacroMoveDown_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not Button { CommandParameter: SidebarMacroCategoryViewModel macroCategory })
        {
            return;
        }

        viewModel.MoveMacroCategory(macroCategory, 1);
    }

    private void MacroAccentCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SidebarHostViewModel viewModel || sender is not ComboBox { Tag: SidebarMacroCategoryViewModel macroCategory, SelectedValue: string accentColor })
        {
            return;
        }

        viewModel.UpdateMacroAccent(macroCategory.Key, accentColor);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
