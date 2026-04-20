using System.Windows;
using System.Windows.Controls;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Views;

public partial class UIDocumentListView : UserControl
{
    public UIDocumentListView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncInnerDataContext();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncInnerDataContext();
    }

    private void SyncInnerDataContext()
    {
        if (DocumentListControl is null)
        {
            return;
        }

        DocumentListControl.DataContext = DataContext is UIDocumentListHostViewModel host
            ? host.DocumentListViewModel
            : DataContext;
    }

    private void DocumentListControl_Loaded(object sender, RoutedEventArgs e)
    {

    }
}
