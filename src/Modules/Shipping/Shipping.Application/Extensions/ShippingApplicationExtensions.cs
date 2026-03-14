using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Shipping.Application.Extensions;

/// <summary>Registers Shipping application-layer services (MediatR, validators).</summary>
public static class ShippingApplicationExtensions
{
    /// <summary>Registers MediatR handlers and FluentValidation validators from this assembly.</summary>
    public static IServiceCollection AddShippingApplication(this IServiceCollection services)
    {
        var assembly = typeof(FactoryERP.Modules.Shipping.Application.AssemblyMarker).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}

