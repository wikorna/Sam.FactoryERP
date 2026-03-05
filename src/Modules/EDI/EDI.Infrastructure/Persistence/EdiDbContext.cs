using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EDI.Infrastructure.Persistence;

public class EdiDbContext : DbContext
{
    public EdiDbContext(DbContextOptions<EdiDbContext> options) : base(options)
    {
    }

    public DbSet<EdiFileJob> EdiFileJobs => Set<EdiFileJob>();
    public DbSet<PartnerProfile> PartnerProfiles => Set<PartnerProfile>();
    public DbSet<ItemMasterStagingRecord> StagingRecords => Set<ItemMasterStagingRecord>();
    public DbSet<PurchaseOrderStagingHeader> PurchaseOrderStagingHeaders => Set<PurchaseOrderStagingHeader>();
    public DbSet<PurchaseOrderStagingDetail> PurchaseOrderStagingDetails => Set<PurchaseOrderStagingDetail>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<EdiStagingFile> EdiStagingFiles => Set<EdiStagingFile>();
    public DbSet<EdiStagingFileError> EdiStagingFileErrors => Set<EdiStagingFileError>();

    // Config-driven EDI
    public DbSet<EdiFileTypeConfig> EdiFileTypeConfigs => Set<EdiFileTypeConfig>();
    public DbSet<EdiColumnDefinition> EdiColumnDefinitions => Set<EdiColumnDefinition>();
    public DbSet<EdiStagingRow> EdiStagingRows => Set<EdiStagingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("edi");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EdiDbContext).Assembly);
    }
}
