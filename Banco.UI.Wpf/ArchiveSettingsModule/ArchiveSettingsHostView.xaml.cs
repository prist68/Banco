using System.Windows;

namespace Banco.UI.Wpf.ArchiveSettingsModule;

public partial class ArchiveSettingsHostView : System.Windows.Controls.UserControl
{
    public ArchiveSettingsHostView()
    {
        InitializeComponent();
    }

    private void SectionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ArchiveSettingsHostViewModel viewModel)
        {
            return;
        }

        if (sender is not System.Windows.Controls.Button button || button.CommandParameter is not ArchiveSettingsSection section)
        {
            return;
        }

        viewModel.SelectSection(section);
    }
}
