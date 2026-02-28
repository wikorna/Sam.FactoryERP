using Admin.Api;
using Auth.Api;
using Costing.Api;
using EDI.Api;
using FactoryERP.Modules.Sales.Api;
using FactoryERP.Modules.Production.Api;
using FactoryERP.Modules.Purchasing.Api;
using FactoryERP.Modules.Inventory.Api;
using FactoryERP.Modules.Costing.Api;
using FactoryERP.Modules.Quality.Api;
using FactoryERP.Modules.Admin.Api;
using FactoryERP.Modules.EDI.Api;
using FactoryERP.Modules.Labeling.Api;
using Inventory.Api;
using Labeling.Api;
using Production.Api;
using Purchasing.Api;
using Quality.Api;
using Sales.Api;

namespace FactoryERP.ApiHost.Modules;

public static class ModuleCatalog
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddSalesModule(config);
        services.AddProductionModule(config);
        services.AddPurchasingModule(config);
        services.AddInventoryModule(config);
        services.AddCostingModule(config);
        services.AddQualityModule(config);
        services.AddAdminModule(config);
        services.AddEdiModule(config);
        services.AddLabelingModule(config);
        services.AddAuthModule(config);
        return services;
    }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder app)
    {
        app.MapSalesEndpoints();
        app.MapProductionEndpoints();
        app.MapPurchasingEndpoints();
        app.MapInventoryEndpoints();
        app.MapCostingEndpoints();
        app.MapQualityEndpoints();
        app.MapAdminEndpoints();
        app.MapEdiEndpoints();
        app.MapLabelingEndpoints();
        app.MapAuthEndpoints();
        return app;
    }
}
