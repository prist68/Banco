using System.Collections.ObjectModel;
using AiSettingsViewModel = Banco.AI.ViewModels.AiSettingsViewModel;
using BackupConfigurationViewModel = Banco.Backup.ViewModels.BackupConfigurationViewModel;
using RestoreConfigurationViewModel = Banco.Backup.ViewModels.RestoreConfigurationViewModel;

namespace Banco.UI.Wpf.ArchiveSettingsModule;

public sealed class ArchiveSettingsHostViewModel : Banco.UI.Wpf.ViewModels.ViewModelBase
{
    private ArchiveSectionItemViewModel? _selectedSection;

    public ArchiveSettingsHostViewModel(
        ArchiveGeneralSettingsViewModel generalSettings,
        BackupConfigurationViewModel backupConfiguration,
        RestoreConfigurationViewModel restoreConfiguration,
        SqliteSettingsViewModel sqliteConfiguration,
        AiSettingsViewModel aiSettings)
    {
        GeneralSettings = generalSettings;
        BackupConfiguration = backupConfiguration;
        RestoreConfiguration = restoreConfiguration;
        SqliteConfiguration = sqliteConfiguration;
        AiSettings = aiSettings;

        Sections =
        [
            new ArchiveSectionItemViewModel(ArchiveSettingsSection.General, "Generali", "DB legacy e percorsi FM", "IconSettings"),
            new ArchiveSectionItemViewModel(ArchiveSettingsSection.Backup, "Backup", "Default salvataggio archivio", "IconSaveAction"),
            new ArchiveSectionItemViewModel(ArchiveSettingsSection.Restore, "Restore", "Import e ripristino backup", "IconDocOpen"),
            new ArchiveSectionItemViewModel(ArchiveSettingsSection.Sqlite, "SQLite", "Supporto tecnico locale", "IconDatabase"),
            new ArchiveSectionItemViewModel(ArchiveSettingsSection.Ai, "Impostazioni AI", "API e modello assistente", "IconAi")
        ];

        foreach (var section in Sections)
        {
            section.IsSelected = false;
        }

        SelectSection(ArchiveSettingsSection.General);
    }

    public string Titolo => "Impostazioni archivio";

    public string Sottotitolo => "Configurazione archivio FM-like con sezioni dedicate per generale, backup, restore, SQLite e supporti AI.";

    public ObservableCollection<ArchiveSectionItemViewModel> Sections { get; }

    public ArchiveGeneralSettingsViewModel GeneralSettings { get; }

    public BackupConfigurationViewModel BackupConfiguration { get; }

    public RestoreConfigurationViewModel RestoreConfiguration { get; }

    public SqliteSettingsViewModel SqliteConfiguration { get; }

    public AiSettingsViewModel AiSettings { get; }

    public ArchiveSectionItemViewModel? SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                NotifyPropertyChanged(nameof(CurrentSectionContent));
                NotifyPropertyChanged(nameof(CurrentSectionTitle));
                NotifyPropertyChanged(nameof(CurrentSectionSubtitle));
            }
        }
    }

    public object CurrentSectionContent => SelectedSection?.Section switch
    {
        ArchiveSettingsSection.Backup => BackupConfiguration,
        ArchiveSettingsSection.Restore => RestoreConfiguration,
        ArchiveSettingsSection.Sqlite => SqliteConfiguration,
        ArchiveSettingsSection.Ai => AiSettings,
        _ => GeneralSettings
    };

    public string CurrentSectionTitle => SelectedSection?.Title ?? "Generali";

    public string CurrentSectionSubtitle => SelectedSection?.Subtitle ?? string.Empty;

    public void SelectSection(ArchiveSettingsSection section)
    {
        var target = Sections.FirstOrDefault(item => item.Section == section) ?? Sections.FirstOrDefault();
        if (target is null)
        {
            return;
        }

        foreach (var item in Sections)
        {
            item.IsSelected = ReferenceEquals(item, target);
        }

        SelectedSection = target;
    }
}
