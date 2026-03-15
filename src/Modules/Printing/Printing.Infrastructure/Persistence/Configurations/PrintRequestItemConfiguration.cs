using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Printing.Domain;

namespace Printing.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="PrintRequestItem"/>.</summary>
public sealed class PrintRequestItemConfiguration : IEntityTypeConfiguration<PrintRequestItem>
{
    public void Configure(EntityTypeBuilder<PrintRequestItem> builder)
    {
        builder.ToTable("PrintRequestItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PrintRequestId)
            .IsRequired();

        builder.Property(x => x.ShipmentBatchItemId)
            .IsRequired();

        builder.Property(x => x.LineNumber)
            .IsRequired();

        builder.Property(x => x.PartNo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CustomerCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        // PrintRequest FK is owned by PrintRequestConfiguration.HasMany

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_PrintRequestItems_IdempotencyKey");

        builder.HasIndex(x => x.PrintRequestId)
            .HasDatabaseName("IX_PrintRequestItems_PrintRequestId");

        builder.HasIndex(x => x.ShipmentBatchItemId)
            .HasDatabaseName("IX_PrintRequestItems_ShipmentBatchItemId");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_PrintRequestItems_Status");
    }
}
