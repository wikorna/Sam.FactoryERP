using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Labeling.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <see cref="LabelingDbContext"/>.
/// Used by <c>dotnet ef migrations add / database update</c> without a running host.
/// EF Core CLI sets CWD to the --startup-project path, so appsettings.json
/// from ApiHost or WorkerHost is resolved automatically.
/// </summary>
public sealed class LabelingDbContextFactory : IDesignTimeDbContextFactory<LabelingDbContext>
{
    public LabelingDbContext CreateDbContext(string[] args)
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

        var optionsBuilder = new DbContextOptionsBuilder<LabelingDbContext>();
        optionsBuilder.UseNpgsql(cs, b =>
            b.MigrationsHistoryTable("__EFMigrationsHistory", "labeling"));

        return new LabelingDbContext(optionsBuilder.Options);
    }
}

