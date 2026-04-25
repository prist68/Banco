using Banco.Riordino;
using Banco.Vendita.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.Core.LocalStore;

public static class DependencyInjection
{
    public static IServiceCollection AddLocalStoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocalStoreBootstrapper, SqliteLocalStoreBootstrapper>();
        services.AddSingleton<ILocalDocumentRepository, SqliteLocalDocumentRepository>();
        services.AddSingleton<ILocalAuditRepository, SqliteLocalAuditRepository>();
        services.AddSingleton<IPointsRewardRuleRepository, SqlitePointsRewardRuleRepository>();
        services.AddSingleton<IPromotionEventRepository, SqlitePromotionEventRepository>();
        services.AddSingleton<IReorderListRepository, SqliteReorderListRepository>();
        services.AddSingleton<IReorderArticleSettingsRepository, SqliteReorderArticleSettingsRepository>();
        services.AddSingleton<ILocalArticleTagRepository, SqliteLocalArticleTagRepository>();

        return services;
    }
}
