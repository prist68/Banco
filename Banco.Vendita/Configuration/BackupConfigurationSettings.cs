namespace Banco.Vendita.Configuration;

public sealed class BackupConfigurationSettings
{
    public string DefaultBackupDirectory { get; set; } = @"C:\Facile Manager";

    public bool RememberLastOptions { get; set; } = true;

    public bool DisableBackupPrompt { get; set; }

    public int BackupSuggestionDays { get; set; } = 7;

    public DateTime? LastBackupAt { get; set; }

    public bool UseDifferentialBackupNaming { get; set; } = true;

    public string EnabledComputerName { get; set; } = string.Empty;

    public bool RestrictAdminBackup { get; set; }

    public bool DeleteOldBackupsEnabled { get; set; }

    public int DeleteOldBackupsAfterDays { get; set; } = 30;

    public bool ScheduledBackupEnabled { get; set; }

    public int ScheduledRunsPerDay { get; set; } = 1;

    public string ScheduledStartTime { get; set; } = "13:00";

    public List<string> ScheduledAdditionalTimes { get; set; } = [];

    public bool ScheduledOnMonday { get; set; } = true;

    public bool ScheduledOnTuesday { get; set; } = true;

    public bool ScheduledOnWednesday { get; set; } = true;

    public bool ScheduledOnThursday { get; set; } = true;

    public bool ScheduledOnFriday { get; set; } = true;

    public bool ScheduledOnSaturday { get; set; }

    public bool ScheduledOnSunday { get; set; }

    public bool IncludeDatabase { get; set; } = true;

    public bool IncludeAttachments { get; set; }

    public bool IncludeImages { get; set; }

    public bool IncludeLayouts { get; set; }

    public bool IncludeReports { get; set; }

    public string DefaultComment { get; set; } = string.Empty;
}
