using Microsoft.Extensions.DependencyInjection;

namespace Printing.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddPrintingDomain(this IServiceCollection services)
    {
        // Register domain services here if any
        return services;
    }
}

