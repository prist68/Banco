namespace Banco.Punti.ViewModels;

public sealed class InlinePickerOption
{
    public InlinePickerOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }
}
