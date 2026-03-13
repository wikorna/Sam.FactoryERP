using Labeling.Application.Interfaces;
using Labeling.Infrastructure.Configurations;
using Labeling.Infrastructure.Persistence;
using Labeling.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Labeling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLabelingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LabelingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Bind ZebraPrinters config section
        services.Configure<ZebraPrinterOptions>(
            configuration.GetSection(ZebraPrinterOptions.SectionName));

        // Printer transports — one per protocol
        services.AddTransient<IPrinterTransport, Raw9100PrinterTransport>();

        // Renderer
        services.AddScoped<Labeling.Application.Interfaces.IZplTemplateRenderer, Labeling.Infrastructure.Services.ZplTemplateRenderer>();

        // High-level printer client (resolves transport by protocol, adds retry)
        services.AddScoped<IZplPrinterClient, ZplPrinterClient>();

        // DbContext abstraction
        services.AddScoped<ILabelingDbContext>(sp => sp.GetRequiredService<LabelingDbContext>());

        // Access Control
        services.AddScoped<IPrinterAccessService, PrinterAccessService>();

        return services;
    }
}
