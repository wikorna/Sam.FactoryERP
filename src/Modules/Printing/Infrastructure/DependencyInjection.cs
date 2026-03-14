using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Printing.Infrastructure.Persistence;
using Printing.Infrastructure.Repositories;
using Printing.Infrastructure.Services;

namespace Printing.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPrintingInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<PrintingDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<IPrintRequestRepository, PrintRequestRepository>();
            services.AddScoped<IPrintJobRepository, PrintJobRepository>();
            services.AddScoped<IPrinterRepository, PrinterRepository>();
            services.AddScoped<IPrinterProfileRepository, PrinterProfileRepository>();
            services.AddScoped<ILabelTemplateRepository, LabelTemplateRepository>();

            // QR payload
            services.AddSingleton<IQrPayloadBuilder, ShipmentQrPayloadBuilder>();

            return services;
        }
    }
}
