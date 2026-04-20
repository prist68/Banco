using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.Views;

public partial class DateRangeFilterBar : UserControl
{
    private DateFilterViewModelBase? _attachedViewModel;
    private bool _isSyncingExternalBindings;

    public static readonly DependencyProperty StartDateProperty =
        DependencyProperty.Register(
            nameof(StartDate),
            typeof(DateTime?),
            typeof(DateRangeFilterBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExternalDateChanged));

    public static readonly DependencyProperty EndDateProperty =
        DependencyProperty.Register(
            nameof(EndDate),
            typeof(DateTime?),
            typeof(DateRangeFilterBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExternalDateChanged));

    public static readonly DependencyProperty SelectedMonthProperty =
        DependencyProperty.Register(
            nameof(SelectedMonth),
            typeof(int?),
            typeof(DateRangeFilterBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExternalMonthYearChanged));

    public static readonly DependencyProperty SelectedYearProperty =
        DependencyProperty.Register(
            nameof(SelectedYear),
            typeof(int?),
            typeof(DateRangeFilterBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnExternalMonthYearChanged));

    public static readonly DependencyProperty AvailableYearsSourceProperty =
        DependencyProperty.Register(
            nameof(AvailableYearsSource),
            typeof(IEnumerable<int>),
            typeof(DateRangeFilterBar),
            new PropertyMetadata(null, OnAvailableYearsSourceChanged));

