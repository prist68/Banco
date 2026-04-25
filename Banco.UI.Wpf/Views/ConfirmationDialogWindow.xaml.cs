using System.Windows;
using System.Windows.Input;

namespace Banco.UI.Wpf.Views;

public partial class ConfirmationDialogWindow : Window
{
    public ConfirmationDialogWindow(
        string eyebrow,
        string dialogTitle,
        string dialogMessage,
        string primaryButtonText,
        string secondaryButtonText,
        string? highlightText = null)
    {
        InitializeComponent();
        Eyebrow = eyebrow;
        DialogTitle = dialogTitle;
        DialogMessage = dialogMessage;
        PrimaryButtonText = primaryButtonText;
        SecondaryButtonText = secondaryButtonText;
        HighlightText = highlightText ?? string.Empty;
        DataContext = this;
    }

    public string Eyebrow { get; }

    public string DialogTitle { get; }

    public string DialogMessage { get; }

    public string HighlightText { get; }

    public string PrimaryButtonText { get; }

    public string SecondaryButtonText { get; }

    // Il dialog restituisce true solo quando l'operatore conferma esplicitamente.
    private void PrimaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void SecondaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
