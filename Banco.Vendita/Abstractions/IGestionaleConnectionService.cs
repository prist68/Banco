using Banco.Vendita.Configuration;
using Banco.Vendita.Diagnostics;

namespace Banco.Vendita.Abstractions;

public interface IGestionaleConnectionService
{
    Task<ConnectionTestResult> TestConnectionAsync(
        GestionaleDatabaseSettings settings,
        CancellationToken cancellationToken = default);
}
