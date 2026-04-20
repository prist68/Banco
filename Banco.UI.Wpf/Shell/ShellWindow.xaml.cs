using System.ComponentModel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Banco.UI.Wpf.Interactions;
using Banco.UI.Wpf.ViewModels;
using Banco.UI.Wpf.Views;

namespace Banco.UI.Wpf.Shell;

public partial class ShellWindow : Window
{
    private readonly DispatcherTimer _sidebarHoverCloseTimer;
    private bool _isProgrammaticWorkspaceSelection;
    private bool _isEvaluatingBancoExit;
    private bool _isProgrammaticWindowClose;
    private bool _isWindowCloseEvaluationQueued;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Immagini/Banco-32.png", UriKind.Absolute));
        DataContext = viewModel;
        _sidebarHoverCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _sidebarHoverCloseTimer.Tick += SidebarHoverCloseTimer_OnTick;
    }

    private void SidebarHoverHost_OnMouseEnter(object sender, MouseEventArgs e)
    {
        _sidebarHoverCloseTimer.Stop();

        if (DataContext is ShellViewModel shell)
        {
            shell.Sidebar.KeepContextPanelOpen();
        }
    }

    private void SidebarHoverHost_OnMouseLeave(object sender, MouseEventArgs e)
    {
        _sidebarHoverCloseTimer.Stop();
        _sidebarHoverCloseTimer.Start();
    }

    private void SidebarHoverCloseTimer_OnTick(object? sender, EventArgs e)
    {
        _sidebarHoverCloseTimer.Stop();

        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var mousePosition = Mouse.GetPosition(this);
        var isOverRail = IsMouseOverElement(SidebarRailHost, mousePosition);
        var isOverOverlay = SidebarContextOverlayHost.IsVisible && IsMouseOverElement(SidebarContextOverlayHost, mousePosition);
        if (isOverRail || isOverOverlay)
        {
            return;
        }

        shell.Sidebar.CloseContextPanel();
    }

    private void HeaderBrandButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        shell.OpenSettingsWorkspace();
    }

    // Routine tasti funzione globali della shell.
    // I comandi si applicano solo quando la tab attiva e` il Banco.
    private void ShellWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
            return;

        switch (e.Key)
        {
            case Key.F2:
                e.Handled = true;
                _ = HandleNewBancoDocumentAsync(shell);
                return;
        }

        if (shell.ActiveTab?.Content is not BancoViewModel banco)
            return;

        switch (e.Key)
        {
            case Key.F1:
                e.Handled = true;
                banco.ApriStoricoAcquistiArticoloDaTastiera();
                break;

            case Key.F4:
            case Key.F10:
                e.Handled = true;
                _ = BancoSaveInteractionHelper.ExecuteOfficialSaveAsync(banco, this);
                break;

            case Key.F5:
                e.Handled = true;
                _ = HandleCortesiaAsync(banco);
                break;

            case Key.F8:
                e.Handled = true;
                _ = HandleScontrinoAsync(banco);
                break;
        }
    }

    private async Task HandleNewBancoDocumentAsync(ShellViewModel shell)
    {
        var banco = shell.GetPreferredBancoViewModel();

        if (!RequiresNewTabConfirmation(banco))
        {
            banco.NuovoDocumentoCommand.Execute(null);
            await Task.CompletedTask;
            return;
        }

        var dialog = new ConfirmationDialogWindow(
            "Banco / nuova scheda",
            "Vendita corrente con merce",
            "La scheda Banco contiene righe o modifiche correnti non concluse.",
            "Apri seconda scheda",
            "Annulla",
            "Se confermi, la vendita corrente resta aperta nella sua tab e viene creata una nuova scheda Banco vuota.")
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        shell.OpenFreshBancoTab();
        await Task.CompletedTask;
    }

    public async Task RequestNewBancoDocumentAsync()
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        await HandleNewBancoDocumentAsync(shell);
    }

    private static async Task HandleScontrinoAsync(BancoViewModel vm)
    {
        if (!BancoDefaultPaymentInteractionHelper.EnsureDefaultCashPayment(vm, Application.Current.MainWindow, "Scontrino"))
        {
            return;
        }

        if (vm.RichiedeConfermaRistampa)
        {
            var dialog = new ConfirmationDialogWindow(
                "Banco / fiscale",
                "Conferma ristampa",
                "Il documento risulta gia` inviato alla stampa fiscale. Vuoi confermare la richiesta di ristampa?",
                "Conferma ristampa",
                "Annulla",
                "L'operazione prosegue solo dopo conferma esplicita dell'operatore.")
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
                return;

            await vm.EmettiScontrinoAsync(confermaRistampa: true);
        }
        else
        {
            await vm.EmettiScontrinoAsync();
        }
    }

    private static async Task HandleCortesiaAsync(BancoViewModel vm)
    {
        if (!BancoDefaultPaymentInteractionHelper.EnsureDefaultCashPayment(vm, Application.Current.MainWindow, "Cortesia"))
        {
            return;
        }

        await vm.EmettiCortesiaAsync();
    }

    private async void WorkspaceTabCloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_isEvaluatingBancoExit)
        {
            return;
        }

        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        if (sender is not Button element || element.CommandParameter is not ShellWorkspaceTabViewModel tabToClose)
        {
            return;
        }

        _isEvaluatingBancoExit = true;
        try
        {
            if (!await CanLeaveTabAsync(tabToClose, WorkspaceExitScenario.TabClose))
            {
                return;
            }

            shell.CloseWorkspaceTab(tabToClose);
        }
        finally
        {
            _isEvaluatingBancoExit = false;
        }
    }

    private async void WorkspaceTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isProgrammaticWorkspaceSelection || _isEvaluatingBancoExit)
        {
            return;
        }

        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        if (sender is not TabControl tabControl || e.Source != tabControl)
        {
            return;
        }

        var tabOrigine = e.RemovedItems.OfType<ShellWorkspaceTabViewModel>().FirstOrDefault();
        var tabDestinazione = e.AddedItems.OfType<ShellWorkspaceTabViewModel>().FirstOrDefault();

        if (tabOrigine is null || tabDestinazione is null || ReferenceEquals(tabOrigine, tabDestinazione))
        {
            return;
        }

        _isEvaluatingBancoExit = true;
        try
        {
            if (await CanLeaveTabAsync(tabOrigine, WorkspaceExitScenario.TabSwitch))
            {
                return;
            }

            _isProgrammaticWorkspaceSelection = true;
            try
            {
                tabControl.SelectedItem = tabOrigine;
                shell.ActiveTab = tabOrigine;
            }
            finally
            {
                _isProgrammaticWorkspaceSelection = false;
            }
        }
        finally
        {
            _isEvaluatingBancoExit = false;
        }
    }

    private async void ShellWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isProgrammaticWindowClose || _isEvaluatingBancoExit)
        {
            return;
        }

        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        e.Cancel = true;
        if (_isWindowCloseEvaluationQueued)
        {
            return;
        }

        _isWindowCloseEvaluationQueued = true;
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => _ = EvaluateWindowCloseAsync(shell)));
    }

    private bool IsMouseOverElement(FrameworkElement element, Point mousePosition)
    {
        if (!element.IsVisible)
        {
            return false;
        }

        var topLeft = element.TranslatePoint(new Point(0, 0), this);
        var bounds = new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
        return bounds.Contains(mousePosition);
    }

    private async Task EvaluateWindowCloseAsync(ShellViewModel shell)
    {
        _isEvaluatingBancoExit = true;
        try
        {
            var canCloseWindow = await CanCloseApplicationAsync(shell);
            if (!canCloseWindow)
            {
                return;
            }

            _isProgrammaticWindowClose = true;
            Close();
        }
        finally
        {
            _isEvaluatingBancoExit = false;
            _isWindowCloseEvaluationQueued = false;
        }
    }

    private async Task<bool> CanCloseApplicationAsync(ShellViewModel shell)
    {
        if (shell.OpenTabs.Count == 0)
        {
            return true;
        }

        var tabsToEvaluate = shell.OpenTabs
            .OrderByDescending(tab => ReferenceEquals(tab, shell.ActiveTab))
            .ToList();

        foreach (var tab in tabsToEvaluate)
        {
            if (!await CanLeaveTabAsync(tab, WorkspaceExitScenario.ApplicationClose))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> CanLeaveTabAsync(ShellWorkspaceTabViewModel tab, WorkspaceExitScenario scenario)
    {
        var guard = BuildExitGuardContext(tab, scenario);
        if (!guard.RequiresPrompt)
        {
            return true;
        }

        var dialog = new ConfirmationDialogWindow(
            guard.DialogTitle,
            guard.DialogHeader,
            guard.DialogMessage,
            "Esci senza salvare",
            "Annulla",
            guard.DialogHint)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        if (guard.DiscardAction is null)
        {
            return true;
        }

        return await guard.DiscardAction();
    }

    private static ExitGuardContext BuildExitGuardContext(ShellWorkspaceTabViewModel tab, WorkspaceExitScenario scenario)
    {
        if (tab.Content is BancoViewModel banco)
        {
            // Il Banco deve poter restare operativo in background mentre l'utente consulta altre schede.
            if (scenario == WorkspaceExitScenario.TabSwitch)
            {
                return ExitGuardContext.Allow();
            }

            var documento = banco.DocumentoLocaleCorrente;
            if (documento is null)
            {
                return ExitGuardContext.Allow();
            }

            if (banco.IsOfficialConsultationDocument)
            {
                return ExitGuardContext.Allow();
            }

            if (banco.DocumentoAccessMode == BancoDocumentoAccessMode.UfficialeRecuperabile)
            {
                if (!banco.HasPendingLocalChanges)
                {
                    return ExitGuardContext.Allow();
                }

                return ExitGuardContext.Prompt(
                    "Banco / uscita",
                    "Documento ufficiale recuperabile con modifiche correnti",
                    "La scheda e` collegata a un documento ufficiale legacy recuperabile e contiene modifiche correnti non ancora confermate.",
                    scenario == WorkspaceExitScenario.ApplicationClose
                        ? "Se confermi, il documento ufficiale resta invariato sul legacy ma le modifiche correnti andranno perse uscendo dal programma."
                        : "Se confermi, il documento ufficiale resta invariato sul legacy ma le modifiche correnti andranno perse uscendo dalla scheda.",
                    discardAction: null);
            }

            var hasLavoroDaPerdere = documento.Righe.Count > 0;
            if (!hasLavoroDaPerdere)
            {
                return ExitGuardContext.Allow();
            }

            var documentoCorrenteId = documento.Id;
            return ExitGuardContext.Prompt(
                "Banco / uscita",
                "Documento Banco non concluso",
                "Il documento contiene righe o modifiche correnti non concluse.",
                scenario == WorkspaceExitScenario.ApplicationClose
                    ? "Se confermi, il lavoro corrente viene annullato prima di uscire dal programma."
                    : "Se confermi, il lavoro corrente viene annullato e l'uscita dalla pagina prosegue.",
                async () =>
                {
                    await banco.CancellaSchedaAsync();
                    return banco.DocumentoLocaleCorrente?.Id != documentoCorrenteId;
                });
        }

        if (TryGetGenericUnsavedChanges(tab.Content))
        {
            return ExitGuardContext.Prompt(
                $"{tab.DisplayTitle} / uscita",
                "Modifiche non salvate",
                $"La pagina {tab.DisplayTitle} contiene modifiche non salvate.",
                "Se confermi, l'uscita prosegue e le modifiche locali non salvate possono andare perse.",
                discardAction: null);
        }

        return ExitGuardContext.Allow();
    }

    private static bool TryGetGenericUnsavedChanges(object? content)
    {
        if (content is null)
        {
            return false;
        }

        var type = content.GetType();
        return ReadBooleanProperty(type, content, "HasPendingLocalChanges")
               || ReadBooleanProperty(type, content, "HasPendingChanges")
               || ReadBooleanProperty(type, content, "HasUnsavedChanges")
               || ReadBooleanProperty(type, content, "IsDirty");
    }

    private static bool RequiresNewTabConfirmation(BancoViewModel banco)
    {
        var documento = banco.DocumentoLocaleCorrente;
        if (documento is null)
        {
            return false;
        }

        if (banco.IsOfficialConsultationDocument)
        {
            return false;
        }

        if (banco.DocumentoAccessMode == BancoDocumentoAccessMode.UfficialeRecuperabile)
        {
            return banco.HasPendingLocalChanges;
        }

        return documento.Righe.Count > 0;
    }

    private static bool ReadBooleanProperty(Type type, object instance, string propertyName)
    {
        var property = type.GetProperty(propertyName);
        if (property?.PropertyType != typeof(bool))
        {
            return false;
        }

        return property.GetValue(instance) as bool? == true;
    }

    private enum WorkspaceExitScenario
    {
        TabClose,
        TabSwitch,
        ApplicationClose
    }

    private sealed record ExitGuardContext(
        bool RequiresPrompt,
        string DialogTitle,
        string DialogHeader,
        string DialogMessage,
        string DialogHint,
        Func<Task<bool>>? DiscardAction)
    {
        public static ExitGuardContext Allow()
            => new(false, string.Empty, string.Empty, string.Empty, string.Empty, null);

        public static ExitGuardContext Prompt(
            string dialogTitle,
            string dialogHeader,
            string dialogMessage,
            string dialogHint,
            Func<Task<bool>>? discardAction)
            => new(true, dialogTitle, dialogHeader, dialogMessage, dialogHint, discardAction);
    }
}