    public static readonly DependencyProperty ShowMonthSelectorProperty =
        DependencyProperty.Register(
            nameof(ShowMonthSelector),
            typeof(bool),
            typeof(DateRangeFilterBar),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowYearSelectorProperty =
        DependencyProperty.Register(
            nameof(ShowYearSelector),
            typeof(bool),
            typeof(DateRangeFilterBar),
            new PropertyMetadata(true));

    public DateRangeFilterBar()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SyncAvailableYears();
            SyncDateBoxes();
            SyncMonthYearControls();
        };
        Unloaded += (_, _) => DetachViewModel();
    }

    public DateTime? StartDate
    {
        get => (DateTime?)GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    public DateTime? EndDate
    {
        get => (DateTime?)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    public int? SelectedMonth
    {
        get => (int?)GetValue(SelectedMonthProperty);
        set => SetValue(SelectedMonthProperty, value);
    }

    public int? SelectedYear
    {
        get => (int?)GetValue(SelectedYearProperty);
        set => SetValue(SelectedYearProperty, value);
    }

    public IEnumerable<int>? AvailableYearsSource
    {
        get => (IEnumerable<int>?)GetValue(AvailableYearsSourceProperty);
        set => SetValue(AvailableYearsSourceProperty, value);
    }

    public bool ShowMonthSelector
    {
        get => (bool)GetValue(ShowMonthSelectorProperty);
        set => SetValue(ShowMonthSelectorProperty, value);
    }

    public bool ShowYearSelector
    {
        get => (bool)GetValue(ShowYearSelectorProperty);
        set => SetValue(ShowYearSelectorProperty, value);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();

        if (e.NewValue is DateFilterViewModelBase viewModel)
        {
            _attachedViewModel = viewModel;
            _attachedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            SyncDateBoxes();
            SyncMonthYearControls();
            SyncAvailableYears();
            return;
        }

        SyncAvailableYears();
        SyncDateBoxes();
        SyncMonthYearControls();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DateFilterViewModelBase.DataInizio)
            or nameof(DateFilterViewModelBase.DataFine))
        {
            Dispatcher.Invoke(SyncDateBoxes);
        }

        if (e.PropertyName is nameof(DateFilterViewModelBase.MeseSelezionato)
            or nameof(DateFilterViewModelBase.AnnoSelezionato))
        {
            Dispatcher.Invoke(SyncMonthYearControls);
        }
    }

    private void DateBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var carattere in e.Text)
        {
            if (!char.IsDigit(carattere) && carattere != '/')
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void DateBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitDateBox(textBox);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Back or Key.Delete or Key.Tab or Key.Left or Key.Right)
        {
            return;
        }

        var text = textBox.Text;
        var caret = textBox.CaretIndex;
        if ((caret == 2 || caret == 5) && text.Length == caret && !text.EndsWith('/'))
        {
            textBox.Text = text + "/";
            textBox.CaretIndex = textBox.Text.Length;
        }
    }

    private void DataInizioBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CommitDateBox(textBox);
        }
    }

    private void DataFineBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CommitDateBox(textBox);
        }
    }

    private void DataInizioCalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        DataInizioCalendar.SelectedDate = ResolveStartDate() ?? DateTime.Today;
        DataInizioPopup.IsOpen = true;
    }

    private void DataFineCalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        DataFineCalendar.SelectedDate = ResolveEndDate() ?? DateTime.Today;
        DataFinePopup.IsOpen = true;
    }

    private void DataInizioCalendar_OnSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!DataInizioCalendar.SelectedDate.HasValue)
        {
            return;
        }

        UpdateStartDate(DataInizioCalendar.SelectedDate.Value.Date);
        DataInizioPopup.IsOpen = false;
    }

    private void DataFineCalendar_OnSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!DataFineCalendar.SelectedDate.HasValue)
        {
            return;
        }

        UpdateEndDate(DataFineCalendar.SelectedDate.Value.Date);
        DataFinePopup.IsOpen = false;
    }

    private void CommitDateBox(TextBox textBox)
    {
        var isDataInizio = ReferenceEquals(textBox, DataInizioBox);
        var valoreCorrente = isDataInizio ? ResolveStartDate() : ResolveEndDate();

        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            if (isDataInizio)
            {
                UpdateStartDate(null);
            }
            else
            {
                UpdateEndDate(null);
            }

            SyncDateBoxes();
            return;
        }

        if (DateTime.TryParseExact(textBox.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            if (isDataInizio)
            {
                UpdateStartDate(parsed.Date);
            }
            else
            {
                UpdateEndDate(parsed.Date);
            }

            SyncDateBoxes();
            return;
        }

        textBox.Text = valoreCorrente?.ToString("dd/MM/yyyy") ?? string.Empty;
    }

    private void SyncDateBoxes()
    {
        if (DataInizioBox is null || DataFineBox is null)
        {
            return;
        }

        DataInizioBox.Text = ResolveStartDate()?.ToString("dd/MM/yyyy") ?? string.Empty;
        DataFineBox.Text = ResolveEndDate()?.ToString("dd/MM/yyyy") ?? string.Empty;
    }

    private void SyncMonthYearControls()
    {
        if (_attachedViewModel is not null)
        {
            MeseSelezionatoGuarded(_attachedViewModel.MeseSelezionato, _attachedViewModel.AnnoSelezionato);
            return;
        }

        MeseSelezionatoGuarded(SelectedMonth, SelectedYear);
    }

    private void MeseSelezionatoGuarded(int? mese, int? anno)
    {
        if (MonthCombo is null || YearCombo is null)
        {
            return;
        }

        _isSyncingExternalBindings = true;
        try
        {
            MonthCombo.SelectedValue = mese;
            YearCombo.SelectedItem = anno;
        }
        finally
        {
            _isSyncingExternalBindings = false;
        }
    }

    private void SyncAvailableYears()
    {
        if (YearCombo is null)
        {
            return;
        }

        YearCombo.ItemsSource = _attachedViewModel?.AnniDisponibili ?? AvailableYearsSource;
    }

    private DateTime? ResolveStartDate() => _attachedViewModel?.DataInizio ?? StartDate;

    private DateTime? ResolveEndDate() => _attachedViewModel?.DataFine ?? EndDate;

    private void UpdateStartDate(DateTime? value)
    {
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.DataInizio = value;
            return;
        }

        StartDate = value;
    }

    private void UpdateEndDate(DateTime? value)
    {
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.DataFine = value;
            return;
        }

        EndDate = value;
    }

    private void UpdateMonthYear(int? month, int? year)
    {
        if (_isSyncingExternalBindings)
        {
            return;
        }

        if (_attachedViewModel is not null)
        {
            _attachedViewModel.MeseSelezionato = month;
            _attachedViewModel.AnnoSelezionato = year;
            return;
        }

        SelectedMonth = month;
        SelectedYear = year;
    }

    private void MonthCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MonthCombo is null || YearCombo is null)
        {
            return;
        }

        UpdateMonthYear(MonthCombo.SelectedValue as int?, YearCombo.SelectedItem as int?);
    }

    private void YearCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MonthCombo is null || YearCombo is null)
        {
            return;
        }

        UpdateMonthYear(MonthCombo.SelectedValue as int?, YearCombo.SelectedItem as int?);
    }

    private static void OnExternalDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DateRangeFilterBar control || control._attachedViewModel is not null)
        {
            return;
        }

        control.SyncDateBoxes();
    }

    private static void OnExternalMonthYearChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DateRangeFilterBar control || control._attachedViewModel is not null)
        {
            return;
        }

        control.SyncMonthYearControls();
    }

    private static void OnAvailableYearsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DateRangeFilterBar control || control._attachedViewModel is not null)
        {
            return;
        }

        control.SyncAvailableYears();
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel is null)
        {
            return;
        }

        _attachedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _attachedViewModel = null;
    }
}
