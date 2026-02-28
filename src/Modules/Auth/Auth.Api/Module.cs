using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace Auth.Api;

/// <summary>Auth module registration for the ApiHost composition root.</summary>
public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration config)
    {
        var appAsm = typeof(Auth.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        services.AddValidatorsFromAssembly(appAsm);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy("AuthLoginPolicy", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Controller-based routing is used; no minimal API mapping needed.
        return app;
    }
}
