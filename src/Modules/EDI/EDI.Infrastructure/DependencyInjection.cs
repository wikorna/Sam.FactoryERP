using EDI.Application.Abstractions;
using EDI.Infrastructure.Detection;
using EDI.Infrastructure.FileStores;
using EDI.Infrastructure.Outbox;
using EDI.Infrastructure.Parsers;
using EDI.Infrastructure.Parsers.ItemMaster;
using EDI.Infrastructure.Persistence;
using EDI.Infrastructure.Persistence.Repositories;
using EDI.Infrastructure.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EDI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEdiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<EdiDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("EdiDatabase"),
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "edi")));

        // Repositories
        services.AddScoped<IEdiFileJobRepository, EdiFileJobRepository>();
        services.AddScoped<IEdiFileTypeConfigRepository, EdiFileTypeConfigRepository>();

        // File handling
        services.AddScoped<IEdiFileStore, FileSystemEdiFileStore>();

        // Parsing (legacy typed parsers)
        services.AddScoped<IEdiParser, CsvItemMasterParser>();
        services.AddScoped<IEdiParserFactory, EdiParserFactory>();

        // Config-driven parsing
        services.AddScoped<ConfigDrivenCsvParser>();
        services.AddScoped<IFileTypeDetector, FileTypeDetector>();

        // Schema-driven file detection (IEdiFileDetector / IEdiSchemaProvider)
        services.AddSingleton<IEdiSchemaProvider>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JsonEdiSchemaProvider>>();
            var schemaDir = Path.Combine(AppContext.BaseDirectory, "Schemas");
            return new JsonEdiSchemaProvider(logger, schemaDir);
        });
        services.AddScoped<IEdiFileDetector, CsvEdiFileDetector>();

        // Staging and Outbox
        services.AddScoped<IStagingRepository, SqlStagingRepository>();
        services.AddScoped<IOutboxPublisher, SqlOutboxPublisher>();

        // Integration
        services.AddScoped<IItemMasterApplyService, EDI.Infrastructure.Integration.InventoryItemMasterApplyService>();

        return services;
    }
}
