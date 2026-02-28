using FactoryERP.Abstractions.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryERP.Abstractions.Extensions;

/// <summary>
/// Registers global CQRS pipeline behaviors. Call once from ApiHost.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MediatR pipeline behaviors: Logging → Performance → Validation.
    /// Order matters: outermost runs first.
    /// </summary>
    public static IServiceCollection AddCqrsBehaviors(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers all FluentValidation validators from a given assembly.
    /// Call per module.
    /// </summary>
    public static IServiceCollection AddValidatorsFromModule<TMarker>(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(TMarker).Assembly, includeInternalTypes: true);
        return services;
    }
}
