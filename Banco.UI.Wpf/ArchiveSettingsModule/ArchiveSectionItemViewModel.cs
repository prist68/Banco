using Banco.UI.Wpf.ViewModels;

namespace Banco.UI.Wpf.ArchiveSettingsModule;

public sealed class ArchiveSectionItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public ArchiveSectionItemViewModel(
        ArchiveSettingsSection section,
        string title,
        string subtitle,
        string iconResourceKey)
    {
        Section = section;
        Title = title;
        Subtitle = subtitle;
        IconResourceKey = iconResourceKey;
    }

    public ArchiveSettingsSection Section { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string IconResourceKey { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
