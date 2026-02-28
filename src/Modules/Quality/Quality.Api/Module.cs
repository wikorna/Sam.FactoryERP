using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Quality.Api;

public static class QualityModule
{
    public static IServiceCollection AddQualityModule(this IServiceCollection services, IConfiguration config)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(QualityModule).Assembly));
        var appAsm = typeof(FactoryERP.Modules.Quality.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }

    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/quality");
        g.MapGet("/ping", () => Results.Ok("quality-ok"));
        return app;
    }
}
