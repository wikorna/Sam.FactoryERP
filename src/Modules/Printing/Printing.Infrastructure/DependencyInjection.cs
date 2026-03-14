using Microsoft.Extensions.DependencyInjection;
using Printing.Application.Abstractions;
using Printing.Infrastructure.Services;
using Printing.Infrastructure.Strategies;

namespace Printing.Infrastructure;

/// <summary>Registers Printing module infrastructure services.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the Printing module infrastructure to the DI container.
    /// Call this from WorkerHost after <c>AddLabelingInfrastructure</c> and
    /// <c>AddShippingInfrastructure</c>, since it depends on both.
    /// </summary>
    public static IServiceCollection AddPrintingInfrastructure(
        this IServiceCollection services)
    {
        // QR payload
        services.AddSingleton<IQrPayloadBuilder, ShipmentQrPayloadBuilder>();

        // Resolvers — scoped to match EF Core DbContext lifetime
        services.AddScoped<ILabelTemplateResolver, LabelingDbTemplateResolver>();
        services.AddScoped<IPrinterProfileResolver, LabelingDbPrinterProfileResolver>();

        // Printer client — stateless; singleton is fine
        services.AddSingleton<ILabelPrinterClient, ZebraLabelPrinterClient>();

        // Versioned template strategies
        services.AddSingleton<ITemplatePrintStrategy, V1ShipmentLabelStrategy>();
        // Register TemplatePrintStrategySelector after all strategies are registered
        services.AddSingleton<TemplatePrintStrategySelector>();

        return services;
    }
}

