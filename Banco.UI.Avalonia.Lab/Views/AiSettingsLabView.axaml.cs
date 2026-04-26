using Avalonia.Controls;
using Banco.AI.ViewModels;

namespace Banco.UI.Avalonia.Lab.Views;

public sealed partial class AiSettingsLabView : UserControl
{
    public AiSettingsLabView(AiSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
