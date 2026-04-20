using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Banco.UI.Wpf.Views;

public sealed class Pos80PrintWindow : Window
{
    private const int OLECMDID_PRINT = 6;
    private const int OLECMDEXECOPT_DONTPROMPTUSER = 2;

    private readonly string _htmlPath;
    private readonly string? _printerName;
    private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly WebBrowser _browser;
    private readonly DispatcherTimer _timeoutTimer;
    private readonly Dictionary<string, string?> _pageSetupBackup = new(StringComparer.OrdinalIgnoreCase);
    private bool _isCompleted;
    private bool _printRequested;
    private string? _previousDefaultPrinter;
    private bool _defaultPrinterChanged;

    private Pos80PrintWindow(Window? owner, string htmlPath, string? printerName)
    {
        Owner = owner;
        _htmlPath = htmlPath;
        _printerName = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim();

        Width = 12;
        Height = 12;
        Left = -10000;
        Top = -10000;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Opacity = 0.01;
        Background = null;

        _browser = new WebBrowser();
        Content = _browser;

        _browser.LoadCompleted += Browser_OnLoadCompleted;
        Loaded += OnLoaded;
        Closed += OnClosed;

        _timeoutTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _timeoutTimer.Tick += TimeoutTimer_OnTick;
    }

    public static async Task<string?> PrintAsync(Window? owner, string htmlPath, string? printerName)
    {
        var window = new Pos80PrintWindow(owner, htmlPath, printerName);
        window.Show();
        return await window._completion.Task;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_htmlPath) || !File.Exists(_htmlPath))
            {
                Complete("Il file di stampa POS80 non esiste o non e` piu` disponibile.");
                return;
            }

            if (!PreparePrinter())
            {
                return;
            }

            _timeoutTimer.Start();
            _browser.Navigate(new Uri(_htmlPath));
        }
        catch (Exception ex)
        {
            Complete($"Errore apertura stampa POS80: {ex.Message}");
        }
    }

    private async void Browser_OnLoadCompleted(object? sender, NavigationEventArgs e)
    {
        if (_printRequested || _isCompleted)
        {
            return;
        }

        _printRequested = true;

        try
        {
            // Piccolo attesa per lasciare completare il rendering del documento HTML locale.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            await Task.Delay(450);

            var activeX = _browser.GetType().InvokeMember(
                "ActiveXInstance",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty,
                binder: null,
                target: _browser,
                args: null);

            if (activeX is null)
            {
                Complete("Il motore WebBrowser interno non ha fornito un'istanza ActiveX valida per la stampa.");
                return;
            }

            activeX.GetType().InvokeMember(
                "ExecWB",
                BindingFlags.InvokeMethod,
                binder: null,
                target: activeX,
                args: [OLECMDID_PRINT, OLECMDEXECOPT_DONTPROMPTUSER, null, null]);

            // La chiamata di stampa e` asincrona lato spooler: aspettiamo il dispatch e poi chiudiamo.
            await Task.Delay(1400);
            Complete(null);
        }
        catch (Exception ex)
        {
            Complete($"Errore stampa POS80: {ex.Message}");
        }
    }

    private void TimeoutTimer_OnTick(object? sender, EventArgs e)
    {
        Complete("Timeout durante l'invio alla stampante POS80.");
    }

    private bool PreparePrinter()
    {
        ApplyIePageSetupOverrides();

        if (string.IsNullOrWhiteSpace(_printerName))
        {
            return true;
        }

        _previousDefaultPrinter = TryGetDefaultPrinter();
        if (string.Equals(_previousDefaultPrinter, _printerName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!SetDefaultPrinter(_printerName))
        {
            var message = Marshal.GetLastWin32Error() is var errorCode && errorCode > 0
                ? $"Impossibile impostare '{_printerName}' come stampante predefinita (Win32 {errorCode})."
                : $"Impossibile impostare '{_printerName}' come stampante predefinita.";
            Complete(message);
            return false;
        }

        _defaultPrinterChanged = true;
        return true;
    }

    private void RestoreDefaultPrinter()
    {
        RestoreIePageSetupOverrides();

        if (!_defaultPrinterChanged || string.IsNullOrWhiteSpace(_previousDefaultPrinter))
        {
            return;
        }

        SetDefaultPrinter(_previousDefaultPrinter);
    }

    private void Complete(string? error)
    {
        if (_isCompleted)
        {
            return;
        }

        _isCompleted = true;
        _timeoutTimer.Stop();
        RestoreDefaultPrinter();

        if (IsLoaded)
        {
            Close();
        }

        _completion.TrySetResult(error);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timeoutTimer.Stop();
        _browser.LoadCompleted -= Browser_OnLoadCompleted;
        Loaded -= OnLoaded;
        Closed -= OnClosed;

        if (!_isCompleted)
        {
            RestoreDefaultPrinter();
            _completion.TrySetResult("Operazione di stampa POS80 interrotta prima del completamento.");
            _isCompleted = true;
        }
    }

    private static string? TryGetDefaultPrinter()
    {
        var capacity = 0;
        GetDefaultPrinter(null, ref capacity);
        if (capacity <= 0)
        {
            return null;
        }

        var buffer = new StringBuilder(capacity);
        return GetDefaultPrinter(buffer, ref capacity) ? buffer.ToString() : null;
    }

    private void ApplyIePageSetupOverrides()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\PageSetup");
        if (key is null)
        {
            return;
        }

        BackupPageSetupValue(key, "header");
        BackupPageSetupValue(key, "footer");
        BackupPageSetupValue(key, "margin_top");
        BackupPageSetupValue(key, "margin_bottom");
        BackupPageSetupValue(key, "margin_left");
        BackupPageSetupValue(key, "margin_right");
        BackupPageSetupValue(key, "Shrink_To_Fit");
        BackupPageSetupValue(key, "Print_Background");

        key.SetValue("header", string.Empty, RegistryValueKind.String);
        key.SetValue("footer", string.Empty, RegistryValueKind.String);
        key.SetValue("margin_top", "0.0", RegistryValueKind.String);
        key.SetValue("margin_bottom", "0.0", RegistryValueKind.String);
        key.SetValue("margin_left", "0.0", RegistryValueKind.String);
        key.SetValue("margin_right", "0.0", RegistryValueKind.String);
        key.SetValue("Shrink_To_Fit", "no", RegistryValueKind.String);
        key.SetValue("Print_Background", "yes", RegistryValueKind.String);
    }

    private void RestoreIePageSetupOverrides()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\PageSetup");
        if (key is null)
        {
            return;
        }

        foreach (var entry in _pageSetupBackup)
        {
            if (entry.Value is null)
            {
                key.DeleteValue(entry.Key, throwOnMissingValue: false);
                continue;
            }

            key.SetValue(entry.Key, entry.Value, RegistryValueKind.String);
        }
    }

    private void BackupPageSetupValue(RegistryKey key, string valueName)
    {
        if (_pageSetupBackup.ContainsKey(valueName))
        {
            return;
        }

        _pageSetupBackup[valueName] = key.GetValue(valueName)?.ToString();
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDefaultPrinter(string pszPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder? pszBuffer, ref int size);
}
