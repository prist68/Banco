using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Banco.UI.Controls;

public partial class CompactGridCommandBar : UserControl
{
    public CompactGridCommandBar()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(CompactGridCommandBar),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NewCommandProperty =
        DependencyProperty.Register(nameof(NewCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(nameof(SaveCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public static readonly DependencyProperty DuplicateCommandProperty =
        DependencyProperty.Register(nameof(DuplicateCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public static readonly DependencyProperty PrintCommandProperty =
        DependencyProperty.Register(nameof(PrintCommand), typeof(ICommand), typeof(CompactGridCommandBar));

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public ICommand? NewCommand
    {
        get => (ICommand?)GetValue(NewCommandProperty);
        set => SetValue(NewCommandProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public ICommand? DuplicateCommand
    {
        get => (ICommand?)GetValue(DuplicateCommandProperty);
        set => SetValue(DuplicateCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public ICommand? PrintCommand
    {
        get => (ICommand?)GetValue(PrintCommandProperty);
        set => SetValue(PrintCommandProperty, value);
    }
}
