using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application;
using Notification.Infrastructure;

namespace Notification.Api;

public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddNotificationInfrastructure(config);
        services.AddNotificationApplication();
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Controllers are discovered via AddApplicationPart in ApiHost.
        return app;
    }
}

