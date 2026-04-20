using System.IO.Compression;
using System.IO;
using System.Windows;
using Banco.UI.Wpf.Services;
using Banco.Vendita.Abstractions;

namespace Banco.UI.Wpf.ViewModels;

public sealed class BackupImportViewModel : ViewModelBase
{
    private readonly IGestionaleBackupImportService _backupImportService;
    private readonly BackupImportDialogService _dialogService;
    private readonly IPosProcessLogService _logService;
    private string _backupFilePath = string.Empty;
    private string _backupSummary = "Nessun backup selezionato.";
    private string _statusMessage = "Seleziona un backup `.zip`, `.bak` o `.sql` per riallineare il db_diltech locale.";
    private string _progressStage = "Pronto";
    private string _progressDetail = "Nessuna importazione in corso.";
    private double _progressPercent;
    private bool _isImportInProgress;
    private bool _hasError;

    public BackupImportViewModel(
        IGestionaleBackupImportService backupImportService,
        BackupImportDialogService dialogService,
        IPosProcessLogService logService)
    {
        _backupImportService = backupImportService;
        _dialogService = dialogService;
        _logService = logService;

        BrowseBackupCommand = new RelayCommand(SelectBackupFile, () => !IsImportInProgress);
        ImportBackupCommand = new RelayCommand(async () => await ImportBackupAsync(), CanImportBackup);
    }

    public string Titolo => "Importa backup gestionale";

    public string BackupFilePath
    {
        get => _backupFilePath;
        private set => SetProperty(ref _backupFilePath, value);
    }

    public string BackupSummary
    {
        get => _backupSummary;
        private set => SetProperty(ref _backupSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ProgressStage
    {
        get => _progressStage;
        private set => SetProperty(ref _progressStage, value);
    }

    public string ProgressDetail
    {
        get => _progressDetail;
        private set => SetProperty(ref _progressDetail, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool IsImportInProgress
    {
        get => _isImportInProgress;
        private set
        {
            if (SetProperty(ref _isImportInProgress, value))
            {
                BrowseBackupCommand.RaiseCanExecuteChanged();
                ImportBackupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand BrowseBackupCommand { get; }

    public RelayCommand ImportBackupCommand { get; }

    private bool CanImportBackup() => !IsImportInProgress && File.Exists(BackupFilePath);

    private void SelectBackupFile()
    {
        var selectedPath = _dialogService.SelectBackupFilePath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        BackupFilePath = selectedPath;
        BackupSummary = BuildBackupSummary(selectedPath);
        StatusMessage = "Backup selezionato. Procedi solo dopo aver chiuso i programmi che usano il DB.";
        ProgressStage = "Pronto";
        ProgressDetail = "Backup caricato. In attesa di conferma import.";
        ProgressPercent = 0;
        HasError = false;
        ImportBackupCommand.RaiseCanExecuteChanged();
        _logService.Info(nameof(BackupImportViewModel), $"Backup selezionato per import: {selectedPath}.");
    }

    private async Task ImportBackupAsync()
    {
        if (!CanImportBackup())
        {
            return;
        }

        var result = MessageBox.Show(
            "L'importazione sovrascrive il contenuto del db_diltech configurato in questa postazione.\n\nChiudi Banco e Facile Manager sulle altre postazioni prima di procedere.\n\nContinuare?",
            "Importa backup gestionale",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            _logService.Warning(nameof(BackupImportViewModel), "Import backup annullato dall'utente prima dell'avvio.");
            return;
        }

        IsImportInProgress = true;
        HasError = false;
        ProgressStage = "Preparazione";
        ProgressDetail = "Verifica iniziale del backup...";
        ProgressPercent = 0;
        StatusMessage = "Importazione backup in corso...";

        try
        {
            _logService.Info(nameof(BackupImportViewModel), $"Import backup avviato da {BackupFilePath}.");
            var progress = new Progress<GestionaleBackupImportProgress>(OnImportProgress);
            var importResult = await _backupImportService.ImportAsync(BackupFilePath, progress);
            BackupSummary = $"{BackupSummary}\nRipristino completato su '{importResult.DatabaseName}' con {importResult.ExecutedStatements:N0} statement eseguiti.";
            StatusMessage = "Importazione completata. Riavvia Banco prima di ripetere i test sul DB riallineato.";
            ProgressStage = "Completato";
            ProgressDetail = $"Restore concluso correttamente su {importResult.DatabaseName}.";
            ProgressPercent = 100;
            _logService.Info(nameof(BackupImportViewModel), $"Import backup completato. Database={importResult.DatabaseName}, Statements={importResult.ExecutedStatements}.");
        }
        catch (Exception ex)
        {
            HasError = true;
            ProgressStage = "Errore";
            ProgressDetail = "L'importazione si e` fermata prima del completamento.";
            StatusMessage = $"Errore import backup: {ex.Message}";
            _logService.Error(nameof(BackupImportViewModel), $"Errore durante l'import backup da {BackupFilePath}.", ex);
        }
        finally
        {
            IsImportInProgress = false;
        }
    }

    private static string BuildBackupSummary(string filePath)
    {
        var info = new FileInfo(filePath);
        if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();
            var sqlEntry = entries.FirstOrDefault(entry =>
                entry.FullName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));

            var sqlLabel = sqlEntry is null
                ? "dump SQL non trovato"
                : $"{sqlEntry.Name} ({sqlEntry.Length / 1024d / 1024d:N1} MB)";

            return $"Archivio ZIP: {info.Name}\nDimensione: {info.Length / 1024d / 1024d:N1} MB\nDump DB: {sqlLabel}\nContenuti archivio: {entries.Count:N0} elementi";
        }

        return $"File backup: {info.Name}\nDimensione: {info.Length / 1024d / 1024d:N1} MB";
    }

    private void OnImportProgress(GestionaleBackupImportProgress progress)
    {
        ProgressStage = progress.Stage;
        ProgressDetail = progress.Message;
        ProgressPercent = progress.Total <= 0
            ? 0
            : Math.Round((double)progress.Current / progress.Total * 100d, 1);
    }
}
