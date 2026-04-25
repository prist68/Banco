using Banco.Backup.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.Backup;

public static class DependencyInjection
{
    public static IServiceCollection AddBackupServices(this IServiceCollection services)
    {
        services.AddSingleton<BackupConfigurationViewModel>();
        services.AddSingleton<RestoreConfigurationViewModel>();
        services.AddSingleton<BackupImportViewModel>();

        return services;
    }
}
