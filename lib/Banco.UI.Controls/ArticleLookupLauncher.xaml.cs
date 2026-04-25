using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Banco.UI.Controls;

public partial class ArticleLookupLauncher : UserControl
{
    public ArticleLookupLauncher()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(ArticleLookupLauncher),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty DescriptionTextProperty =
        DependencyProperty.Register(
            nameof(DescriptionText),
            typeof(string),
            typeof(ArticleLookupLauncher),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SearchLabelProperty =
        DependencyProperty.Register(
            nameof(SearchLabel),
            typeof(string),
            typeof(ArticleLookupLauncher),
            new PropertyMetadata("Codice / barcode"));

    public static readonly DependencyProperty DescriptionLabelProperty =
        DependencyProperty.Register(
            nameof(DescriptionLabel),
            typeof(string),
            typeof(ArticleLookupLauncher),
            new PropertyMetadata("Descrizione articolo"));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string DescriptionText
    {
        get => (string)GetValue(DescriptionTextProperty);
        set => SetValue(DescriptionTextProperty, value);
    }

    public string SearchLabel
    {
        get => (string)GetValue(SearchLabelProperty);
        set => SetValue(SearchLabelProperty, value);
    }

    public string DescriptionLabel
    {
        get => (string)GetValue(DescriptionLabelProperty);
        set => SetValue(DescriptionLabelProperty, value);
    }

    public bool IsSearchKeyboardFocusWithin => SearchTextBox.IsKeyboardFocusWithin;

    public event TextChangedEventHandler? SearchTextChanged;
    public event KeyEventHandler? SearchKeyDown;
    public event TextCompositionEventHandler? SearchPreviewTextInput;
    public event KeyboardFocusChangedEventHandler? SearchLostKeyboardFocus;

    public void FocusSearchBox()
    {
        SearchTextBox.Focus();
    }

    public void SelectAllSearchText()
    {
        SearchTextBox.SelectAll();
    }

    private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchTextChanged?.Invoke(this, e);
    }

    private void SearchTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        SearchKeyDown?.Invoke(this, e);
    }

    private void SearchTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        SearchPreviewTextInput?.Invoke(this, e);
    }

    private void SearchTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchLostKeyboardFocus?.Invoke(this, e);
    }
}
