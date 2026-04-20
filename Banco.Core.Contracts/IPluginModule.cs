namespace Banco.Core.Contracts;

public interface IPluginModule
{
    string Id { get; }

    string DisplayName { get; }
}
