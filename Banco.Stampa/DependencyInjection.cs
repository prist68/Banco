using Microsoft.Extensions.DependencyInjection;

namespace Banco.Stampa;

public static class DependencyInjection
{
    public static IServiceCollection AddStampaServices(this IServiceCollection services)
    {
        services.AddSingleton<IPrintModulePathService, PrintModulePathService>();
        services.AddSingleton<IPrinterCatalogService, SystemPrinterCatalogService>();
        services.AddSingleton<IPrintLayoutCatalogService, JsonPrintLayoutCatalogService>();
        services.AddSingleton<IFastReportRuntimeService, FastReportRuntimeService>();
        services.AddSingleton<IBancoPosPrintService, BancoPosPrintService>();
        services.AddSingleton<IFastReportStoreProfileService, FastReportStoreProfileService>();
        services.AddSingleton<IFastReportPreviewDataService, FastReportPreviewDataService>();
        services.AddSingleton<IFastReportDocumentSchemaService, FastReportDocumentSchemaService>();
        services.AddSingleton<ILegacyRepxReportCatalogService, LegacyRepxReportCatalogService>();
        services.AddSingleton<IPrintReportContractCatalogService, PrintReportContractCatalogService>();
        services.AddSingleton<IPrintReportFamilyCatalogService, PrintReportFamilyCatalogService>();
        return services;
    }
}
