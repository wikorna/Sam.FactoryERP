using Microsoft.Extensions.DependencyInjection;

namespace EDI.Application;

public static class Extensions
{
    public static IServiceCollection AddEdiApplication(this IServiceCollection services)
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        var appAsm = typeof(FactoryERP.Modules.EDI.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(appAsm));

        return services;
    }
}
