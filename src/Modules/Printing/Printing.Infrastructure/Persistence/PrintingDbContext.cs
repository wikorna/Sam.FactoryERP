using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;

namespace Printing.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Printing module.
/// All tables live in the <c>printing</c> PostgreSQL schema.
/// </summary>
public sealed class PrintingDbContext : DbContext, IPrintingDbContext
{
    public PrintingDbContext(DbContextOptions<PrintingDbContext> options) : base(options) { }

    public DbSet<PrintRequest> PrintRequests => Set<PrintRequest>();
    public DbSet<PrintRequestItem> PrintRequestItems => Set<PrintRequestItem>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<PrintResult> PrintResults => Set<PrintResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("printing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrintingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
