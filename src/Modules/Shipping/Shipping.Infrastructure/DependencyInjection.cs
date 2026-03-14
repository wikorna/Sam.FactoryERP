using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shipping.Application.Abstractions;
using Shipping.Infrastructure.Parsers;
using Shipping.Infrastructure.Persistence;
using Shipping.Infrastructure.Persistence.Repositories;
using Shipping.Infrastructure.Services;

namespace Shipping.Infrastructure;

/// <summary>Registers Shipping infrastructure services (DbContext, repositories).</summary>
public static class DependencyInjection
{
    /// <summary>Adds the Shipping module infrastructure to the DI container.</summary>
    public static IServiceCollection AddShippingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ShippingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IShippingDbContext>(sp => sp.GetRequiredService<ShippingDbContext>());
        services.AddScoped<IShipmentBatchRepository, ShipmentBatchRepository>();
        services.AddScoped<IShipmentCsvParser, ShipmentCsvParser>();
        services.AddScoped<IBatchNumberGenerator, SequentialBatchNumberGenerator>();

        return services;
    }
}

