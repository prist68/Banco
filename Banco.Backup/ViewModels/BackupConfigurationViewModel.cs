using Banco.Vendita.Abstractions;
using Banco.Vendita.Configuration;
using System.Collections.ObjectModel;

namespace Banco.Backup.ViewModels;

public sealed class BackupConfigurationViewModel : ViewModelBase
{
    private static readonly TimeSpan SchedulingAutoSaveDelay = TimeSpan.FromMilliseconds(250);
    private readonly IApplicationConfigurationService _configurationService;
    private readonly IBackupScheduleService _backupScheduleService;
    private readonly IGestionaleBackupExportService _backupExportService;
    private readonly IPosProcessLogService _logService;
    private bool _isApplyingSettings;
    private CancellationTokenSource? _schedulingAutoSaveCts;

    private string _defaultBackupDirectory = string.Empty;
    private bool _rememberLastOptions;
    private bool _disableBackupPrompt;
    private int _backupSuggestionDays;
    private DateTime? _lastBackupAt;
    private bool _useDifferentialBackupNaming;
    private string _enabledComputerName = string.Empty;
    private bool _restrictAdminBackup;
    private bool _deleteOldBackupsEnabled;
    private int _deleteOldBackupsAfterDays = 30;
    private bool _scheduledBackupEnabled;
    private int _scheduledRunsPerDay;
    private string _scheduledStartTime = "13:00";
    private bool _scheduledOnMonday;
    private bool _scheduledOnTuesday;
    private bool _scheduledOnWednesday;
    private bool _scheduledOnThursday;
    private bool _scheduledOnFriday;
    private bool _scheduledOnSaturday;
    private bool _scheduledOnSunday;
    private bool _includeDatabase;
    private bool _includeAttachments;
    private bool _includeImages;
    private bool _includeLayouts;
    private bool _includeReports;
    private bool _isBackupInProgress;
    private string _backupProgressStage = "Pronto";
    private string _backupProgressDetail = "Nessun backup in corso.";
    private double _backupProgressPercent;
    private string _defaultComment = string.Empty;
    private string _statusMessage = "Configurazione backup non ancora caricata.";

    public BackupConfigurationViewModel(
        IApplicationConfigurationService configurationService,
        IBackupScheduleService backupScheduleService,
        IGestionaleBackupExportService backupExportService,
        IPosProcessLogService logService)
    {
        _configurationService = configurationService;
        _backupScheduleService = backupScheduleService;
        _backupExportService = backupExportService;
        _logService = logService;
        SaveCommand = new RelayCommand(() => _ = SaveAsync());
        ExecuteBackupCommand = new RelayCommand(() => _ = ExecuteBackupAsync(), CanExecuteBackup);
        UseCurrentComputerCommand = new RelayCommand(UseCurrentComputer);
        SetLastBackupNowCommand = new RelayCommand(SetLastBackupNow);
        _ = LoadAsync();
    }

    public string DefaultBackupDirectory
    {
        get => _defaultBackupDirectory;
        set => SetProperty(ref _defaultBackupDirectory, value);
    }

    public bool RememberLastOptions
    {
        get => _rememberLastOptions;
        set => SetProperty(ref _rememberLastOptions, value);
    }

    public bool DisableBackupPrompt
    {
        get => _disableBackupPrompt;
        set => SetProperty(ref _disableBackupPrompt, value);
    }

    public int BackupSuggestionDays
    {
        get => _backupSuggestionDays;
        set => SetProperty(ref _backupSuggestionDays, value <= 0 ? 1 : value);
    }

    public DateTime? LastBackupAt
    {
        get => _lastBackupAt;
        set
        {
            if (SetProperty(ref _lastBackupAt, value))
            {
                NotifyPropertyChanged(nameof(LastBackupSummary));
            }
        }
    }

    public bool UseDifferentialBackupNaming
    {
        get => _useDifferentialBackupNaming;
        set => SetProperty(ref _useDifferentialBackupNaming, value);
    }

