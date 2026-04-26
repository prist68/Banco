using Banco.AI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        services.AddSingleton<AiSettingsViewModel>();

        return services;
    }
}
