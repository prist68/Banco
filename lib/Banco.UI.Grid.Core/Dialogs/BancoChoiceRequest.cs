namespace Banco.UI.Grid.Core.Dialogs;

public sealed class BancoChoiceRequest<T>
{
    public required string Title { get; init; }

    public string? Message { get; init; }

    public required IReadOnlyList<BancoChoiceOption<T>> Options { get; init; }

    public string CancelText { get; init; } = "Annulla";
}
