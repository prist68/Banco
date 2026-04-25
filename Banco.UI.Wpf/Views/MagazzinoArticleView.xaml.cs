using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GridLength = System.Windows.GridLength;

namespace Banco.UI.Wpf.Views;

public partial class MagazzinoArticleView : UserControl
{
    public static readonly DependencyProperty HideSearchPaneProperty =
        DependencyProperty.Register(
            nameof(HideSearchPane),
            typeof(bool),
            typeof(MagazzinoArticleView),
            new PropertyMetadata(false, OnHideSearchPaneChanged));

    public MagazzinoArticleView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySearchPaneVisibility();
    }

    public bool HideSearchPane
    {
        get => (bool)GetValue(HideSearchPaneProperty);
        set => SetValue(HideSearchPaneProperty, value);
    }

    private static void OnHideSearchPaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MagazzinoArticleView view)
        {
            view.ApplySearchPaneVisibility();
        }
    }

    private void ApplySearchPaneVisibility()
    {
        if (HideSearchPane)
        {
            SearchPaneColumn.Width = new GridLength(0);
            SearchPaneSpacerColumn.Width = new GridLength(0);
            SearchPaneHost.Visibility = Visibility.Collapsed;
            return;
        }

        SearchPaneColumn.Width = new GridLength(300);
        SearchPaneSpacerColumn.Width = new GridLength(12);
        SearchPaneHost.Visibility = Visibility.Visible;
    }

    private void NestedScrollableRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ContentScrollViewer is null)
        {
            return;
        }

        var step = 48d;
        var direction = e.Delta < 0 ? 1d : -1d;
        var targetOffset = Math.Max(0d, ContentScrollViewer.VerticalOffset + (direction * step));

        ContentScrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }
}
