using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Banco.UI.Shared.Input;

public static class PosAmountTextBoxBehavior
{
    private sealed class PosAmountState
    {
        public long Centesimi;
        public bool IsUpdating;
        public string LastFormattedText = string.Empty;
    }

    private static readonly ConditionalWeakTable<TextBox, PosAmountState> States = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PosAmountTextBoxBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CultureNameProperty =
        DependencyProperty.RegisterAttached(
            "CultureName",
            typeof(string),
            typeof(PosAmountTextBoxBehavior),
            new PropertyMetadata("it-IT", OnCultureNameChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static string GetCultureName(DependencyObject element) => (string)element.GetValue(CultureNameProperty);

    public static void SetCultureName(DependencyObject element, string value) => element.SetValue(CultureNameProperty, value);

    public static bool TryProcessDigitInput(TextBox textBox, string? text)
    {
        if (!GetIsEnabled(textBox)
            || string.IsNullOrWhiteSpace(text)
            || text.Any(ch => !char.IsDigit(ch)))
        {
            return false;
        }

        var state = GetState(textBox);
        if (state.IsUpdating)
        {
            return false;
        }

        SyncStateFromText(textBox, state);

        foreach (var character in text)
        {
            state.Centesimi = (state.Centesimi * 10) + (character - '0');
        }

        UpdateTextBox(textBox, state);
        return true;
    }

    public static void ResetToZero(TextBox textBox)
    {
        if (!GetIsEnabled(textBox))
        {
            return;
        }

        var state = GetState(textBox);
        if (state.IsUpdating)
        {
            return;
        }

        state.Centesimi = 0;
        UpdateTextBox(textBox, state);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Attach(textBox);
            return;
        }

        Detach(textBox);
    }

    private static void OnCultureNameChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox || !GetIsEnabled(textBox))
        {
            return;
        }

        var state = GetState(textBox);
        SyncStateFromText(textBox, state);
        UpdateTextBox(textBox, state);
    }

    private static void Attach(TextBox textBox)
    {
        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
        textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
        textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        DataObject.RemovePastingHandler(textBox, OnPasting);

        textBox.PreviewTextInput += OnPreviewTextInput;
        textBox.PreviewKeyDown += OnPreviewKeyDown;
        textBox.GotKeyboardFocus += OnGotKeyboardFocus;
        textBox.LostKeyboardFocus += OnLostKeyboardFocus;
        textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        DataObject.AddPastingHandler(textBox, OnPasting);

        var state = GetState(textBox);
        SyncStateFromText(textBox, state);
        UpdateTextBox(textBox, state);
    }

    private static void Detach(TextBox textBox)
    {
        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
        textBox.LostKeyboardFocus -= OnLostKeyboardFocus;
        textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        DataObject.RemovePastingHandler(textBox, OnPasting);
    }

    private static PosAmountState GetState(TextBox textBox) => States.GetValue(textBox, _ => new PosAmountState());

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!TryProcessDigitInput(textBox, e.Text))
        {
            return;
        }

        e.Handled = true;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var state = GetState(textBox);
        if (state.IsUpdating)
        {
            return;
        }

        if (e.Key == Key.Back)
        {
            e.Handled = true;
            SyncStateFromText(textBox, state);
            state.Centesimi /= 10;
            UpdateTextBox(textBox, state);
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            state.Centesimi = 0;
            UpdateTextBox(textBox, state);
            return;
        }

        if (e.Key is Key.Space or Key.OemComma or Key.Decimal or Key.OemPeriod)
        {
            e.Handled = true;
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var state = GetState(textBox);
        SyncStateFromText(textBox, state);
        UpdateTextBox(textBox, state);
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var state = GetState(textBox);
        SyncStateFromText(textBox, state);
        UpdateTextBox(textBox, state);
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.IsKeyboardFocusWithin)
        {
            return;
        }

        e.Handled = true;
        textBox.Focus();
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
    }

    private static void SyncStateFromText(TextBox textBox, PosAmountState state)
    {
        if (state.IsUpdating)
        {
            return;
        }

        var text = textBox.Text ?? string.Empty;
        if (string.Equals(text, state.LastFormattedText, StringComparison.Ordinal))
        {
            return;
        }

        state.Centesimi = ParseCentesimi(text);
    }

    private static long ParseCentesimi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var normalized = NormalizeDecimalInput(value.Replace("€", string.Empty).Trim());
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return 0;
        }

        var bounded = Math.Max(0, parsed);
        return (long)Math.Round(bounded * 100m, 0, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeDecimalInput(string value)
    {
        var compact = value.Replace(" ", string.Empty);
        var hasComma = compact.Contains(',');
        var hasDot = compact.Contains('.');

        if (hasComma && hasDot)
        {
            var lastComma = compact.LastIndexOf(',');
            var lastDot = compact.LastIndexOf('.');

            if (lastComma > lastDot)
            {
                return compact.Replace(".", string.Empty).Replace(',', '.');
            }

            return compact.Replace(",", string.Empty);
        }

        if (hasComma)
        {
            return compact.Replace(',', '.');
        }

        return compact;
    }

    private static void UpdateTextBox(TextBox textBox, PosAmountState state)
    {
        state.IsUpdating = true;

        var culture = ResolveCulture(textBox);
        var value = state.Centesimi / 100m;
        var formatted = value.ToString("N2", culture);

        if (!string.Equals(textBox.Text, formatted, StringComparison.Ordinal))
        {
            textBox.Text = formatted;
        }

        state.LastFormattedText = formatted;
        textBox.CaretIndex = textBox.Text.Length;

        state.IsUpdating = false;
    }

    private static CultureInfo ResolveCulture(TextBox textBox)
    {
        var cultureName = GetCultureName(textBox);

        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return CultureInfo.GetCultureInfo("it-IT");
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("it-IT");
        }
    }
}
