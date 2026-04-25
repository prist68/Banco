namespace Banco.UI.Shared.Grid;

public sealed class GridColumnDefinition
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    public required bool IsVisibleByDefault { get; init; }

    public required double DefaultWidth { get; init; }

    public required int DefaultDisplayIndex { get; init; }

    public bool CanHide { get; init; } = true;

    public bool IsLocked { get; init; }

    public double? MinWidth { get; init; }

    public double? MaxWidth { get; init; }

    public string? Group { get; init; }

    public bool IsFrozen { get; init; }

    public string? PresetKey { get; init; }

    public string? Format { get; init; }

    public string? Description { get; init; }

    public bool IsNumeric { get; init; }

    public bool IsPresetUnscontrinati { get; init; }

    // Guard rail: il nome stabile lato menu/shared e` TextAlignment.
    public Banco.Vendita.Configuration.GridColumnContentAlignment TextAlignment { get; init; } = Banco.Vendita.Configuration.GridColumnContentAlignment.Center;

    // Compatibilita` temporanea con il codice gia` introdotto.
    public Banco.Vendita.Configuration.GridColumnContentAlignment DefaultContentAlignment
    {
        get => TextAlignment;
        init => TextAlignment = value;
    }
}
