using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shipping.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="ShipmentBatchRowError"/>.</summary>
public sealed class ShipmentBatchRowErrorConfiguration : IEntityTypeConfiguration<ShipmentBatchRowError>
{
    public void Configure(EntityTypeBuilder<ShipmentBatchRowError> builder)
    {
        builder.ToTable("ShipmentBatchRowErrors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShipmentBatchId)
            .IsRequired();

        builder.Property(x => x.RowNumber)
            .IsRequired();

        builder.Property(x => x.ErrorCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.ShipmentBatchId)
            .HasDatabaseName("IX_ShipmentBatchRowErrors_BatchId");

        builder.HasIndex(x => new { x.ShipmentBatchId, x.RowNumber })
            .HasDatabaseName("IX_ShipmentBatchRowErrors_BatchId_Row");
    }
}

