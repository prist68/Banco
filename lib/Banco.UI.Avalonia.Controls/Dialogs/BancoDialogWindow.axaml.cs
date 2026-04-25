using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Banco.UI.Avalonia.Controls.Dialogs;

public sealed partial class BancoDialogWindow : Window
{
    public BancoDialogWindow()
    {
        InitializeComponent();
    }

    public void Configure(
        string title,
        string message,
        string primaryText,
        string? secondaryText = null)
    {
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        PrimaryButton.Content = primaryText;

        if (string.IsNullOrWhiteSpace(secondaryText))
        {
            SecondaryButton.IsVisible = false;
        }
        else
        {
            SecondaryButton.Content = secondaryText;
            SecondaryButton.IsVisible = true;
        }
    }

    private void PrimaryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void SecondaryButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
