using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Costing.Api;

public static class CostingModule
{
    public static IServiceCollection AddCostingModule(this IServiceCollection services, IConfiguration config)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CostingModule).Assembly));
        var appAsm = typeof(FactoryERP.Modules.Costing.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }

    public static IEndpointRouteBuilder MapCostingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/costing");
        g.MapGet("/ping", () => Results.Ok("costing-ok"));
        return app;
    }
}
