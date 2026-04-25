using System.Windows.Input;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ShellWorkspaceTabViewModel : ViewModelBase
{
    private string _displayTitle;

    public ShellWorkspaceTabViewModel(string key, string destinationKey, string title, object content, bool canClose, Action<ShellWorkspaceTabViewModel> closeAction)
    {
        Key = key;
        DestinationKey = destinationKey;
        Title = title;
        Content = content;
        _displayTitle = ResolveDisplayTitle(title, content);
        CanClose = canClose;
        CloseCommand = new RelayCommand(() => closeAction(this), () => CanClose);
    }

    public string Key { get; }

    public string DestinationKey { get; }

    public string Title { get; }

    public string DisplayTitle
    {
        get => _displayTitle;
        private set => SetProperty(ref _displayTitle, value);
    }

    public object Content { get; }

    public bool CanClose { get; }

    public ICommand CloseCommand { get; }

    public void UpdateDisplayTitle(string displayTitle)
    {
        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            return;
        }

        DisplayTitle = displayTitle.Trim();
    }

    private static string ResolveDisplayTitle(string title, object content)
    {
        if (!string.IsNullOrWhiteSpace(title) && !title.Contains('.', StringComparison.Ordinal))
        {
            return title.Trim();
        }

        var contentTypeName = content.GetType().Name;

        return contentTypeName switch
        {
            "DashboardViewModel" => "Dashboard",
            "BancoViewModel" => "Banco",
            "DocumentListViewModel" => "Documenti",
            "UIDocumentListHostViewModel" => "Documenti",
            "ReorderListViewModel" => "Lista riordino",
            "MagazzinoArticleViewModel" => "Articolo magazzino",
            "PuntiViewModel" => "Punti",
            "ArchiveSettingsHostViewModel" => "Impostazioni archivio",
            "DiagnosticsViewModel" => "Diagnostica / Percorsi",
            _ => string.IsNullOrWhiteSpace(title) ? contentTypeName.Replace("ViewModel", string.Empty, StringComparison.Ordinal) : title.Trim()
        };
    }
}
