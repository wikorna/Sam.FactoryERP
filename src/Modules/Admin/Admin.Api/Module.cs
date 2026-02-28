using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Admin.Api;

public static class AdminModule
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services, IConfiguration config)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AdminModule).Assembly));
        var appAsm = typeof(FactoryERP.Modules.Admin.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/admin");
        g.MapGet("/ping", () => Results.Ok("admin-ok"));
        return app;
    }
}
