using System.Windows.Input;

namespace Banco.UI.Wpf.ViewModels;

public sealed class ShellWorkspaceTabViewModel : ViewModelBase
{
    public ShellWorkspaceTabViewModel(string key, string destinationKey, string title, object content, bool canClose, Action<ShellWorkspaceTabViewModel> closeAction)
    {
        Key = key;
        DestinationKey = destinationKey;
        Title = title;
        Content = content;
        DisplayTitle = ResolveDisplayTitle(title, content);
        CanClose = canClose;
        CloseCommand = new RelayCommand(() => closeAction(this), () => CanClose);
    }

    public string Key { get; }

    public string DestinationKey { get; }

    public string Title { get; }

    public string DisplayTitle { get; }

    public object Content { get; }

    public bool CanClose { get; }

    public ICommand CloseCommand { get; }

    private static string ResolveDisplayTitle(string title, object content)
    {
        if (!string.IsNullOrWhiteSpace(title) && !title.Contains('.', StringComparison.Ordinal))
        {
            return title.Trim();
        }

        var contentTypeName = content.GetType().Name;

        return contentTypeName switch
        {
            "BancoViewModel" => "Banco",
            "DocumentListViewModel" => "Documenti",
            "UIDocumentListHostViewModel" => "Documenti",
            "ReorderListViewModel" => "Lista riordino",
            "MagazzinoArticleViewModel" => "Articolo magazzino",
            "PuntiViewModel" => "Punti",
            "SettingsViewModel" => "Configurazione DB",
            "DiagnosticsViewModel" => "Diagnostica / Percorsi",
            _ => string.IsNullOrWhiteSpace(title) ? contentTypeName.Replace("ViewModel", string.Empty, StringComparison.Ordinal) : title.Trim()
        };
    }
}
