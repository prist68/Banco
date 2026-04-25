using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Banco.UI.Controls;

public partial class InlineLookupPicker : UserControl
{
    private Window? _ownerWindow;

    public static readonly RoutedEvent SelectedItemChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(SelectedItemChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(InlineLookupPicker));

    public InlineLookupPicker()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(InlineLookupPicker),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(InlineLookupPicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(InlineLookupPicker),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(InlineLookupPicker),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(InlineLookupPicker),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty SearchPlaceholderTextProperty =
        DependencyProperty.Register(nameof(SearchPlaceholderText), typeof(string), typeof(InlineLookupPicker),
            new PropertyMetadata("Filtra elenco"));

    public static readonly DependencyProperty EmptyTextProperty =
        DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(InlineLookupPicker),
            new PropertyMetadata("Nessun elemento trovato."));

    public static readonly DependencyProperty MaxListHeightProperty =
        DependencyProperty.Register(nameof(MaxListHeight), typeof(double), typeof(InlineLookupPicker),
            new PropertyMetadata(220d));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public event RoutedEventHandler SelectedItemChanged
    {
        add => AddHandler(SelectedItemChangedEvent, value);
        remove => RemoveHandler(SelectedItemChangedEvent, value);
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string SearchPlaceholderText
    {
        get => (string)GetValue(SearchPlaceholderTextProperty);
        set => SetValue(SearchPlaceholderTextProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public double MaxListHeight
    {
        get => (double)GetValue(MaxListHeightProperty);
        set => SetValue(MaxListHeightProperty, value);
    }

    public bool HasItems => EnumerateItems().Any();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateHasItems();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachOwnerWindowHandlers();
    }

    private void ClosedFieldHost_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        IsOpen = !IsOpen;
        e.Handled = true;
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not InlineLookupPicker picker)
        {
            return;
        }

        if (e.NewValue is not null)
        {
            picker.IsOpen = false;
            if (!string.IsNullOrWhiteSpace(picker.SearchText))
            {
                picker.SearchText = string.Empty;
            }
        }

        picker.RaiseEvent(new RoutedEventArgs(SelectedItemChangedEvent, picker));
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not InlineLookupPicker picker || e.NewValue is not bool isOpen)
        {
            return;
        }

        if (isOpen)
        {
            picker.AttachOwnerWindowHandlers();
            picker.Dispatcher.BeginInvoke(picker.FocusOpenedPicker);
            return;
        }

        picker.DetachOwnerWindowHandlers();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not InlineLookupPicker picker)
        {
            return;
        }

        if (e.OldValue is INotifyCollectionChanged oldNotify)
        {
            oldNotify.CollectionChanged -= picker.ItemsSource_CollectionChanged;
        }

        if (e.NewValue is INotifyCollectionChanged newNotify)
        {
            newNotify.CollectionChanged += picker.ItemsSource_CollectionChanged;
        }

        picker.UpdateHasItems();
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateHasItems();
    }

    private void LookupPopup_OnOpened(object sender, EventArgs e)
    {
        AttachOwnerWindowHandlers();
        Dispatcher.BeginInvoke(FocusOpenedPicker);
    }

    private void LookupPopup_OnClosed(object sender, EventArgs e)
    {
        DetachOwnerWindowHandlers();
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    private void ItemsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsListBox.SelectedItem is null)
        {
            return;
        }

        SetCurrentValue(SelectedItemProperty, ItemsListBox.SelectedItem);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            SetCurrentValue(SearchTextProperty, string.Empty);
        }

        IsOpen = false;
    }

    private void UpdateHasItems()
    {
        SetValue(HasItemsPropertyKey, EnumerateItems().Any());
    }

    private IEnumerable<object?> EnumerateItems()
    {
        if (ItemsSource is null)
        {
            yield break;
        }

        foreach (var item in ItemsSource)
        {
            yield return item;
        }
    }

    private void FocusOpenedPicker()
    {
        if (SelectedItem is not null)
        {
            ItemsListBox.SelectedItem = SelectedItem;
            ItemsListBox.ScrollIntoView(SelectedItem);
        }

        FilterTextBox.Focus();
        FilterTextBox.SelectAll();
    }

    private void AttachOwnerWindowHandlers()
    {
        var ownerWindow = Window.GetWindow(this);
        if (ownerWindow is null)
        {
            return;
        }

        if (ReferenceEquals(_ownerWindow, ownerWindow))
        {
            return;
        }

        DetachOwnerWindowHandlers();
        _ownerWindow = ownerWindow;
        _ownerWindow.PreviewMouseDown += OwnerWindow_OnPreviewMouseDown;
        _ownerWindow.Deactivated += OwnerWindow_OnDeactivated;
    }

    private void DetachOwnerWindowHandlers()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        _ownerWindow.PreviewMouseDown -= OwnerWindow_OnPreviewMouseDown;
        _ownerWindow.Deactivated -= OwnerWindow_OnDeactivated;
        _ownerWindow = null;
    }

    private void OwnerWindow_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsOpen)
        {
            return;
        }

        if (IsMouseOver || LookupPopup.IsMouseOver)
        {
            return;
        }

        IsOpen = false;
    }

    private void OwnerWindow_OnDeactivated(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            IsOpen = false;
        }
    }

    private static readonly DependencyPropertyKey HasItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasItems), typeof(bool), typeof(InlineLookupPicker),
            new PropertyMetadata(false));

    public static readonly DependencyProperty HasItemsProperty = HasItemsPropertyKey.DependencyProperty;
}
