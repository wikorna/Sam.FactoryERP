using Microsoft.Extensions.DependencyInjection;

namespace Notification.Application;

public static class Extensions
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        var appAsm = typeof(FactoryERP.Modules.Notification.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }
}

