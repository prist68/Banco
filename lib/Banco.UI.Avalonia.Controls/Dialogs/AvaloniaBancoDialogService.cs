using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Banco.UI.Grid.Core.Dialogs;

namespace Banco.UI.Avalonia.Controls.Dialogs;

public sealed class AvaloniaBancoDialogService : IBancoDialogService
{
    private readonly Func<Window?>? _ownerResolver;

    public AvaloniaBancoDialogService(Func<Window?>? ownerResolver = null)
    {
        _ownerResolver = ownerResolver;
    }

    public async Task<bool> ConfirmAsync(BancoConfirmRequest request, CancellationToken cancellationToken = default)
    {
        var dialog = new BancoDialogWindow();
        dialog.Configure(request.Title, request.Message, request.ConfirmText, request.CancelText);
        return await ShowBooleanDialogAsync(dialog);
    }

    public async Task ShowMessageAsync(BancoMessageRequest request, CancellationToken cancellationToken = default)
    {
        var dialog = new BancoDialogWindow();
        dialog.Configure(request.Title, request.Message, request.CloseText);
        _ = await ShowBooleanDialogAsync(dialog);
    }

    public async Task<T?> ChooseAsync<T>(BancoChoiceRequest<T> request, CancellationToken cancellationToken = default)
    {
        var dialog = new BancoChoiceDialogWindow<T>(request);
        var owner = ResolveOwner();
        var confirmed = owner is null
            ? await dialog.ShowDialog<bool>(new Window())
            : await dialog.ShowDialog<bool>(owner);

        return confirmed ? dialog.SelectedValue : default;
    }

    private async Task<bool> ShowBooleanDialogAsync(BancoDialogWindow dialog)
    {
        var owner = ResolveOwner();
        return owner is null
            ? await dialog.ShowDialog<bool>(new Window())
            : await dialog.ShowDialog<bool>(owner);
    }

    private Window? ResolveOwner()
    {
        if (_ownerResolver is not null)
        {
            return _ownerResolver();
        }

        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
