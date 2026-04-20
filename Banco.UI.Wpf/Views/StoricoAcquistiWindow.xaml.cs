using System.Windows;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Views;

public partial class StoricoAcquistiWindow : Window
{
    public StoricoAcquistiWindow(StoricoAcquistiViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.CaricaDocumentiAsync();
    }

    private void DateRangeFilterBar_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
