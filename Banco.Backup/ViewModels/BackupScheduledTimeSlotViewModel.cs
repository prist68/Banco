namespace Banco.Backup.ViewModels;

public sealed class BackupScheduledTimeSlotViewModel : ViewModelBase
{
    private readonly Action _onChanged;
    private string _timeText;

    public BackupScheduledTimeSlotViewModel(int index, string timeText, Action onChanged)
    {
        Index = index;
        _timeText = timeText;
        _onChanged = onChanged;
    }

    public int Index { get; }

    public string Label => $"Orario {Index + 2}";

    public string TimeText
    {
        get => _timeText;
        set
        {
            if (SetProperty(ref _timeText, value))
            {
                _onChanged();
            }
        }
    }
}
