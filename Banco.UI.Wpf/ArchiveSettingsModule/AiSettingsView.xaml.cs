namespace Banco.UI.Wpf.ArchiveSettingsModule;

public partial class AiSettingsView : System.Windows.Controls.UserControl
{
    private bool _isSynchronizingPassword;

    public AiSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is System.ComponentModel.INotifyPropertyChanged oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        if (e.NewValue is Banco.AI.ViewModels.AiSettingsViewModel newViewModel)
        {
            newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            SyncPasswordBox(newViewModel.ApiKey);
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Banco.AI.ViewModels.AiSettingsViewModel.ApiKey) &&
            sender is Banco.AI.ViewModels.AiSettingsViewModel viewModel)
        {
            SyncPasswordBox(viewModel.ApiKey);
        }
    }

    private void ApiKeyPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isSynchronizingPassword ||
            DataContext is not Banco.AI.ViewModels.AiSettingsViewModel viewModel)
        {
            return;
        }

        viewModel.ApiKey = ApiKeyPasswordBox.Password;
    }

    private void SyncPasswordBox(string apiKey)
    {
        if (ApiKeyPasswordBox.Password == apiKey)
        {
            return;
        }

        _isSynchronizingPassword = true;
        ApiKeyPasswordBox.Password = apiKey;
        _isSynchronizingPassword = false;
    }
}
