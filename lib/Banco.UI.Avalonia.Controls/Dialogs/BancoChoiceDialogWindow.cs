using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Banco.UI.Grid.Core.Dialogs;

namespace Banco.UI.Avalonia.Controls.Dialogs;

public sealed class BancoChoiceDialogWindow<T> : Window
{
    private T? _selectedValue;

    public BancoChoiceDialogWindow(BancoChoiceRequest<T> request)
    {
        Title = request.Title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel
        {
            Spacing = 12,
            Margin = new global::Avalonia.Thickness(18)
        };

        panel.Children.Add(new TextBlock
        {
            Text = request.Title,
            FontSize = 18,
            FontWeight = FontWeight.Bold
        });

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            panel.Children.Add(new TextBlock
            {
                Text = request.Message,
                TextWrapping = TextWrapping.Wrap
            });
        }

        foreach (var option in request.Options)
        {
            var button = new Button
            {
                Content = string.IsNullOrWhiteSpace(option.Description)
                    ? option.Label
                    : $"{option.Label}\n{option.Description}",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Click += (_, _) =>
            {
                _selectedValue = option.Value;
                Close(true);
            };
            panel.Children.Add(button);
        }

        var cancelButton = new Button
        {
            Content = request.CancelText,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        cancelButton.Click += (_, _) => Close(false);
        panel.Children.Add(cancelButton);

        Content = panel;
    }

    public T? SelectedValue => _selectedValue;
}
