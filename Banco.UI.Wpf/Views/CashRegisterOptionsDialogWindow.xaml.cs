using System.Windows;
using Banco.Core.Domain.Entities;
using Banco.Vendita.Fiscal;

namespace Banco.UI.Wpf.Views;

public partial class CashRegisterOptionsDialogWindow : Window
{
    private readonly DocumentoLocale? _currentDocument;
    private readonly string _defaultReceiptPrefix;

    public CashRegisterOptionsDialogWindow(
        DocumentoLocale? currentDocument = null,
        string? defaultMachineId = null,
        string? defaultReceiptPrefix = null)
    {
        _currentDocument = currentDocument;
        _defaultReceiptPrefix = string.IsNullOrWhiteSpace(defaultReceiptPrefix) ? "1959" : defaultReceiptPrefix.Trim();
        InitializeComponent();
        InitializeReceiptDefaults();
        Loaded += (_, _) =>
        {
            if (ReceiptDocumentPrefixTextBox.Text == "1959")
            {
                ReceiptNumberTextBox.Focus();
                ReceiptNumberTextBox.SelectAll();
            }
            else
            {
                ReceiptDocumentPrefixTextBox.Focus();
                ReceiptDocumentPrefixTextBox.SelectAll();
            }
        };
        if (!string.IsNullOrWhiteSpace(defaultMachineId))
        {
            ReceiptMachineIdTextBox.Text = defaultMachineId.Trim();
        }
    }

    public CashRegisterOptionSelection? Selection { get; private set; }

    private void PrintDailyJournalButton_OnClick(object sender, RoutedEventArgs e)
    {
        Selection = new CashRegisterOptionSelection
        {
            Action = CashRegisterOptionAction.DailyJournal,
            JournalMode = ResolveSelectedJournalMode()
        };
        DialogResult = true;
    }

    private void CloseCashAndTransmitButton_OnClick(object sender, RoutedEventArgs e)
    {
        Selection = new CashRegisterOptionSelection
        {
            Action = CashRegisterOptionAction.CloseCashAndTransmit,
            JournalMode = ResolveSelectedJournalMode()
        };
        DialogResult = true;
    }

    private void ReceiptReprintButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryBuildReceiptSelection(CashRegisterOptionAction.ReceiptReprint, out var selection))
        {
            return;
        }

        Selection = selection;
        DialogResult = true;
    }

    private void ReceiptCancellationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentDocument is null ||
            _currentDocument.Righe.Count == 0)
        {
            ShowValidation("L'annullo protocollo richiede la scheda corrente con le righe del documento da annullare.");
            return;
        }

        if (!TryBuildReceiptSelection(CashRegisterOptionAction.ReceiptCancellation, out var selection))
        {
            return;
        }

        Selection = selection;
        DialogResult = true;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Selection = null;
        DialogResult = false;
    }

    private CashJournalMode ResolveSelectedJournalMode()
    {
        if (LongJournalRadioButton.IsChecked == true)
        {
            return CashJournalMode.Long;
        }

        if (MediumJournalRadioButton.IsChecked == true)
        {
            return CashJournalMode.Medium;
        }

        return CashJournalMode.Short;
    }

    private void InitializeReceiptDefaults()
    {
        var receiptDate = _currentDocument?.DataDocumentoGestionale ?? DateTime.Today;
        ReceiptDatePicker.SelectedDate = receiptDate;

        ReceiptDocumentPrefixTextBox.Text = _defaultReceiptPrefix;
        ReceiptNumberTextBox.Text = string.Empty;

        ReceiptMachineIdTextBox.Text = "ND";
        ReceiptCancellationButton.IsEnabled = true;
    }

    private bool TryBuildReceiptSelection(
        CashRegisterOptionAction action,
        out CashRegisterOptionSelection? selection)
    {
        selection = null;
        ClearValidation();

        var receiptPrefix = ReceiptDocumentPrefixTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(receiptPrefix))
        {
            ShowValidation("Inserisci il primo blocco del documento fiscale.");
            return false;
        }

        if (receiptPrefix.Length > 4 || !receiptPrefix.All(char.IsDigit))
        {
            ShowValidation("Il primo blocco del documento deve essere numerico e lungo fino a 4 cifre.");
            return false;
        }

        var receiptNumberText = ReceiptNumberTextBox.Text.Trim();
        if (receiptNumberText.Length == 0)
        {
            ShowValidation("Inserisci le ultime 4 cifre del numero scontrino.");
            return false;
        }

        if (!int.TryParse(receiptNumberText, out var receiptNumber) || receiptNumber < 0)
        {
            ShowValidation("Inserisci un numero scontrino valido.");
            return false;
        }

        if (receiptNumberText.Length > 4)
        {
            ShowValidation("Le ultime 4 cifre del numero scontrino non possono superare 4 caratteri.");
            return false;
        }

        var receiptDate = ReceiptDatePicker.SelectedDate;
        if (!receiptDate.HasValue)
        {
            ShowValidation("Seleziona la data dello scontrino.");
            return false;
        }

        var machineId = string.IsNullOrWhiteSpace(ReceiptMachineIdTextBox.Text)
            ? "ND"
            : ReceiptMachineIdTextBox.Text.Trim();

        selection = new CashRegisterOptionSelection
        {
            Action = action,
            JournalMode = ResolveSelectedJournalMode(),
            ReceiptDocumentPrefix = receiptPrefix,
            ReceiptNumber = receiptNumber,
            ReceiptDate = receiptDate.Value.Date,
            ReceiptMachineId = machineId
        };

        return true;
    }

    private void ReceiptDatePicker_OnSelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ClearValidation();
    }

    private void ReceiptFields_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ClearValidation();
    }

    private void ShowValidation(string message)
    {
        ValidationMessageTextBlock.Text = message;
        ValidationMessageTextBlock.Visibility = Visibility.Visible;
    }

    private void ClearValidation()
    {
        ValidationMessageTextBlock.Text = string.Empty;
        ValidationMessageTextBlock.Visibility = Visibility.Collapsed;
    }
}
