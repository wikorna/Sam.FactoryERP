using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shipping.Application.Extensions;
using Shipping.Infrastructure;

namespace Shipping.Api;

/// <summary>Shipping module composition root — registers all DI services and maps endpoints.</summary>
public static class ShippingModule
{
    /// <summary>Registers Shipping application + infrastructure services.</summary>
    public static IServiceCollection AddShippingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddShippingApplication();
        services.AddShippingInfrastructure(config);
        return services;
    }

    /// <summary>Maps Shipping minimal API endpoints. Controllers are discovered via <c>AddApplicationPart</c>.</summary>
    public static IEndpointRouteBuilder MapShippingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/shipping");
        g.MapGet("/ping", () => Results.Ok("shipping-ok"));
        return app;
    }
}