    public string EnabledComputerName
    {
        get => _enabledComputerName;
        set => SetProperty(ref _enabledComputerName, value);
    }

    public bool RestrictAdminBackup
    {
        get => _restrictAdminBackup;
        set => SetProperty(ref _restrictAdminBackup, value);
    }

    public bool DeleteOldBackupsEnabled
    {
        get => _deleteOldBackupsEnabled;
        set => SetProperty(ref _deleteOldBackupsEnabled, value);
    }

    public int DeleteOldBackupsAfterDays
    {
        get => _deleteOldBackupsAfterDays;
        set => SetProperty(ref _deleteOldBackupsAfterDays, value <= 0 ? 1 : value);
    }

    public bool ScheduledBackupEnabled
    {
        get => _scheduledBackupEnabled;
        set
        {
            if (SetProperty(ref _scheduledBackupEnabled, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Programmazione backup aggiornata.");
            }
        }
    }

    public int ScheduledRunsPerDay
    {
        get => _scheduledRunsPerDay;
        set
        {
            if (SetProperty(ref _scheduledRunsPerDay, value <= 0 ? 1 : value))
            {
                SyncAdditionalScheduledTimes();
                MarkSchedulingChanged("Numero esecuzioni giornaliere aggiornato.");
            }
        }
    }

    public string ScheduledStartTime
    {
        get => _scheduledStartTime;
        set
        {
            if (SetProperty(ref _scheduledStartTime, NormalizeTime(value)))
            {
                MarkSchedulingChanged("Orario iniziale aggiornato.");
            }
        }
    }

    public bool ScheduledOnMonday
    {
        get => _scheduledOnMonday;
        set
        {
            if (SetProperty(ref _scheduledOnMonday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool ScheduledOnTuesday
    {
        get => _scheduledOnTuesday;
        set
        {
            if (SetProperty(ref _scheduledOnTuesday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool ScheduledOnWednesday
    {
        get => _scheduledOnWednesday;
        set
        {
            if (SetProperty(ref _scheduledOnWednesday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool ScheduledOnThursday
    {
        get => _scheduledOnThursday;
        set
        {
            if (SetProperty(ref _scheduledOnThursday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool ScheduledOnFriday
    {
        get => _scheduledOnFriday;
        set
        {
            if (SetProperty(ref _scheduledOnFriday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool ScheduledOnSaturday
    {
        get => _scheduledOnSaturday;
        set
        {
            if (SetProperty(ref _scheduledOnSaturday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool ScheduledOnSunday
    {
        get => _scheduledOnSunday;
        set
        {
            if (SetProperty(ref _scheduledOnSunday, value))
            {
                NotifyPropertyChanged(nameof(ScheduledDaysSummary));
                MarkSchedulingChanged("Giorni pianificati aggiornati.");
            }
        }
    }

    public bool IncludeDatabase
    {
        get => _includeDatabase;
        set => SetProperty(ref _includeDatabase, value);
    }

    public bool IncludeAttachments
    {
        get => _includeAttachments;
        set => SetProperty(ref _includeAttachments, value);
    }

    public bool IncludeImages
    {
        get => _includeImages;
        set => SetProperty(ref _includeImages, value);
    }

    public bool IncludeLayouts
    {
        get => _includeLayouts;
        set => SetProperty(ref _includeLayouts, value);
    }

    public bool IncludeReports
    {
        get => _includeReports;
        set => SetProperty(ref _includeReports, value);
    }

    public string DefaultComment
    {
        get => _defaultComment;
        set => SetProperty(ref _defaultComment, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string BackupProgressStage
    {
        get => _backupProgressStage;
        private set => SetProperty(ref _backupProgressStage, value);
    }

    public string BackupProgressDetail
    {
        get => _backupProgressDetail;
        private set => SetProperty(ref _backupProgressDetail, value);
    }

    public double BackupProgressPercent
    {
        get => _backupProgressPercent;
        private set => SetProperty(ref _backupProgressPercent, value);
    }

    public bool IsBackupInProgress
    {
        get => _isBackupInProgress;
        private set
        {
            if (SetProperty(ref _isBackupInProgress, value))
            {
                ExecuteBackupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string CurrentComputerName => Environment.MachineName;

    public string LastBackupSummary => LastBackupAt.HasValue
        ? LastBackupAt.Value.ToString("dd/MM/yyyy HH:mm")
        : "Nessuna copia registrata";

    public string ScheduledDaysSummary
    {
        get
        {
            if (!ScheduledBackupEnabled)
            {
                return "Programmazione disattiva";
            }

            var days = new List<string>();
            if (ScheduledOnMonday) days.Add("Lun");
            if (ScheduledOnTuesday) days.Add("Mar");
            if (ScheduledOnWednesday) days.Add("Mer");
            if (ScheduledOnThursday) days.Add("Gio");
            if (ScheduledOnFriday) days.Add("Ven");
            if (ScheduledOnSaturday) days.Add("Sab");
            if (ScheduledOnSunday) days.Add("Dom");

            return days.Count == 0
                ? "Nessun giorno selezionato"
                : string.Join(", ", days);
        }
    }

    public RelayCommand SaveCommand { get; }

    public RelayCommand ExecuteBackupCommand { get; }

    public RelayCommand UseCurrentComputerCommand { get; }

    public RelayCommand SetLastBackupNowCommand { get; }

    public ObservableCollection<BackupScheduledTimeSlotViewModel> AdditionalScheduledTimes { get; } = [];

    public event EventHandler<string>? BackupCompleted;

    public event EventHandler<string>? BackupFailed;

    private async Task LoadAsync()
    {
        var settings = await _configurationService.LoadAsync();
        ApplySettings(settings.BackupConfiguration);
        StatusMessage = "Default backup caricati.";
    }

    private async Task SaveAsync()
    {
        try
        {
            var settings = await _configurationService.LoadAsync();
            settings.BackupConfiguration = BuildBackupSettings();

            await _configurationService.SaveAsync(settings);
            var schedulingResult = await _backupScheduleService.SynchronizeAsync(settings.BackupConfiguration);

            ApplySettings(settings.BackupConfiguration);
            StatusMessage = $"Configurazione backup salvata. {schedulingResult.Message}";
            _logService.Info(nameof(BackupConfigurationViewModel), $"Configurazione backup salvata. Cartella={settings.BackupConfiguration.DefaultBackupDirectory}, DisattivaSuggerimento={settings.BackupConfiguration.DisableBackupPrompt}, GiorniSuggerimento={settings.BackupConfiguration.BackupSuggestionDays}, UltimaCopia={settings.BackupConfiguration.LastBackupAt:O}, ComputerAbilitato={settings.BackupConfiguration.EnabledComputerName}, Schedulato={settings.BackupConfiguration.ScheduledBackupEnabled}, Orario={settings.BackupConfiguration.ScheduledStartTime}, EsecuzioniGiornaliere={settings.BackupConfiguration.ScheduledRunsPerDay}, Dati={settings.BackupConfiguration.IncludeDatabase}, Allegati={settings.BackupConfiguration.IncludeAttachments}, Immagini={settings.BackupConfiguration.IncludeImages}, Layout={settings.BackupConfiguration.IncludeLayouts}, Report={settings.BackupConfiguration.IncludeReports}, Task={string.Join(" | ", schedulingResult.TaskNames)}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore salvataggio configurazione backup: {ex.Message}";
            _logService.Error(nameof(BackupConfigurationViewModel), "Errore durante il salvataggio della configurazione backup.", ex);
        }
    }

    private bool CanExecuteBackup() => !IsBackupInProgress;

    private async Task ExecuteBackupAsync()
    {
        if (!CanExecuteBackup())
        {
            return;
        }

        IsBackupInProgress = true;
        StatusMessage = "Creazione backup in corso...";
        BackupProgressStage = "Preparazione";
        BackupProgressDetail = "Avvio creazione backup...";
        BackupProgressPercent = 0;

        try
        {
            var progress = new Progress<GestionaleBackupExportProgress>(OnBackupProgress);
            var result = await _backupExportService.ExportAsync(new GestionaleBackupExportRequest(
                DestinationDirectory: DefaultBackupDirectory,
                IncludeDatabase: IncludeDatabase,
                IncludeAttachments: IncludeAttachments,
                IncludeImages: IncludeImages,
                IncludeLayouts: IncludeLayouts,
                IncludeReports: IncludeReports),
                progress);

            LastBackupAt = result.CreatedAt;
            StatusMessage = $"Backup creato: {result.BackupFilePath}";
            BackupProgressStage = "Completato";
            BackupProgressDetail = "Backup completato correttamente.";
            BackupProgressPercent = 100;
            _logService.Info(nameof(BackupConfigurationViewModel), $"Backup eseguito manualmente. File={result.BackupFilePath}, Voci={string.Join(" | ", result.IncludedEntries)}.");
            BackupCompleted?.Invoke(this, result.BackupFilePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore creazione backup: {ex.Message}";
            BackupProgressStage = "Errore";
            BackupProgressDetail = "La creazione del backup si e` interrotta prima del completamento.";
            _logService.Error(nameof(BackupConfigurationViewModel), "Errore durante l'esecuzione manuale del backup.", ex);
            BackupFailed?.Invoke(this, ex.Message);
        }
        finally
        {
            IsBackupInProgress = false;
        }
    }

    private void ApplySettings(BackupConfigurationSettings settings)
    {
        _isApplyingSettings = true;
        try
        {
            DefaultBackupDirectory = settings.DefaultBackupDirectory;
            RememberLastOptions = settings.RememberLastOptions;
            DisableBackupPrompt = settings.DisableBackupPrompt;
            BackupSuggestionDays = settings.BackupSuggestionDays <= 0 ? 7 : settings.BackupSuggestionDays;
            LastBackupAt = settings.LastBackupAt;
            UseDifferentialBackupNaming = settings.UseDifferentialBackupNaming;
            EnabledComputerName = settings.EnabledComputerName;
            RestrictAdminBackup = settings.RestrictAdminBackup;
            DeleteOldBackupsEnabled = settings.DeleteOldBackupsEnabled;
            DeleteOldBackupsAfterDays = settings.DeleteOldBackupsAfterDays <= 0 ? 30 : settings.DeleteOldBackupsAfterDays;
            ScheduledBackupEnabled = settings.ScheduledBackupEnabled;
            ScheduledRunsPerDay = settings.ScheduledRunsPerDay <= 0 ? 1 : settings.ScheduledRunsPerDay;
            ScheduledStartTime = NormalizeTime(settings.ScheduledStartTime);
            LoadAdditionalScheduledTimes(settings.ScheduledAdditionalTimes);
            ScheduledOnMonday = settings.ScheduledOnMonday;
            ScheduledOnTuesday = settings.ScheduledOnTuesday;
            ScheduledOnWednesday = settings.ScheduledOnWednesday;
            ScheduledOnThursday = settings.ScheduledOnThursday;
            ScheduledOnFriday = settings.ScheduledOnFriday;
            ScheduledOnSaturday = settings.ScheduledOnSaturday;
            ScheduledOnSunday = settings.ScheduledOnSunday;
            IncludeDatabase = settings.IncludeDatabase;
            IncludeAttachments = settings.IncludeAttachments;
            IncludeImages = settings.IncludeImages;
            IncludeLayouts = settings.IncludeLayouts;
            IncludeReports = settings.IncludeReports;
            DefaultComment = settings.DefaultComment;
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void UseCurrentComputer()
    {
        EnabledComputerName = CurrentComputerName;
        StatusMessage = $"Computer abilitato impostato su {CurrentComputerName}.";
    }

    private void SetLastBackupNow()
    {
        LastBackupAt = DateTime.Now;
        StatusMessage = $"Ultima copia aggiornata a {LastBackupSummary}.";
    }

    private static string NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "13:00";
        }

        return TimeSpan.TryParse(value, out var time)
            ? $"{time.Hours:00}:{time.Minutes:00}"
            : "13:00";
    }

    private void LoadAdditionalScheduledTimes(IEnumerable<string>? times)
    {
        AdditionalScheduledTimes.Clear();

        if (times is not null)
        {
            foreach (var time in times)
            {
                AdditionalScheduledTimes.Add(CreateScheduledTimeSlot(AdditionalScheduledTimes.Count, NormalizeTime(time)));
            }
        }

        SyncAdditionalScheduledTimes();
    }

    private void SyncAdditionalScheduledTimes()
    {
        var requiredCount = Math.Max(0, ScheduledRunsPerDay - 1);

        while (AdditionalScheduledTimes.Count < requiredCount)
        {
            AdditionalScheduledTimes.Add(CreateScheduledTimeSlot(AdditionalScheduledTimes.Count, BuildSuggestedTime(AdditionalScheduledTimes.Count + 1)));
        }

        while (AdditionalScheduledTimes.Count > requiredCount)
        {
            AdditionalScheduledTimes.RemoveAt(AdditionalScheduledTimes.Count - 1);
        }
    }

    private BackupScheduledTimeSlotViewModel CreateScheduledTimeSlot(int index, string timeText)
    {
        return new BackupScheduledTimeSlotViewModel(index, timeText, () =>
        {
            MarkSchedulingChanged("Orari backup aggiornati.");
        });
    }

    private string BuildSuggestedTime(int offsetIndex)
    {
        var baseTime = TimeSpan.TryParse(NormalizeTime(ScheduledStartTime), out var parsed)
            ? parsed
            : new TimeSpan(13, 0, 0);
        var suggested = baseTime.Add(TimeSpan.FromHours(offsetIndex * 2));
        if (suggested.TotalHours >= 24)
        {
            suggested = suggested.Subtract(TimeSpan.FromDays(Math.Floor(suggested.TotalHours / 24)));
        }

        return $"{suggested.Hours:00}:{suggested.Minutes:00}";
    }

    private void MarkUnsavedChanges(string message)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        StatusMessage = message;
    }

    private void MarkSchedulingChanged(string message)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        StatusMessage = $"{message} Salvataggio automatico in corso...";
        QueueSchedulingAutoSave();
    }

    private void QueueSchedulingAutoSave()
    {
        _schedulingAutoSaveCts?.Cancel();
        _schedulingAutoSaveCts?.Dispose();

        var cts = new CancellationTokenSource();
        _schedulingAutoSaveCts = cts;
        _ = PersistSchedulingSettingsAsync(cts.Token);
    }

    private async Task PersistSchedulingSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SchedulingAutoSaveDelay, cancellationToken);

            var settings = await _configurationService.LoadAsync(cancellationToken);
            var currentBackupSettings = BuildBackupSettings();
            settings.BackupConfiguration.ScheduledBackupEnabled = currentBackupSettings.ScheduledBackupEnabled;
            settings.BackupConfiguration.ScheduledRunsPerDay = currentBackupSettings.ScheduledRunsPerDay;
            settings.BackupConfiguration.ScheduledStartTime = currentBackupSettings.ScheduledStartTime;
            settings.BackupConfiguration.ScheduledAdditionalTimes = currentBackupSettings.ScheduledAdditionalTimes;
            settings.BackupConfiguration.ScheduledOnMonday = currentBackupSettings.ScheduledOnMonday;
            settings.BackupConfiguration.ScheduledOnTuesday = currentBackupSettings.ScheduledOnTuesday;
            settings.BackupConfiguration.ScheduledOnWednesday = currentBackupSettings.ScheduledOnWednesday;
            settings.BackupConfiguration.ScheduledOnThursday = currentBackupSettings.ScheduledOnThursday;
            settings.BackupConfiguration.ScheduledOnFriday = currentBackupSettings.ScheduledOnFriday;
            settings.BackupConfiguration.ScheduledOnSaturday = currentBackupSettings.ScheduledOnSaturday;
            settings.BackupConfiguration.ScheduledOnSunday = currentBackupSettings.ScheduledOnSunday;

            await _configurationService.SaveAsync(settings, cancellationToken);
            var schedulingResult = await _backupScheduleService.SynchronizeAsync(settings.BackupConfiguration, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                StatusMessage = $"Programmazione backup salvata automaticamente. {schedulingResult.Message}";
            }
        }
        catch (OperationCanceledException)
        {
            // Il debounce annulla i salvataggi intermedi senza segnalarli come errore.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore salvataggio programmazione backup: {ex.Message}";
            _logService.Error(nameof(BackupConfigurationViewModel), "Errore durante il salvataggio automatico della programmazione backup.", ex);
        }
    }

    private void OnBackupProgress(GestionaleBackupExportProgress progress)
    {
        BackupProgressStage = progress.Stage;
        BackupProgressDetail = progress.Message;
        BackupProgressPercent = progress.Total <= 0
            ? 0
            : Math.Round((double)progress.Current / progress.Total * 100d, 1);
    }

    private BackupConfigurationSettings BuildBackupSettings()
    {
        return new BackupConfigurationSettings
        {
            DefaultBackupDirectory = string.IsNullOrWhiteSpace(DefaultBackupDirectory) ? @"C:\Facile Manager" : DefaultBackupDirectory.Trim(),
            RememberLastOptions = RememberLastOptions,
            DisableBackupPrompt = DisableBackupPrompt,
            BackupSuggestionDays = BackupSuggestionDays <= 0 ? 7 : BackupSuggestionDays,
            LastBackupAt = LastBackupAt,
            UseDifferentialBackupNaming = UseDifferentialBackupNaming,
            EnabledComputerName = EnabledComputerName?.Trim() ?? string.Empty,
            RestrictAdminBackup = RestrictAdminBackup,
            DeleteOldBackupsEnabled = DeleteOldBackupsEnabled,
            DeleteOldBackupsAfterDays = DeleteOldBackupsAfterDays <= 0 ? 30 : DeleteOldBackupsAfterDays,
            ScheduledBackupEnabled = ScheduledBackupEnabled,
            ScheduledRunsPerDay = ScheduledRunsPerDay <= 0 ? 1 : ScheduledRunsPerDay,
            ScheduledStartTime = NormalizeTime(ScheduledStartTime),
            ScheduledAdditionalTimes = AdditionalScheduledTimes
                .Select(item => NormalizeTime(item.TimeText))
                .ToList(),
            ScheduledOnMonday = ScheduledOnMonday,
            ScheduledOnTuesday = ScheduledOnTuesday,
            ScheduledOnWednesday = ScheduledOnWednesday,
            ScheduledOnThursday = ScheduledOnThursday,
            ScheduledOnFriday = ScheduledOnFriday,
            ScheduledOnSaturday = ScheduledOnSaturday,
            ScheduledOnSunday = ScheduledOnSunday,
            IncludeDatabase = IncludeDatabase,
            IncludeAttachments = IncludeAttachments,
            IncludeImages = IncludeImages,
            IncludeLayouts = IncludeLayouts,
            IncludeReports = IncludeReports,
            DefaultComment = DefaultComment?.Trim() ?? string.Empty
        };
    }
}
