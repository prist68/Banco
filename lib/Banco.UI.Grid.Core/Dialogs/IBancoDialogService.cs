namespace Banco.UI.Grid.Core.Dialogs;

public interface IBancoDialogService
{
    Task<bool> ConfirmAsync(BancoConfirmRequest request, CancellationToken cancellationToken = default);

    Task ShowMessageAsync(BancoMessageRequest request, CancellationToken cancellationToken = default);

    Task<T?> ChooseAsync<T>(BancoChoiceRequest<T> request, CancellationToken cancellationToken = default);
}
