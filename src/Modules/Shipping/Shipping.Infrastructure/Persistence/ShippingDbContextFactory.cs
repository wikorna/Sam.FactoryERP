using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Shipping.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="ShippingDbContext"/>.
/// Used by <c>dotnet ef migrations add / database update</c> without a running host.
/// </summary>
public sealed class ShippingDbContextFactory : IDesignTimeDbContextFactory<ShippingDbContext>
{
    public ShippingDbContext CreateDbContext(string[] args)
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
                     "Run with --startup-project pointing to ApiHost or WorkerHost.");

        var optionsBuilder = new DbContextOptionsBuilder<ShippingDbContext>();
        optionsBuilder.UseNpgsql(cs, b =>
            b.MigrationsHistoryTable("__EFMigrationsHistory", "shipping"));

        return new ShippingDbContext(optionsBuilder.Options);
    }
}

