using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Printing.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="PrintingDbContext"/>.
/// Used by <c>dotnet ef migrations add / database update</c> without a running host.
/// </summary>
public sealed class PrintingDbContextFactory : IDesignTimeDbContextFactory<PrintingDbContext>
{
    public PrintingDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "ConnectionStrings:DefaultConnection not found. " +
                     "Run with --startup-project pointing to WorkerHost.");

        var optionsBuilder = new DbContextOptionsBuilder<PrintingDbContext>();
        optionsBuilder.UseNpgsql(cs, b =>
            b.MigrationsHistoryTable("__EFMigrationsHistory", "printing"));

        return new PrintingDbContext(optionsBuilder.Options);
    }
}
