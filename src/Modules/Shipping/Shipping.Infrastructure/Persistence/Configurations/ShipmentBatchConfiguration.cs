using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Shipping.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shipping.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="ShipmentBatch"/> aggregate root.</summary>
public sealed class ShipmentBatchConfiguration : IEntityTypeConfiguration<ShipmentBatch>
{
    public void Configure(EntityTypeBuilder<ShipmentBatch> builder)
    {
        builder.ToTable("ShipmentBatches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BatchNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PoReference)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.ReviewDecision)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ReviewComment)
            .HasMaxLength(2000);

        builder.Property(x => x.SourceFileName)
            .HasMaxLength(500);

        builder.Property(x => x.SourceFileSha256)
            .HasMaxLength(64);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(200);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasMany(x => x.Items)
            .WithOne(x => x.ShipmentBatch)
            .HasForeignKey(x => x.ShipmentBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RowErrors)
            .WithOne(x => x.ShipmentBatch)
            .HasForeignKey(x => x.ShipmentBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.BatchNumber)
            .IsUnique()
            .HasDatabaseName("IX_ShipmentBatches_BatchNumber");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_ShipmentBatches_Status");

        builder.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("IX_ShipmentBatches_CreatedAtUtc");

        builder.HasIndex(x => x.PoReference)
            .HasDatabaseName("IX_ShipmentBatches_PoReference");

        // Ignore domain events collection — not persisted.
        builder.Ignore(x => x.DomainEvents);
    }
}

