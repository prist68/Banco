namespace Banco.UI.Grid.Core.Dialogs;

public sealed class BancoMessageRequest
{
    public required string Title { get; init; }

    public required string Message { get; init; }

    public string CloseText { get; init; } = "OK";

    public BancoDialogTone Tone { get; init; } = BancoDialogTone.Info;
}
