using Avalonia.Controls;

namespace Banco.UI.Avalonia.Controls.Dialogs;

public sealed partial class BancoProgressDialog : Window
{
    public BancoProgressDialog()
    {
        InitializeComponent();
    }

    public void Configure(string title, string message, bool isIndeterminate = true)
    {
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ProgressBar.IsIndeterminate = isIndeterminate;
    }
}
