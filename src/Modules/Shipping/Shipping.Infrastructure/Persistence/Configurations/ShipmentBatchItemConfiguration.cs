using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shipping.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="ShipmentBatchItem"/>.</summary>
public sealed class ShipmentBatchItemConfiguration : IEntityTypeConfiguration<ShipmentBatchItem>
{
    public void Configure(EntityTypeBuilder<ShipmentBatchItem> builder)
    {
        builder.ToTable("ShipmentBatchItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShipmentBatchId)
            .IsRequired();

        builder.Property(x => x.LineNumber)
            .IsRequired();

        builder.Property(x => x.PartNo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ProductName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.PoNumber)
            .HasMaxLength(100);

        builder.Property(x => x.PoItem)
            .HasMaxLength(50);

        builder.Property(x => x.DueDate)
            .HasMaxLength(30);

        builder.Property(x => x.RunNo)
            .HasMaxLength(50);

        builder.Property(x => x.Store)
            .HasMaxLength(100);

        builder.Property(x => x.QrPayload)
            .HasMaxLength(4000);

        builder.Property(x => x.Remarks)
            .HasMaxLength(2000);

        builder.Property(x => x.LabelCopies)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.IsPrinted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.ShipmentBatchId)
            .HasDatabaseName("IX_ShipmentBatchItems_BatchId");

        builder.HasIndex(x => x.PartNo)
            .HasDatabaseName("IX_ShipmentBatchItems_PartNo");

        builder.HasIndex(x => new { x.ShipmentBatchId, x.LineNumber })
            .IsUnique()
            .HasDatabaseName("IX_ShipmentBatchItems_BatchId_LineNumber");

        // Ignore domain events collection — not persisted.
        builder.Ignore(x => x.DomainEvents);
    }
}

