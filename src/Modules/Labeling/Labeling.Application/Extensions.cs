using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Labeling.Application;

public static class Extensions
{
    public static IServiceCollection AddLabelingApplication(this IServiceCollection services)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        var appAsm = typeof(FactoryERP.Modules.Labeling.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));
        return services;
    }
}
