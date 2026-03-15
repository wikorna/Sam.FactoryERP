using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Printing.Domain;

namespace Printing.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for <see cref="PrintResult"/>.</summary>
public sealed class PrintResultConfiguration : IEntityTypeConfiguration<PrintResult>
{
    public void Configure(EntityTypeBuilder<PrintResult> builder)
    {
        builder.ToTable("PrintResults");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PrintJobId)
            .IsRequired();

        builder.Property(x => x.IsSuccess)
            .IsRequired();

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // ── Relationships ─────────────────────────────────────────────────
        // Cascade delete: removing a PrintJob removes its outcome history.
        builder.HasOne(x => x.PrintJob)
            .WithMany()
            .HasForeignKey(x => x.PrintJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────
        builder.HasIndex(x => x.PrintJobId)
            .HasDatabaseName("IX_PrintResults_PrintJobId");

        builder.HasIndex(x => x.IsSuccess)
            .HasDatabaseName("IX_PrintResults_IsSuccess");
    }
}
