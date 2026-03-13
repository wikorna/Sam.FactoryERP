using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Infrastructure.Persistence;

public class LabelingDbContext : DbContext, ILabelingDbContext
{
    public LabelingDbContext(DbContextOptions<LabelingDbContext> options) : base(options)
    {
    }

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrinterImage> PrinterImages => Set<PrinterImage>();
    public DbSet<DepartmentPrinter> DepartmentPrinters => Set<DepartmentPrinter>();
    public DbSet<StorePrinter> StorePrinters => Set<StorePrinter>();
    public DbSet<UserPrinterOverride> UserPrinterOverrides => Set<UserPrinterOverride>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("labeling");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LabelingDbContext).Assembly);

        // MassTransit Outbox/Inbox tables in the labeling schema
        modelBuilder.AddInboxStateEntity(cfg => cfg.ToTable("InboxState", "labeling"));
        modelBuilder.AddOutboxMessageEntity(cfg => cfg.ToTable("OutboxMessage", "labeling"));
        modelBuilder.AddOutboxStateEntity(cfg => cfg.ToTable("OutboxState", "labeling"));
        base.OnModelCreating(modelBuilder);
    }
}
