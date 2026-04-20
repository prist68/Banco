using Banco.Vendita.Configuration;

namespace Banco.Vendita.Abstractions;

public interface ILocalStoreBootstrapper
{
    Task InitializeAsync(LocalStoreSettings settings, CancellationToken cancellationToken = default);
}
