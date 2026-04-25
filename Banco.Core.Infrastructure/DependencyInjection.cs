using Banco.Vendita.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Banco.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationConfigurationService, FileApplicationConfigurationService>();
        services.AddSingleton<IBackupScheduleService, WindowsBackupScheduleService>();
        services.AddSingleton<IGestionaleConnectionService, GestionaleConnectionService>();
        services.AddSingleton<IGestionaleBackupExportService, GestionaleBackupExportService>();
        services.AddSingleton<IGestionaleBackupImportService, GestionaleBackupImportService>();
        services.AddSingleton<IGestionaleDocumentReadService, GestionaleDocumentReadService>();
        services.AddSingleton<IGestionaleDocumentDeleteService, GestionaleDocumentDeleteService>();
        services.AddSingleton<IGestionaleDocumentWriter, GestionaleDocumentWriter>();
        services.AddSingleton<ILegacyReceiptAlignmentService, LegacyReceiptAlignmentService>();
        services.AddSingleton<IGestionaleSupplierOrderWriteService, GestionaleSupplierOrderWriteService>();
        services.AddSingleton<IGestionaleArticleReadService, GestionaleArticleReadService>();
        services.AddSingleton<IGestionaleArticleWriteService, GestionaleArticleWriteService>();
        services.AddSingleton<IGestionaleCustomerReadService, GestionaleCustomerReadService>();
        services.AddSingleton<IGestionaleFidelityHistoryService, GestionaleFidelityHistoryService>();
        services.AddSingleton<IGestionalePriceListReadService, GestionalePriceListReadService>();
        services.AddSingleton<GestionalePointsReadService>();
        services.AddSingleton<IGestionalePointsReadService>(sp => sp.GetRequiredService<GestionalePointsReadService>());
        services.AddSingleton<IGestionalePointsWriteService>(sp => sp.GetRequiredService<GestionalePointsReadService>());
        services.AddSingleton<IGestionaleOperatorReadService, GestionaleOperatorReadService>();
        services.AddSingleton<IPosPaymentService, NexiPosPaymentService>();
        services.AddSingleton<IWinEcrAutoRunService, WinEcrAutoRunService>();
        services.AddSingleton<IPosProcessLogService, PosProcessLogService>();

        return services;
    }
}
