using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Notification.Infrastructure.Persistence;

/// <summary>Design-time factory — used only by <c>dotnet ef migrations</c>.</summary>
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(cs, b => b.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options;

        return new NotificationDbContext(options);
    }
}

