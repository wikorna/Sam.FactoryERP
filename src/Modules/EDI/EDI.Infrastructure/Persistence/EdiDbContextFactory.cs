using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EDI.Infrastructure.Persistence;

public class EdiDbContextFactory : IDesignTimeDbContextFactory<EdiDbContext>
{
    // public EdiDbContext CreateDbContext(string[] args)
    // {
    //     var optionsBuilder = new DbContextOptionsBuilder<EdiDbContext>();
    //     optionsBuilder.UseSqlServer("Server=localhost;Database=EdiDatabase;Integrated Security=true;TrustServerCertificate=true",
    //         b => b.MigrationsHistoryTable("__EFMigrationsHistory", "edi"));

    //     return new EdiDbContext(optionsBuilder.Options);
    // }
    public EdiDbContext CreateDbContext(string[] args)
    {
        // EF Core CLI sets the current directory to the --startup-project path automatically.
        var basePath = Directory.GetCurrentDirectory();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection") 
                 ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection not found.");

        var optionsBuilder = new DbContextOptionsBuilder<EdiDbContext>();
        optionsBuilder.UseNpgsql(cs, b => b.MigrationsHistoryTable("__EFMigrationsHistory", "edi"));

        return new EdiDbContext(optionsBuilder.Options);
    }
}
