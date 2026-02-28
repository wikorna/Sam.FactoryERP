using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Production.Api;

public static class ProductionModule
{
    public static IServiceCollection AddProductionModule(this IServiceCollection services, IConfiguration config)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProductionModule).Assembly));
        var appAsm = typeof(FactoryERP.Modules.Production.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }

    public static IEndpointRouteBuilder MapProductionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/production");
        g.MapGet("/ping", () => Results.Ok("production-ok"));
        return app;
    }
}
