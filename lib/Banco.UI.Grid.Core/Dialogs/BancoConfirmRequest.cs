namespace Banco.UI.Grid.Core.Dialogs;

public sealed class BancoConfirmRequest
{
    public required string Title { get; init; }

    public required string Message { get; init; }

    public string ConfirmText { get; init; } = "Conferma";

    public string CancelText { get; init; } = "Annulla";

    public BancoDialogTone Tone { get; init; } = BancoDialogTone.Info;
}
