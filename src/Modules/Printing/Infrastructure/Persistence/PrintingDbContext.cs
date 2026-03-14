using Microsoft.EntityFrameworkCore;
using Printing.Domain;

namespace Printing.Infrastructure.Persistence;

public class PrintingDbContext : DbContext
{
    public PrintingDbContext(DbContextOptions<PrintingDbContext> options) : base(options)
    {
    }

    public DbSet<PrintJob> PrintJobs { get; set; }
    public DbSet<Printer> Printers { get; set; }
    public DbSet<PrinterProfile> PrinterProfiles { get; set; }
    public DbSet<LabelTemplate> LabelTemplates { get; set; }
    public DbSet<PrintRequest> PrintRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("printing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrintingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
