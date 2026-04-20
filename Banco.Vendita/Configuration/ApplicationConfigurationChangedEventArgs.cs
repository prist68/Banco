namespace Banco.Vendita.Configuration;

public sealed class ApplicationConfigurationChangedEventArgs : EventArgs
{
    public required AppSettings Settings { get; init; }

    public bool GestionaleDatabaseChanged { get; init; }

    public bool LocalStoreChanged { get; init; }
}
