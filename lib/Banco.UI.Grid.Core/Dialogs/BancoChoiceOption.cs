namespace Banco.UI.Grid.Core.Dialogs;

public sealed class BancoChoiceOption<T>
{
    public required string Label { get; init; }

    public string? Description { get; init; }

    public required T Value { get; init; }
}
