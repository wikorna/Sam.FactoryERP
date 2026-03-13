using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Persistence.Configurations;

public sealed class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
    public void Configure(EntityTypeBuilder<Printer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Protocol)
            .IsRequired();

        builder.Property(x => x.Host)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Port)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Dpi)
            .HasDefaultValue(203);

        builder.Property(x => x.LabelWidthMm)
            .HasDefaultValue(0);

        builder.Property(x => x.LabelHeightMm)
            .HasDefaultValue(0);

        builder.Property(x => x.DefaultOrientation)
            .HasDefaultValue(LabelMediaOrientation.Portrait);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        // Unique name for human identification
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("IX_Printers_Name");

        builder.HasIndex(x => x.IsEnabled)
            .HasDatabaseName("IX_Printers_IsEnabled");
    }
}
