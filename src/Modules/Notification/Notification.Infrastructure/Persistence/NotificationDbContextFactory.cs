using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Notification.Infrastructure.Persistence;

/// <summary>Design-time factory — used only by <c>dotnet ef migrations</c>.</summary>
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        // Resolve connection string from environment or local appsettings
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=FactoryDB;Username=wikorna;Password=password";

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(
                connectionString,
                b => b.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"))
            .Options;

        return new NotificationDbContext(options);
    }
}

