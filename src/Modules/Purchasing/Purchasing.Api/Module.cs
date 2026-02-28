using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Purchasing.Api;

public static class PurchasingModule
{
    public static IServiceCollection AddPurchasingModule(this IServiceCollection services, IConfiguration config)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PurchasingModule).Assembly));
        var appAsm = typeof(FactoryERP.Modules.Purchasing.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }

    public static IEndpointRouteBuilder MapPurchasingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/purchasing");
        g.MapGet("/ping", () => Results.Ok("purchasing-ok"));
        return app;
    }
}
