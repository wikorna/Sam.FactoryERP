using FactoryERP.Abstractions.Extensions;
using Inventory.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Api;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration config)
    {
        var appAsm = typeof(FactoryERP.Modules.Inventory.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));

        // FluentValidation validators from Application layer
        services.AddValidatorsFromModule<FactoryERP.Modules.Inventory.Application.AssemblyMarker>();

        // Infrastructure (EF Core, repos)
        services.AddInventoryInfrastructure(config);

        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Endpoints are mapped via [ApiController] attribute discovery
        return app;
    }
}
