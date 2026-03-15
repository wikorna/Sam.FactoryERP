using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Printing.Domain;

namespace Printing.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="PrintRequest"/> aggregate root.</summary>
public sealed class PrintRequestConfiguration : IEntityTypeConfiguration<PrintRequest>
{
    public void Configure(EntityTypeBuilder<PrintRequest> builder)
    {
        builder.ToTable("PrintRequests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BatchId)
            .IsRequired();

        builder.Property(x => x.BatchNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.RequestedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        // ── Relationships ─────────────────────────────────────────────────
        builder.HasMany(x => x.Items)
            .WithOne(x => x.PrintRequest)
            .HasForeignKey(x => x.PrintRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_PrintRequests_IdempotencyKey");

        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("IX_PrintRequests_BatchId");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_PrintRequests_Status");

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_PrintRequests_Status_CreatedAtUtc");
    }
}
