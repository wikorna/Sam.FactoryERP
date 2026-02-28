using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Persistence.Configurations;

public sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.PrinterId)
            .IsRequired();

        builder.Property(x => x.ZplPayload)
            .IsRequired();

        builder.Property(x => x.Copies)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.FailCount)
            .HasDefaultValue(0);

        builder.Property(x => x.LastErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.LastErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CorrelationId)
            .IsRequired();

        builder.Property(x => x.RequestedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        // ── FK to Printer ─────────────────────────────────────────────────
        builder.HasOne(x => x.Printer)
            .WithMany()
            .HasForeignKey(x => x.PrinterId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("IX_PrintJobs_IdempotencyKey");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_PrintJobs_Status");

        builder.HasIndex(x => x.PrinterId)
            .HasDatabaseName("IX_PrintJobs_PrinterId");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("IX_PrintJobs_CorrelationId");

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_PrintJobs_Status_CreatedAtUtc");
    }
}
