namespace Banco.UI.Wpf.ViewModels;

public sealed class UIDocumentListHostViewModel
{
    public UIDocumentListHostViewModel(DocumentListViewModel documentListViewModel)
    {
        DocumentListViewModel = documentListViewModel;
    }

    public DocumentListViewModel DocumentListViewModel { get; }
}
