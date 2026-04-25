using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Banco.Punti.ViewModels;
using Banco.Vendita.Customers;
using Banco.Vendita.Points;

namespace Banco.Punti.Views;

public partial class PuntiView : UserControl
{
    private readonly DispatcherTimer _customerPreviewCloseTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(120)
    };

    public PuntiView()
    {
        InitializeComponent();
        _customerPreviewCloseTimer.Tick += CustomerPreviewCloseTimer_OnTick;
    }

    private void OpenFidelityHistoryWindow_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new FidelityHistoryWindow
        {
            Owner = Window.GetWindow(this),
            DataContext = DataContext
        };

        window.ShowDialog();
    }

    private void CustomerPreviewPopup_OnClosed(object sender, EventArgs e)
    {
        _customerPreviewCloseTimer.Stop();
        if (CustomerPreviewPopup.PlacementTarget is null)
        {
            return;
        }

        CustomerPreviewPopup.PlacementTarget = null;
    }

    private void CustomerResultItem_OnMouseEnter(object sender, MouseEventArgs e)
    {
        _customerPreviewCloseTimer.Stop();

        if (sender is not ListBoxItem itemContainer ||
            itemContainer.DataContext is not GestionaleCustomerSummary customer ||
            DataContext is not PuntiViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedCustomer = customer;
        if (!customer.HaRaccoltaPunti)
        {
            CustomerPreviewPopup.IsOpen = false;
            return;
        }

        CustomerPreviewPopup.PlacementTarget = itemContainer;
        Dispatcher.BeginInvoke(UpdateCustomerPreviewPopup, DispatcherPriority.Loaded);
    }

    private void CustomerResultItem_OnMouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleCustomerPreviewClose();
    }

    private void RewardGridRow_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || !row.IsSelected)
        {
            return;
        }

        if (FindAncestor<DataGridDetailsPresenter>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        var clickedCell = FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        var clickedHeader = FindAncestor<DataGridRowHeader>(e.OriginalSource as DependencyObject);
        if (clickedCell is null && clickedHeader is null)
        {
            return;
        }

        row.IsSelected = false;
        if (ReferenceEquals(RewardRulesGrid.SelectedItem, row.Item))
        {
            RewardRulesGrid.SelectedItem = null;
        }

        e.Handled = true;
    }

    private void RewardRuleSelectionCheckBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox ||
            checkBox.DataContext is not PointsRewardRule rule ||
            DataContext is not PuntiViewModel viewModel)
        {
            return;
        }

        checkBox.IsChecked = viewModel.IsRewardRuleChecked(rule);
    }

    private void RewardRuleSelectionCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        UpdateRewardRuleCheckedState(sender, true);
        e.Handled = true;
    }

    private void RewardRuleSelectionCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
    {
        UpdateRewardRuleCheckedState(sender, false);
        e.Handled = true;
    }

    private void RewardRuleSelectionCheckBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        checkBox.IsChecked = !(checkBox.IsChecked ?? false);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T target)
            {
                return target;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private void UpdateRewardRuleCheckedState(object sender, bool isChecked)
    {
        if (sender is not CheckBox checkBox ||
            checkBox.DataContext is not PointsRewardRule rule ||
            DataContext is not PuntiViewModel viewModel)
        {
            return;
        }

        viewModel.SetRewardRuleChecked(rule, isChecked);
    }

    private void UpdateCustomerPreviewPopup()
    {
        if (CustomerResultsListBox.SelectedItem is not GestionaleCustomerSummary selectedCustomer)
        {
            CustomerPreviewPopup.IsOpen = false;
            return;
        }

        if (!selectedCustomer.HaRaccoltaPunti)
        {
            CustomerPreviewPopup.IsOpen = false;
            return;
        }

        if (CustomerResultsListBox.ItemContainerGenerator.ContainerFromItem(selectedCustomer) is not ListBoxItem itemContainer)
        {
            CustomerPreviewPopup.IsOpen = false;
            return;
        }

        CustomerPreviewPopup.PlacementTarget = itemContainer;
        if (!CustomerPreviewPopup.IsOpen)
        {
            CustomerPreviewPopup.IsOpen = true;
            return;
        }

        // Forza il ricalcolo del placement se la selezione cambia sulla stessa popup aperta.
        CustomerPreviewPopup.HorizontalOffset += 0.1;
        CustomerPreviewPopup.HorizontalOffset -= 0.1;
    }

    private CustomPopupPlacement[] CustomerPreviewPopup_OnCustomPopupPlacement(Size popupSize, Size targetSize, Point offset)
    {
        if (CustomerPreviewPopup.PlacementTarget is not FrameworkElement placementTarget)
        {
            return
            [
                new CustomPopupPlacement(new Point(12, 0), PopupPrimaryAxis.Horizontal)
            ];
        }

        const double gap = 10d;
        const double screenPadding = 8d;

        var targetTopLeft = placementTarget.PointToScreen(new Point(0, 0));
        var workArea = SystemParameters.WorkArea;

        var preferredTop = targetTopLeft.Y + ((targetSize.Height - popupSize.Height) / 2d);
        var clampedTop = Math.Max(workArea.Top + screenPadding, Math.Min(preferredTop, workArea.Bottom - popupSize.Height - screenPadding));
        var yOffset = clampedTop - targetTopLeft.Y;

        var rightScreenX = targetTopLeft.X + targetSize.Width + gap;
        var leftScreenX = targetTopLeft.X - popupSize.Width - gap;

        if (rightScreenX + popupSize.Width <= workArea.Right - screenPadding)
        {
            return
            [
                new CustomPopupPlacement(new Point(targetSize.Width + gap, yOffset), PopupPrimaryAxis.Horizontal)
            ];
        }

        if (leftScreenX >= workArea.Left + screenPadding)
        {
            return
            [
                new CustomPopupPlacement(new Point(-popupSize.Width - gap, yOffset), PopupPrimaryAxis.Horizontal)
            ];
        }

        var fallbackScreenX = Math.Max(workArea.Left + screenPadding, Math.Min(rightScreenX, workArea.Right - popupSize.Width - screenPadding));
        var xOffset = fallbackScreenX - targetTopLeft.X;
        return
        [
            new CustomPopupPlacement(new Point(xOffset, yOffset), PopupPrimaryAxis.Horizontal)
        ];
    }

    private void CustomerPreviewCloseTimer_OnTick(object? sender, EventArgs e)
    {
        _customerPreviewCloseTimer.Stop();

        if (CustomerPreviewPopup.IsMouseOver || CustomerResultsListBox.IsMouseOver)
        {
            return;
        }

        CustomerPreviewPopup.IsOpen = false;
    }

    private void ScheduleCustomerPreviewClose()
    {
        _customerPreviewCloseTimer.Stop();
        _customerPreviewCloseTimer.Start();
    }
}
